using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DinamikAda.Servisler;

/// Bir çalışma düzenindeki tek pencere: hangi exe, hangi başlık, nerede (fiziksel piksel), büyütülmüş mü.
public sealed class DuzenPencere
{
    public string Exe { get; set; } = "";
    public string Baslik { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public bool Maksimize { get; set; }
}

public sealed class DuzenKaydi
{
    public string Ad { get; set; } = "";
    public List<DuzenPencere> Pencereler { get; set; } = new();
}

/// Çalışma düzenleri: açık pencerelerin yerleşimini yakalar, kayıtlı düzeni tek tıkla geri kurar
/// (kapalı uygulamayı başlatır, penceresini bekler, yerine koyar).
public static class DuzenServisi
{
    private static readonly string[] AtlananExe = { "explorer.exe", "ApplicationFrameHost.exe", "TextInputHost.exe", "ShellExperienceHost.exe", "StartMenuExperienceHost.exe", "SearchHost.exe", "DinamikAda.exe" };

    /// Şu an görünen, başlıklı, küçültülmemiş üst düzey pencereler
    public static List<DuzenPencere> Yakala()
    {
        var sonuc = new List<DuzenPencere>();
        foreach (var (h, exe, baslik) in Pencereler())
        {
            if (IsIconic(h)) continue;
            if (AtlananExe.Any(a => string.Equals(a, Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase))) continue;
            if (!CerceveSinirlari(h, out var r)) continue;
            if (r.Right - r.Left < 120 || r.Bottom - r.Top < 80) continue;
            sonuc.Add(new DuzenPencere { Exe = exe, Baslik = baslik, X = r.Left, Y = r.Top, W = r.Right - r.Left, H = r.Bottom - r.Top, Maksimize = IsZoomed(h) });
        }
        return sonuc;
    }

    /// Düzeni kur. Dönüş: (yerleştirilen, başlatılamayan/bulunamayan)
    public static async Task<(int Ok, int Yok, List<string> Baslatilan)> UygulaAsync(DuzenKaydi d, Action<string>? gunluk = null)
    {
        int ok = 0, yok = 0; var baslatilan = new List<string>();
        var kullanilan = new HashSet<IntPtr>();
        var bekleyen = new List<(DuzenPencere P, DateTime Baslangic)>();

        // 1) Açık olanları hemen yerleştir, kapalı olanları başlat
        foreach (var p in d.Pencereler)
        {
            var h = Bul(p, kullanilan);
            if (h != IntPtr.Zero) { kullanilan.Add(h); Yerlestir(h, p); ok++; continue; }
            if (!File.Exists(p.Exe) || AtlananExe.Contains(Path.GetFileName(p.Exe), StringComparer.OrdinalIgnoreCase)) { yok++; gunluk?.Invoke($"duzen: yok {p.Exe}"); continue; }
            try
            {
                Process.Start(new ProcessStartInfo(p.Exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(p.Exe) ?? "" });
                baslatilan.Add(Path.GetFileNameWithoutExtension(p.Exe));
                bekleyen.Add((p, DateTime.Now));
            }
            catch (Exception e) { yok++; gunluk?.Invoke($"duzen: baslatilamadi {p.Exe}: {e.Message}"); }
        }

        // 2) Yeni başlatılanların penceresini bekle (en çok 15 sn), gelince yerleştir
        var son = DateTime.Now.AddSeconds(15);
        while (bekleyen.Count > 0 && DateTime.Now < son)
        {
            await Task.Delay(400);
            for (int i = bekleyen.Count - 1; i >= 0; i--)
            {
                var h = Bul(bekleyen[i].P, kullanilan);
                if (h == IntPtr.Zero) continue;
                kullanilan.Add(h);
                await Task.Delay(300);                       // pencere ilk boyutunu alsın
                Yerlestir(h, bekleyen[i].P); ok++;
                bekleyen.RemoveAt(i);
            }
        }
        yok += bekleyen.Count;
        return (ok, yok, baslatilan);
    }

    private static IntPtr Bul(DuzenPencere p, HashSet<IntPtr> haric)
    {
        IntPtr adayExe = IntPtr.Zero;
        foreach (var (h, exe, baslik) in Pencereler())
        {
            if (haric.Contains(h) || !string.Equals(exe, p.Exe, StringComparison.OrdinalIgnoreCase)) continue;
            if (!CerceveSinirlari(h, out var r) || r.Right - r.Left < 120) continue;
            if (p.Baslik.Length > 0 && baslik.Contains(BaslikCekirdek(p.Baslik), StringComparison.OrdinalIgnoreCase)) return h;
            if (adayExe == IntPtr.Zero) adayExe = h;
        }
        return adayExe;
    }

    /// "proje - Visual Studio Code" → "Visual Studio Code" (son parça: uygulama adı, belge değişse de tutar)
    private static string BaslikCekirdek(string b)
    {
        var parcalar = b.Split(new[] { " - ", " – ", " — ", " | " }, StringSplitOptions.RemoveEmptyEntries);
        return parcalar.Length > 1 ? parcalar[^1].Trim() : b.Trim();
    }

    private static void Yerlestir(IntPtr h, DuzenPencere p)
    {
        // Zaten yerindeyse dokunma (titreme olmasın)
        if (!IsIconic(h) && IsZoomed(h) == p.Maksimize && CerceveSinirlari(h, out var simdi)
            && Math.Abs(simdi.Left - p.X) <= 4 && Math.Abs(simdi.Top - p.Y) <= 4
            && Math.Abs(simdi.Right - simdi.Left - p.W) <= 8 && Math.Abs(simdi.Bottom - simdi.Top - p.H) <= 8) return;
        if (IsIconic(h) || IsZoomed(h)) ShowWindow(h, SW_RESTORE);
        // DWM çerçevesi (gölge payı) pencere dikdörtgeninden geniştir; farkı telafi et
        GetWindowRect(h, out var wr);
        int sl = 0, st = 0, sr = 0, sb = 0;
        if (CerceveSinirlari(h, out var fr)) { sl = fr.Left - wr.Left; st = fr.Top - wr.Top; sr = wr.Right - fr.Right; sb = wr.Bottom - fr.Bottom; }
        SetWindowPos(h, IntPtr.Zero, p.X - sl, p.Y - st, p.W + sl + sr, p.H + st + sb, SWP_NOZORDER | SWP_NOACTIVATE);
        if (p.Maksimize) ShowWindow(h, SW_MAXIMIZE);
    }

    private static IEnumerable<(IntPtr H, string Exe, string Baslik)> Pencereler()
    {
        var liste = new List<(IntPtr, string, string)>();
        uint ben = (uint)Environment.ProcessId;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            int ex = GetWindowLong(h, GWL_EXSTYLE);
            if ((ex & WS_EX_TOOLWINDOW) != 0) return true;
            if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            var sb = new StringBuilder(512);
            if (GetWindowText(h, sb, 512) == 0) return true;
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == ben) return true;
            string exe = ExeYolu(pid);
            if (exe.Length == 0) return true;
            liste.Add((h, exe, sb.ToString()));
            return true;
        }, IntPtr.Zero);
        return liste;
    }

    private static string ExeYolu(uint pid)
    {
        var h = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
        if (h == IntPtr.Zero) return "";
        try
        {
            var sb = new StringBuilder(1024); int n = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString() : "";
        }
        finally { CloseHandle(h); }
    }

    private static bool CerceveSinirlari(IntPtr h, out RECT r)
    {
        if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf<RECT>()) == 0) return true;
        return GetWindowRect(h, out r);
    }

    // ---- Win32 ----
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    private delegate bool EnumProc(IntPtr h, IntPtr l);
    private const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, DWMWA_CLOAKED = 14, DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int SW_RESTORE = 9, SW_MAXIMIZE = 3;
    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder exe, ref int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
}
