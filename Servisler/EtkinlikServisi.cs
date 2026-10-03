using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DinamikAda.Servisler;

public enum CanavarModu { Normal, Oyun, Is, Muzik, Uyku }

/// Kullanıcının ne yaptığını kabaca anlar: ön plandaki sürecin adı, tam ekran, medya, son giriş zamanı.
public sealed class EtkinlikServisi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder sb, int max);

    // Süreç adı listede olmasa da pencere başlığından tanınan IDE / editörler
    private static readonly string[] IsBaslikIpuclari =
    {
        "Visual Studio Code", "Visual Studio", "Cursor", "Windsurf", "JetBrains", "IntelliJ", "PyCharm", "WebStorm", "Rider",
        "Android Studio", "Sublime Text", "Notepad++", "SOLIDWORKS", "Fusion", "Blender", "Figma", "Obsidian", "Terminal", "PowerShell",
    };

    private static readonly HashSet<string> IsUygulamalari = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code", "devenv", "rider64", "idea64", "pycharm64", "webstorm64", "clion64", "goland64",
        "WindowsTerminal", "powershell", "pwsh", "cmd", "mintty", "wsl",
        "WINWORD", "EXCEL", "POWERPNT", "OUTLOOK", "ONENOTE", "notepad", "notepad++",
        "SLDWORKS", "Fusion360", "AutoCAD", "acad", "Blender", "Figma", "Obsidian",
        "Postman", "dbeaver", "DataGrip64", "Photoshop", "Illustrator", "Premiere",
    };

    private static readonly string[] OyunYolIpuclari =
    {
        "steamapps", "Riot Games", "Epic Games", "GOG Galaxy", "Ubisoft", "EA Games", "Battle.net",
        "WindowsApps\\Microsoft.", "Xbox", "Rockstar", "Forza", "VALORANT", "League of Legends",
    };

    private readonly Dictionary<uint, (string Ad, string Yol)> _surecOnbellek = new();

    public CanavarModu Mod { get; private set; } = CanavarModu.Normal;
    public string Aciklama { get; private set; } = "";

    public event Action<CanavarModu, string>? Degisti;

    public void Tik(bool medyaOynuyor, bool onPlanTamEkran, int uykuDakika)
    {
        var (mod, aciklama) = Hesapla(medyaOynuyor, onPlanTamEkran, uykuDakika);
        if (mod != Mod || aciklama != Aciklama)
        {
            Mod = mod; Aciklama = aciklama;
            Degisti?.Invoke(mod, aciklama);
        }
    }

    private (CanavarModu, string) Hesapla(bool medyaOynuyor, bool tamEkran, int uykuDakika)
    {
        if (BostaDakika() >= Math.Max(1, uykuDakika)) return (CanavarModu.Uyku, "uyuyor");

        var (ad, yol) = OnPlanSurec();
        string baslik = OnPlanBaslik();
        bool isUyg = IsUygulamalari.Contains(ad)
                     || IsBaslikIpuclari.Any(i => baslik.Contains(i, StringComparison.OrdinalIgnoreCase));
        bool oyun = tamEkran && !isUyg
                    || OyunYolIpuclari.Any(i => yol.Contains(i, StringComparison.OrdinalIgnoreCase));
        if (oyun && ad.Length > 0) return (CanavarModu.Oyun, "oyunda");
        if (isUyg) return (CanavarModu.Is, "işte");
        if (medyaOynuyor) return (CanavarModu.Muzik, "müzikte");
        return (CanavarModu.Normal, "");
    }

    private static double BostaDakika()
    {
        var li = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref li)) return 0;
        return (Environment.TickCount64 - li.dwTime) / 60000.0;
    }

    private static string OnPlanBaslik()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return "";
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(h, sb, sb.Capacity);
            return sb.ToString();
        }
        catch { return ""; }
    }

    private (string Ad, string Yol) OnPlanSurec()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return ("", "");
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == 0) return ("", "");
            if (_surecOnbellek.TryGetValue(pid, out var bilinen)) return bilinen;

            using var p = Process.GetProcessById((int)pid);
            string yol = "";
            try { yol = p.MainModule?.FileName ?? ""; } catch { /* yükseltilmiş süreç: yol okunamaz */ }
            var sonuc = (p.ProcessName, yol);
            if (_surecOnbellek.Count > 200) _surecOnbellek.Clear();
            _surecOnbellek[pid] = sonuc;
            return sonuc;
        }
        catch { return ("", ""); }
    }
}
