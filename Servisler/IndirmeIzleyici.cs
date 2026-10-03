using System.IO;
using System.Runtime.InteropServices;

namespace DinamikAda.Servisler;

/// İndirilenler klasörünü izler: yeni dosya tamamlanınca (boyut 2 sn sabit, dosya açılabiliyor) Indirildi olayı.
/// Tarayıcıların geçici uzantıları (.crdownload, .part, .tmp…) yoksayılır.
public sealed class IndirmeIzleyici : IDisposable
{
    private static readonly string[] GeciciUzantilar = { ".crdownload", ".part", ".partial", ".tmp", ".download", ".opdownload", ".!qb", ".bc!", ".td" };
    private FileSystemWatcher? _izleyici;
    private readonly Dictionary<string, DateTime> _bekleyen = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _bildirilen = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Timers.Timer _zaman = new(1000);

    public event Action<string>? Indirildi;   // tam yol (arka plan iş parçacığından gelir)
    public string Klasor { get; } = IndirilenlerKlasoru();

    public bool Baslat()
    {
        try
        {
            if (!Directory.Exists(Klasor)) return false;
            _izleyici = new FileSystemWatcher(Klasor) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite };
            _izleyici.Created += (_, e) => Aday(e.FullPath);
            _izleyici.Renamed += (_, e) => Aday(e.FullPath);
            _izleyici.Changed += (_, e) => { lock (_bekleyen) if (_bekleyen.ContainsKey(e.FullPath)) _bekleyen[e.FullPath] = DateTime.Now; };
            _izleyici.EnableRaisingEvents = true;
            _zaman.Elapsed += (_, _) => Kontrol();
            _zaman.Start();
            return true;
        }
        catch { return false; }
    }

    private void Aday(string yol)
    {
        string uz = Path.GetExtension(yol);
        if (GeciciUzantilar.Contains(uz, StringComparer.OrdinalIgnoreCase)) return;
        string ad = Path.GetFileName(yol);
        if (ad.StartsWith("~$") || ad.StartsWith(".")) return;
        lock (_bekleyen) { if (!_bildirilen.Contains(yol)) _bekleyen[yol] = DateTime.Now; }
    }

    /// Saniyede bir: 2 sn'dir değişmeyen ve kilitli olmayan dosyayı bildir
    private void Kontrol()
    {
        List<string> hazir = new();
        lock (_bekleyen)
        {
            foreach (var (yol, son) in _bekleyen.ToList())
            {
                if (!File.Exists(yol)) { _bekleyen.Remove(yol); continue; }
                if ((DateTime.Now - son).TotalSeconds < 2) continue;
                try
                {
                    var bilgi = new FileInfo(yol);
                    if ((bilgi.Attributes & (FileAttributes.Hidden | FileAttributes.Temporary)) != 0) { _bekleyen.Remove(yol); continue; }
                    using var fs = File.Open(yol, FileMode.Open, FileAccess.Read, FileShare.Read);   // yazan süreç bitti mi
                    if (bilgi.Length == 0) continue;
                    _bekleyen.Remove(yol); _bildirilen.Add(yol); hazir.Add(yol);
                }
                catch { _bekleyen[yol] = DateTime.Now; }   // hâlâ yazılıyor
            }
            if (_bildirilen.Count > 500) _bildirilen.Clear();
        }
        foreach (var y in hazir) Indirildi?.Invoke(y);
    }

    private static string IndirilenlerKlasoru()
    {
        try
        {
            var kimlik = new Guid("374DE290-123F-4565-9164-39C4925E467B");   // FOLDERID_Downloads
            if (SHGetKnownFolderPath(ref kimlik, 0, IntPtr.Zero, out var p) == 0)
            {
                string yol = Marshal.PtrToStringUni(p) ?? "";
                Marshal.FreeCoTaskMem(p);
                if (yol.Length > 0) return yol;
            }
        }
        catch { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid rfid, uint flags, IntPtr token, out IntPtr path);

    public void Dispose() { _zaman.Stop(); _izleyici?.Dispose(); }
}
