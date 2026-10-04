using System.IO;

namespace DinamikAda.Servisler;

/// Claude Code oturumlarını izler (ayar dosyasına dokunmadan): %USERPROFILE%\.claude\projects\*.jsonl
/// son değişiklik zamanından durum çıkarır. Oturum çalışıp durunca "seni bekliyor" sinyali verir.
public sealed class ClaudeKodServisi
{
    private static string Klasor => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    public bool Var => Directory.Exists(Klasor);
    public string Durum { get; private set; } = "boşta";
    public int AktifOturum { get; private set; }

    private bool _calisiyordu, _bekliyorBildirildi;

    /// Birkaç saniyede bir çağrılır. Dönüş: "oturum durdu, bekliyor" olayı tetiklendi mi.
    public bool Tik()
    {
        var (enYeni, aktif) = Tara();
        AktifOturum = aktif;
        if (enYeni == DateTime.MinValue) { Durum = "yok"; return false; }
        double sn = (DateTime.Now - enYeni).TotalSeconds;
        if (sn < 10)
        {
            _calisiyordu = true; _bekliyorBildirildi = false;
            Durum = "çalışıyor";
            return false;
        }
        if (_calisiyordu && !_bekliyorBildirildi && sn >= 25 && sn < 900)
        {
            _bekliyorBildirildi = true;
            Durum = "bekliyor";
            return true;   // iş durdu: bitti ya da onay/girdi bekliyor
        }
        if (sn >= 900) { _calisiyordu = false; Durum = "boşta"; }
        else if (!_calisiyordu) Durum = sn < 120 ? "az önce" : "boşta";
        return false;
    }

    private (DateTime EnYeni, int Aktif) Tara()
    {
        try
        {
            var dosyalar = new DirectoryInfo(Klasor).GetFiles("*.jsonl", SearchOption.AllDirectories);
            if (dosyalar.Length == 0) return (DateTime.MinValue, 0);
            var enYeni = dosyalar.Max(f => f.LastWriteTime);
            int aktif = dosyalar.Count(f => (DateTime.Now - f.LastWriteTime).TotalMinutes < 5);
            return (enYeni, aktif);
        }
        catch { return (DateTime.MinValue, 0); }
    }
}
