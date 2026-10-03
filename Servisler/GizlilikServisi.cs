using Microsoft.Win32;

namespace DinamikAda.Servisler;

/// Mikrofon ve kamerayı şu an hangi uygulamanın kullandığını Windows'un
/// CapabilityAccessManager kayıt defterinden okur (LastUsedTimeStop == 0 → kullanımda).
public sealed class GizlilikServisi
{
    private const string Kok = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";

    public bool MikrofonKullanimda { get; private set; }
    public bool KameraKullanimda { get; private set; }
    public string MikrofonKullanan { get; private set; } = "";
    public string KameraKullanan { get; private set; } = "";

    /// (mikrofon, kamera) durumu değişince
    public event Action<bool, bool>? Degisti;

    public void Tik()
    {
        var (mik, mikAd) = Kullanan("microphone");
        var (kam, kamAd) = Kullanan("webcam");
        bool degisti = mik != MikrofonKullanimda || kam != KameraKullanimda;
        MikrofonKullanimda = mik; KameraKullanimda = kam;
        MikrofonKullanan = mikAd; KameraKullanan = kamAd;
        if (degisti) Degisti?.Invoke(mik, kam);
    }

    private static (bool, string) Kullanan(string yetenek)
    {
        try
        {
            using var kok = Registry.CurrentUser.OpenSubKey(Kok + yetenek);
            if (kok == null) return (false, "");

            foreach (var alt in kok.GetSubKeyNames())
            {
                using var k = kok.OpenSubKey(alt);
                if (k == null) continue;

                if (alt == "NonPackaged")
                {
                    foreach (var exe in k.GetSubKeyNames())
                    {
                        using var e = k.OpenSubKey(exe);
                        if (Aktif(e)) return (true, ExeAdi(exe));
                    }
                }
                else if (Aktif(k))
                {
                    return (true, alt.Split('_')[0]);
                }
            }
        }
        catch { }
        return (false, "");
    }

    private static bool Aktif(RegistryKey? k)
    {
        if (k == null) return false;
        var basla = k.GetValue("LastUsedTimeStart");
        var bitir = k.GetValue("LastUsedTimeStop");
        if (basla == null || bitir == null) return false;
        return Convert.ToInt64(basla) != 0 && Convert.ToInt64(bitir) == 0;
    }

    private static string ExeAdi(string anahtar)
    {
        // "C:#Program Files#Zoom#Zoom.exe" → "Zoom"
        var parcalar = anahtar.Split('#');
        var son = parcalar[^1];
        return son.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? son[..^4] : son;
    }
}
