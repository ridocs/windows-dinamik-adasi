using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DinamikAda.Servisler;

/// Haznedeki tek dosya ya da klasör.
public sealed class HazneOgesi : INotifyPropertyChanged
{
    public string Yol { get; init; } = "";
    public string Ad { get; init; } = "";
    public bool Klasor { get; init; }
    public long Boyut { get; init; }
    public DateTime Tarih { get; init; }
    public ImageSource? Simge { get; init; }
    public bool Resim { get; init; }

    private string _durum = "";
    public string Durum { get => _durum; set { _durum = value; Bildir(nameof(Durum)); Bildir(nameof(AltBilgi)); } }

    private string? _link;
    public string? Link { get => _link; set { _link = value; Bildir(nameof(Link)); } }

    public string BoyutMetni => Klasor ? "klasör" : Boyut < 1024 ? $"{Boyut} B" : Boyut < 1024 * 1024 ? $"{Boyut / 1024.0:0.#} KB" : Boyut < 1024L * 1024 * 1024 ? $"{Boyut / 1048576.0:0.#} MB" : $"{Boyut / 1073741824.0:0.##} GB";
    public string AltBilgi => string.IsNullOrEmpty(Durum) ? $"{BoyutMetni} · {Tarih:dd.MM HH:mm}" : Durum;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Bildir(string ad) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(ad));
}

/// Hazne: bırakılan dosyaların listesi, diskte kalıcı (yollar), pano ve zip yardımcıları.
public sealed class HazneDeposu
{
    public ObservableCollection<HazneOgesi> Ogeler { get; } = new();

    private static string Dosya => Path.Combine(Ayarlar.Klasor, "hazne.json");
    public static string PanoKlasoru => Path.Combine(Ayarlar.Klasor, "hazne");

    public event Action? Degisti;

    public void Yukle()
    {
        try
        {
            if (!File.Exists(Dosya)) return;
            var yollar = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Dosya)) ?? Array.Empty<string>();
            foreach (var y in yollar) Ekle(y, kaydet: false);
        }
        catch { }
    }

    public void Kaydet()
    {
        try
        {
            Directory.CreateDirectory(Ayarlar.Klasor);
            File.WriteAllText(Dosya, JsonSerializer.Serialize(Ogeler.Select(o => o.Yol).ToArray()));
        }
        catch { }
    }

    /// Son Ekle çağrısı yinelenen bir öğe yüzünden atlandıysa true.
    public bool SonEklemeYinelendi { get; private set; }

    public HazneOgesi? Ekle(string yol, bool kaydet = true)
    {
        SonEklemeYinelendi = false;
        try
        {
            yol = Path.GetFullPath(yol);
            bool klasor = Directory.Exists(yol);
            if (!klasor && !File.Exists(yol)) return null;
            if (Ogeler.Any(o => string.Equals(o.Yol, yol, StringComparison.OrdinalIgnoreCase))) { SonEklemeYinelendi = true; return null; }

            var bilgi = klasor ? null : new FileInfo(yol);
            // Aynı dosyanın başka yoldaki kopyası: ad + boyut + değişiklik zamanı eşleşiyorsa tekrar ekleme
            if (bilgi != null && Ogeler.Any(o => !o.Klasor && o.Boyut == bilgi.Length
                                                  && string.Equals(o.Ad, bilgi.Name, StringComparison.OrdinalIgnoreCase)
                                                  && Math.Abs((o.Tarih - bilgi.LastWriteTime).TotalSeconds) < 2))
                return null;
            string uz = Path.GetExtension(yol).ToLowerInvariant();
            bool resim = uz is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
            var oge = new HazneOgesi
            {
                Yol = yol,
                Ad = Path.GetFileName(yol.TrimEnd('\\', '/')),
                Klasor = klasor,
                Boyut = bilgi?.Length ?? 0,
                Tarih = bilgi?.LastWriteTime ?? Directory.GetLastWriteTime(yol),
                Resim = resim,
                Simge = SimgeUret(yol, resim),
            };
            Ogeler.Insert(0, oge);
            if (kaydet) Kaydet();
            Degisti?.Invoke();
            return oge;
        }
        catch { return null; }
    }

    public void Cikar(HazneOgesi oge) { Ogeler.Remove(oge); Kaydet(); Degisti?.Invoke(); }
    public void Temizle() { Ogeler.Clear(); Kaydet(); Degisti?.Invoke(); }

    /// Pano: dosya listesi, görsel (PNG olarak kaydedilir) ya da metin (TXT). Eklenen sayısını döner.
    public int PanodanEkle()
    {
        int n = 0;
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                foreach (string y in Clipboard.GetFileDropList()) if (Ekle(y) != null) n++;
                return n;
            }
            Directory.CreateDirectory(PanoKlasoru);
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (img == null) return 0;
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(img));
                using var bellek = new MemoryStream();
                enc.Save(bellek);
                var veri = bellek.ToArray();
                if (AyniIcerikVar(veri)) { SonEklemeYinelendi = true; return 0; }
                string yol = Path.Combine(PanoKlasoru, $"pano-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                File.WriteAllBytes(yol, veri);
                return Ekle(yol) != null ? 1 : 0;
            }
            if (Clipboard.ContainsText())
            {
                string metin = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(metin)) return 0;
                var veri = System.Text.Encoding.UTF8.GetBytes(metin);
                if (AyniIcerikVar(veri)) { SonEklemeYinelendi = true; return 0; }
                string yol = Path.Combine(PanoKlasoru, $"pano-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllBytes(yol, veri);
                return Ekle(yol) != null ? 1 : 0;
            }
        }
        catch { }
        return n;
    }

    /// Aynı içerik haznede zaten var mı (panodan yapıştırılanlar için; önce uzunluk, sonra SHA-256).
    private bool AyniIcerikVar(byte[] veri)
    {
        byte[]? ozet = null;
        foreach (var o in Ogeler)
        {
            if (o.Klasor || o.Boyut != veri.LongLength) continue;
            try
            {
                ozet ??= System.Security.Cryptography.SHA256.HashData(veri);
                var mevcut = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(o.Yol));
                if (ozet.AsSpan().SequenceEqual(mevcut)) return true;
            }
            catch { }
        }
        return false;
    }

    /// Haznedekileri masaüstünde tek zip'e toplar, zip'i hazneye ekler, yolunu döner.
    public string? ZipYap()
    {
        if (Ogeler.Count == 0) return null;
        try
        {
            string masaustu = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string zip = Path.Combine(masaustu, $"hazne-{DateTime.Now:yyyyMMdd-HHmm}.zip");
            using (var arsiv = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                foreach (var o in Ogeler.ToList())
                {
                    if (o.Yol.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && o.Yol == zip) continue;
                    if (o.Klasor)
                    {
                        foreach (var f in Directory.EnumerateFiles(o.Yol, "*", SearchOption.AllDirectories))
                            arsiv.CreateEntryFromFile(f, Path.Combine(o.Ad, Path.GetRelativePath(o.Yol, f)).Replace('\\', '/'), CompressionLevel.Optimal);
                    }
                    else arsiv.CreateEntryFromFile(o.Yol, o.Ad, CompressionLevel.Optimal);
                }
            }
            Ekle(zip);
            return zip;
        }
        catch { return null; }
    }

    // ---- simge / küçük resim ----

    private static ImageSource? SimgeUret(string yol, bool resim)
    {
        try
        {
            if (resim)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 96;
                bmp.UriSource = new Uri(yol);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            return DosyaSimgesi(yol);
        }
        catch { return null; }
    }

    /// Windows kabuk simgesi (exe, belge, klasör). Kısayol çubuğu da kullanır.
    public static ImageSource? DosyaSimgesi(string yol)
    {
        try
        {
            var shfi = new SHFILEINFO();
            IntPtr h = SHGetFileInfo(yol, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
            if (h == IntPtr.Zero || shfi.hIcon == IntPtr.Zero) return null;
            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                src.Freeze();
                return src;
            }
            finally { DestroyIcon(shfi.hIcon); }
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    private const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0x0;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO psfi, uint cb, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
}
