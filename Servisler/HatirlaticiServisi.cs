using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DinamikAda.Servisler;

/// Hatırlatıcılar: "hatırlat: [zaman] metin" ile kurulur, zamanı gelince duyurulur. Kalıcı (json).
public sealed class HatirlaticiServisi
{
    public sealed record Hatirlatici(string Id, long ZamanMs, string Metin);

    private readonly List<Hatirlatici> _liste = new();
    private static string Dosya => Path.Combine(Ayarlar.Klasor, "hatirlatici.json");

    public int Sayi => _liste.Count;

    public void Yukle()
    {
        try { if (File.Exists(Dosya)) { var l = JsonSerializer.Deserialize<List<Hatirlatici>>(File.ReadAllText(Dosya)); if (l != null) { _liste.Clear(); _liste.AddRange(l); } } }
        catch { }
    }

    private void Kaydet()
    {
        try { Directory.CreateDirectory(Ayarlar.Klasor); File.WriteAllText(Dosya, JsonSerializer.Serialize(_liste)); } catch { }
    }

    /// (zaman, kalan metin) ayrıştır ve ekle. Dönüş: kurulan zaman ve metin, ya da null (zaman anlaşılmadı).
    public (DateTime Zaman, string Metin)? Ekle(string girdi)
    {
        var (zaman, metin) = ZamanAyristir(girdi);
        if (zaman == null) return null;
        _liste.Add(new Hatirlatici(Guid.NewGuid().ToString("N")[..8], new DateTimeOffset(zaman.Value).ToUnixTimeMilliseconds(), metin));
        Kaydet();
        return (zaman.Value, metin);
    }

    /// Zamanı gelen hatırlatıcıları döndür ve listeden çıkar
    public List<Hatirlatici> Gecenler()
    {
        long simdi = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var gecen = _liste.Where(h => h.ZamanMs <= simdi).ToList();
        if (gecen.Count > 0) { _liste.RemoveAll(h => h.ZamanMs <= simdi); Kaydet(); }
        return gecen;
    }

    /// "yarın 10:00 toplantı", "15 dakika sonra çay", "14:30 ara" → (DateTime, "toplantı")
    public static (DateTime? Zaman, string Metin) ZamanAyristir(string g)
    {
        string s = g.Trim();
        var now = DateTime.Now;

        // N dakika/saat sonra
        var m = Regex.Match(s, @"^(\d{1,4})\s*(dakika|dakka|saat|dk|sa)\b\s*(sonra)?\s*", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            int n = int.Parse(m.Groups[1].Value);
            bool saat = m.Groups[2].Value.StartsWith("sa", StringComparison.OrdinalIgnoreCase);
            return (now.AddMinutes(saat ? n * 60 : n), s[m.Length..].Trim());
        }

        // yarın / bugün + HH:MM
        bool yarin = false;
        var y = Regex.Match(s, @"^(yarın|yarin)\s+", RegexOptions.IgnoreCase);
        if (y.Success) { yarin = true; s = s[y.Length..]; }
        else { var bg = Regex.Match(s, @"^bugün\s+|^bugun\s+", RegexOptions.IgnoreCase); if (bg.Success) s = s[bg.Length..]; }

        var t = Regex.Match(s, @"^(\d{1,2})[:.](\d{2})\s*");
        if (t.Success)
        {
            int saat = int.Parse(t.Groups[1].Value), dk = int.Parse(t.Groups[2].Value);
            if (saat < 24 && dk < 60)
            {
                var hedef = now.Date.AddHours(saat).AddMinutes(dk);
                if (yarin) hedef = hedef.AddDays(1);
                else if (hedef <= now) hedef = hedef.AddDays(1);   // bugün geçtiyse yarına
                return (hedef, s[t.Length..].Trim());
            }
        }

        // sadece "yarın metin" → yarın sabah 9
        if (yarin) return (now.Date.AddDays(1).AddHours(9), s.Trim());

        return (null, g);
    }

    public static string Bicimle(DateTime z)
    {
        var fark = z - DateTime.Now;
        if (fark.TotalMinutes < 1) return "birazdan";
        if (z.Date == DateTime.Today) return z.ToString("HH:mm");
        if (z.Date == DateTime.Today.AddDays(1)) return "yarın " + z.ToString("HH:mm");
        return z.ToString("d MMM HH:mm", new System.Globalization.CultureInfo("tr-TR"));
    }
}
