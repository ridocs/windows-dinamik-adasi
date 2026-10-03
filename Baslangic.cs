using System.IO;

namespace DinamikAda;

/// Başlangıç klasöründeki kısayolu yönetir (yönetici izni gerekmez).
public static class Baslangic
{
    private static string KisayolYolu =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Dinamik Ada.lnk");

    public static bool Acik => File.Exists(KisayolYolu);

    public static void Ayarla(bool acik)
    {
        try
        {
            if (!acik) { if (File.Exists(KisayolYolu)) File.Delete(KisayolYolu); return; }

            string exe = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exe)) return;

            // WScript.Shell COM: ek paket gerekmez
            var tur = Type.GetTypeFromProgID("WScript.Shell");
            if (tur == null) return;
            dynamic kabuk = Activator.CreateInstance(tur)!;
            dynamic kisayol = kabuk.CreateShortcut(KisayolYolu);
            kisayol.TargetPath = exe;
            kisayol.WorkingDirectory = Path.GetDirectoryName(exe);
            kisayol.Description = "Dinamik Ada: ekran üstü medya kapsülü";
            kisayol.Save();
        }
        catch { /* kısayol yazılamazsa sessizce geç; ayar penceresi mevcut durumu gösterir */ }
    }
}
