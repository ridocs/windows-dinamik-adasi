using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;

namespace DinamikAda.Servisler;

/// Windows Canlı Altyazı (LiveCaptions) penceresini UIA ile okur, metni MyMemory ile Türkçeye çevirir.
/// FindAll UIA ağacında E_UNEXPECTED verdiğinden TreeWalker ile adım adım gezilir.
public sealed class AltyaziServisi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    // Hoş geldiniz / ayar ekranındaki sabit metinler: altyazı değil, süz
    private static readonly string[] Statik =
    {
        "hoş geldiniz", "gerçek zamanlı", "evet, devam et", "gizlilik", "daha fazla bilgi",
        "ses verilerinizi", "alt yazı dili", "mikrofon", "canlı alt yaz", "welcome", "privacy", "learn more",
        "hayır, teşekkürler", "teşekkürler", "evet", "no thanks", "thanks", "settings", "ayarlar", "position", "konum",
    };

    public string Orijinal { get; private set; } = "";
    public string Turkce { get; private set; } = "";
    public bool Aktif { get; private set; }   // LiveCaptions penceresi açık

    private string _sonCevrilen = "";
    private readonly Dictionary<string, string> _onbellek = new();
    private bool _ceviriyor;

    public bool PencereVar() => Pencere() != IntPtr.Zero;

    /// Altyazı metnini oku; değiştiyse çeviriyi tetikle. Dönüş: metin değişti mi.
    public bool Oku()
    {
        var h = Pencere();
        Aktif = h != IntPtr.Zero;
        if (!Aktif) { Orijinal = ""; Turkce = ""; return false; }
        string metin = MetinOku(h);
        if (metin.Length == 0 || metin == Orijinal) return false;
        Orijinal = metin;
        return true;
    }

    public async Task CevirAsync()
    {
        string kaynak = Orijinal;
        if (kaynak.Length == 0 || kaynak == _sonCevrilen || _ceviriyor) return;
        _ceviriyor = true;
        try
        {
            // Yalnız son cümleyi çevir (altyazı birikir); son ~180 karakter
            string parca = kaynak.Length > 180 ? kaynak[^180..] : kaynak;
            if (_onbellek.TryGetValue(parca, out var hazir)) { Turkce = hazir; _sonCevrilen = kaynak; return; }
            string url = "https://api.mymemory.translated.net/get?langpair=en|tr&q=" + Uri.EscapeDataString(parca);
            using var r = await Http.GetAsync(url);
            if (!r.IsSuccessStatusCode) return;
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("responseData", out var rd) && rd.TryGetProperty("translatedText", out var tt))
            {
                string ceviri = tt.GetString() ?? "";
                if (ceviri.Length > 0 && !ceviri.StartsWith("PLEASE SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    Turkce = ceviri;
                    _onbellek[parca] = ceviri;
                    if (_onbellek.Count > 200) _onbellek.Clear();
                }
            }
            _sonCevrilen = kaynak;
        }
        catch { }
        finally { _ceviriyor = false; }
    }

    // ---- UIA okuma ----
    private static string MetinOku(IntPtr h)
    {
        try
        {
            var kök = AutomationElement.FromHandle(h);
            if (kök == null) return "";
            var walker = TreeWalker.RawViewWalker;
            var parçalar = new List<string>();
            Gez(walker, kök, parçalar, 0);
            // Statik UI metinlerini süz, kalanı birleştir (altyazı en uzun blok)
            var süzülü = parçalar.Where(p => p.Length >= 14 && p.Contains(' ') && !Statik.Any(s => p.Contains(s, StringComparison.OrdinalIgnoreCase))).ToList();
            if (süzülü.Count == 0) return "";
            // En uzun parça altyazı metnidir (cümleler birikir)
            return süzülü.OrderByDescending(p => p.Length).First().Trim();
        }
        catch { return ""; }
    }

    private static void Gez(TreeWalker w, AutomationElement el, List<string> çıktı, int derinlik)
    {
        if (derinlik > 25) return;
        var çocuk = w.GetFirstChild(el);
        while (çocuk != null)
        {
            try
            {
                string ad = çocuk.Current.Name ?? "";
                var tip = çocuk.Current.ControlType;
                if ((tip == ControlType.Text || tip == ControlType.Document || tip == ControlType.Edit) && ad.Length > 0)
                    çıktı.Add(ad);
            }
            catch { }
            Gez(w, çocuk, çıktı, derinlik + 1);
            try { çocuk = w.GetNextSibling(çocuk); } catch { break; }
        }
    }

    private static IntPtr Pencere() => FindWindow("LiveCaptionsDesktopWindow", null);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? sinif, string? baslik);
}
