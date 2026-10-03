using System.IO;

namespace DinamikAda.Servisler;

/// Sistemdeki uygulamalar: Başlat menüsü kısayolları (masaüstü uygulamaları) + paketli (Store) uygulamalar.
public sealed record KuruluUygulama(string Ad, string Hedef, bool Paketli)
{
    public string Tur => Paketli ? "Store uygulaması" : Path.GetFileName(Hedef);
}

public static class KuruluUygulamalar
{
    public static List<KuruluUygulama> Listele()
    {
        var sonuc = new Dictionary<string, KuruluUygulama>(StringComparer.OrdinalIgnoreCase);

        // 1) Başlat menüsü .lnk dosyaları → gerçek exe hedefi (simge ve ad buradan iyi gelir)
        foreach (var kok in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        })
        {
            if (!Directory.Exists(kok)) continue;
            IEnumerable<string> lnkler;
            try { lnkler = Directory.EnumerateFiles(kok, "*.lnk", SearchOption.AllDirectories); } catch { continue; }
            foreach (var lnk in lnkler)
            {
                string ad = Path.GetFileNameWithoutExtension(lnk);
                if (AdGereksizMi(ad)) continue;
                string? hedef = KisayolHedefi(lnk);
                if (hedef == null) continue;
                if (!hedef.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(hedef)) continue;
                if (HedefGereksizMi(hedef)) continue;
                if (!sonuc.ContainsKey(ad)) sonuc[ad] = new KuruluUygulama(ad, hedef, false);
            }
        }

        // 2) Paketli uygulamalar: shell:AppsFolder (Shell COM); Path'i AUMID olanlar
        try
        {
            var tur = Type.GetTypeFromProgID("Shell.Application");
            if (tur != null)
            {
                dynamic kabuk = Activator.CreateInstance(tur)!;
                dynamic klasor = kabuk.NameSpace("shell:AppsFolder");
                dynamic ogeler = klasor.Items();
                foreach (dynamic o in ogeler)
                {
                    string ad = (string)o.Name;
                    string yol = (string)o.Path;
                    if (string.IsNullOrEmpty(ad) || string.IsNullOrEmpty(yol) || !yol.Contains('!')) continue;
                    if (AdGereksizMi(ad)) continue;
                    if (!sonuc.ContainsKey(ad)) sonuc[ad] = new KuruluUygulama(ad, "shell:AppsFolder\\" + yol, true);
                }
            }
        }
        catch { }

        return sonuc.Values.OrderBy(u => u.Ad, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true)).ToList();
    }

    private static bool AdGereksizMi(string ad)
    {
        string a = ad.ToLowerInvariant();
        return a.Contains("uninstall") || a.Contains("kaldır") || a.Contains("readme") || a.Contains("help") || a.Contains("yardım")
            || a.Contains("documentation") || a.Contains("license") || a.Contains("website") || a.Contains("web sitesi");
    }

    private static bool HedefGereksizMi(string hedef)
    {
        string h = hedef.ToLowerInvariant();
        return h.Contains("\\uninstall") || h.Contains("unins0") || h.Contains("\\windows\\system32\\") && !h.EndsWith("mspaint.exe") && !h.EndsWith("notepad.exe") && !h.EndsWith("calc.exe");
    }

    private static string? KisayolHedefi(string lnk)
    {
        try
        {
            var tur = Type.GetTypeFromProgID("WScript.Shell");
            if (tur == null) return null;
            dynamic kabuk = Activator.CreateInstance(tur)!;
            dynamic k = kabuk.CreateShortcut(lnk);
            string hedef = (string)k.TargetPath;
            return string.IsNullOrWhiteSpace(hedef) ? null : hedef;
        }
        catch { return null; }
    }
}
