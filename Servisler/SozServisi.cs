using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DinamikAda.Servisler;

/// Şarkı sözleri: lrclib.net (hesapsız, ücretsiz). Zamanlı (LRC) varsa satır satır eşlenir, yoksa düz metin ilk satırları.
public sealed class SozServisi
{
    private sealed record Satir(TimeSpan Zaman, string Metin);

    private static readonly HttpClient Http = Olustur();
    private static HttpClient Olustur()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        // Başlıklar ASCII olmalı: Türkçe karakter FormatException fırlatır ve tüm istekler sessizce düşer
        h.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "DinamikAda/1.0 (Windows; personal desktop capsule)");
        h.DefaultRequestHeaders.TryAddWithoutValidation("Lrclib-Client", "DinamikAda v1.0");
        return h;
    }

    private string _anahtar = "";
    private List<Satir> _satirlar = new();
    private readonly Dictionary<string, List<Satir>> _onbellek = new();
    private int _sonIndeks = -1;

    public bool Var => _satirlar.Count > 0;
    public bool Zamanli { get; private set; }
    public string Kaynak { get; private set; } = "";
    public string SonHata { get; private set; } = "";

    /// Parça değişince çağır; aynı parça için tekrar istek atmaz. Dönüş: sözler bulundu mu.
    public async Task<bool> YukleAsync(string baslik, string sanatci, TimeSpan sure)
    {
        var (b, s) = Temizle(baslik, sanatci);
        string anahtar = (b + "|" + s).ToLowerInvariant();
        if (anahtar == _anahtar) return Var;
        _anahtar = anahtar; _satirlar = new(); _sonIndeks = -1; Zamanli = false; Kaynak = "";
        if (b.Length == 0) return false;
        if (_onbellek.TryGetValue(anahtar, out var hazir)) { _satirlar = hazir; Zamanli = hazir.Count > 0 && hazir[0].Zaman >= TimeSpan.Zero && hazir.Any(x => x.Zaman > TimeSpan.Zero); return Var; }

        try
        {
            // Sırayla dene: birebir get → track+artist arama → serbest "q" arama → yalnız parça adı.
            // lrclib arada 503 veriyor: her adres bir kez yeniden denenir. Adaylardan sanatçısı tutan ve süresi en yakın olan seçilir.
            var adresler = new List<string>();
            if (s.Length > 0)
            {
                adresler.Add($"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(b)}&artist_name={Uri.EscapeDataString(s)}" + (sure.TotalSeconds > 10 ? $"&duration={(int)sure.TotalSeconds}" : ""));
                adresler.Add($"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(b)}&artist_name={Uri.EscapeDataString(s)}");
                adresler.Add($"https://lrclib.net/api/search?q={Uri.EscapeDataString(s + " " + b)}");
            }
            adresler.Add($"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(b)}");

            var adaylar = new List<JsonElement>();
            foreach (var url in adresler)
            {
                for (int deneme = 0; deneme < 2; deneme++)
                {
                    using var r = await Http.GetAsync(url);
                    if ((int)r.StatusCode >= 500) { await Task.Delay(1200); continue; }
                    if (r.IsSuccessStatusCode)
                    {
                        var kok = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
                        if (kok.ValueKind == JsonValueKind.Array) foreach (var e in kok.EnumerateArray()) adaylar.Add(e.Clone());
                        else if (kok.ValueKind == JsonValueKind.Object) adaylar.Add(kok.Clone());
                    }
                    break;
                }
                if (anahtar != _anahtar) return false;   // bu arada parça değişti
                if (adaylar.Any(a => SozVar(a, "syncedLyrics"))) break;   // zamanlı bulundu, daha fazla arama gereksiz
            }
            var kayit = Sec(adaylar, b, s, sure);
            if (kayit == null) { _onbellek[anahtar] = _satirlar; return false; }

            string? senkron = kayit.Value.TryGetProperty("syncedLyrics", out var sy) && sy.ValueKind == JsonValueKind.String ? sy.GetString() : null;
            string? duz = kayit.Value.TryGetProperty("plainLyrics", out var pl) && pl.ValueKind == JsonValueKind.String ? pl.GetString() : null;
            if (!string.IsNullOrWhiteSpace(senkron)) { _satirlar = LrcAyristir(senkron); Zamanli = true; }
            else if (!string.IsNullOrWhiteSpace(duz))
                _satirlar = duz.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Select(x => new Satir(TimeSpan.MinValue, x)).ToList();
            Kaynak = kayit.Value.TryGetProperty("trackName", out var tn) ? $"{tn.GetString()} · {(kayit.Value.TryGetProperty("artistName", out var an) ? an.GetString() : "")}" : "lrclib";
            _onbellek[anahtar] = _satirlar;
        }
        catch (Exception e) { SonHata = e.Message; }
        return Var;
    }

    /// Konuma düşen satır. Zamanlı değilse konumla orantılı satır. Satır değişmediyse null.
    public string? SatirAl(TimeSpan konum, TimeSpan sure)
    {
        if (_satirlar.Count == 0) return null;
        int i;
        if (Zamanli)
        {
            i = -1;
            var hedef = konum + TimeSpan.FromMilliseconds(250);
            for (int k = 0; k < _satirlar.Count && _satirlar[k].Zaman <= hedef; k++) i = k;
        }
        else i = sure.TotalSeconds > 0 ? Math.Clamp((int)(konum.TotalSeconds / sure.TotalSeconds * _satirlar.Count), 0, _satirlar.Count - 1) : 0;
        if (i == _sonIndeks) return null;
        _sonIndeks = i;
        if (i < 0) return "♪";
        string m = _satirlar[i].Metin.Trim();
        return m.Length == 0 ? "♪" : m;
    }

    public void Sifirla() { _sonIndeks = -1; }

    private static bool SozVar(JsonElement e, string alan) =>
        e.TryGetProperty(alan, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString());

    /// Puan: zamanlı sözler +100, sanatçı tutuyor +50, parça adı tutuyor +30, süre farkı her saniye -1 (en çok -40)
    private static JsonElement? Sec(List<JsonElement> adaylar, string baslik, string sanatci, TimeSpan sure)
    {
        JsonElement? enIyi = null; double enIyiPuan = double.MinValue;
        string sb = Sade(baslik), ss = Sade(sanatci);
        foreach (var a in adaylar)
        {
            if (!SozVar(a, "syncedLyrics") && !SozVar(a, "plainLyrics")) continue;
            double puan = SozVar(a, "syncedLyrics") ? 100 : 0;
            string an = a.TryGetProperty("artistName", out var x1) ? Sade(x1.GetString() ?? "") : "";
            string tn = a.TryGetProperty("trackName", out var x2) ? Sade(x2.GetString() ?? "") : "";
            if (ss.Length > 0 && (an.Contains(ss) || ss.Contains(an)) && an.Length > 0) puan += 50;
            if (sb.Length > 0 && (tn == sb || tn.Contains(sb) || sb.Contains(tn)) && tn.Length > 0) puan += 30;
            if (sure.TotalSeconds > 10 && a.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number)
                puan -= Math.Min(40, Math.Abs(d.GetDouble() - sure.TotalSeconds));
            if (puan > enIyiPuan) { enIyiPuan = puan; enIyi = a; }
        }
        return enIyi;
    }

    private static string Sade(string s) =>
        new string(s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD).Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();

    private static List<Satir> LrcAyristir(string lrc)
    {
        var liste = new List<Satir>();
        var rx = new Regex(@"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]");
        foreach (var ham in lrc.Split('\n'))
        {
            var zamanlar = rx.Matches(ham);
            if (zamanlar.Count == 0) continue;
            string metin = rx.Replace(ham, "").Trim();
            foreach (Match z in zamanlar)
            {
                int dk = int.Parse(z.Groups[1].Value), sn = int.Parse(z.Groups[2].Value);
                string kesir = z.Groups[3].Success ? z.Groups[3].Value : "0";
                int ms = kesir.Length == 1 ? int.Parse(kesir) * 100 : kesir.Length == 2 ? int.Parse(kesir) * 10 : int.Parse(kesir);
                liste.Add(new Satir(new TimeSpan(0, 0, dk, sn, ms), metin));
            }
        }
        return liste.OrderBy(x => x.Zaman).ToList();
    }

    /// "Şarkı (Official Video) - YouTube" → "Şarkı"; sanatçı boşsa "Sanatçı - Şarkı" bölünür.
    private static (string Baslik, string Sanatci) Temizle(string baslik, string sanatci)
    {
        string b = baslik ?? "", s = sanatci ?? "";
        b = Regex.Replace(b, @"\s*[-–|]\s*YouTube(\s*Music)?\s*$", "", RegexOptions.IgnoreCase);
        b = Regex.Replace(b, @"\s*[\(\[][^\)\]]*(official|video|lyric|audio|visualizer|hd|4k|remaster|klip|canlı|live)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
        b = Regex.Replace(b, @"\s*(ft\.|feat\.)\s.*$", "", RegexOptions.IgnoreCase).Trim();
        if (s.Length == 0 || s.EndsWith("- Topic", StringComparison.OrdinalIgnoreCase) || s.Contains("VEVO", StringComparison.OrdinalIgnoreCase))
        {
            var parca = b.Split(new[] { " - ", " – " }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parca.Length == 2) { s = parca[0].Trim(); b = parca[1].Trim(); }
            else if (s.EndsWith("- Topic", StringComparison.OrdinalIgnoreCase)) s = s[..^7].Trim();
        }
        s = Regex.Replace(s, @"\s*(ft\.|feat\.|,|&).*$", "", RegexOptions.IgnoreCase).Trim();
        return (b, s);
    }
}
