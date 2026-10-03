using System.Globalization;
using System.IO;
using System.Text;

namespace DinamikAda.Servisler;

/// vCard (.vcf) rehberi: ad → numara. vCard 2.1 (QUOTED-PRINTABLE, satır katlama) ve 3.0/4.0 okunur.
public sealed class RehberServisi
{
    private readonly List<(string Ad, string AdSade, string Numara)> _kisiler = new();

    public int Sayi => _kisiler.Count;
    public static string VarsayilanYol => Path.Combine(Ayarlar.Klasor, "rehber.vcf");

    public int Yukle(string yol)
    {
        _kisiler.Clear();
        if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol)) return 0;
        string metin;
        try { metin = File.ReadAllText(yol, Encoding.UTF8); } catch { return 0; }

        // Satır katlama: sonraki satır boşluk/sekme ile başlıyorsa öncekinin devamı
        var satirlar = new List<string>();
        foreach (var ham in metin.Replace("\r\n", "\n").Split('\n'))
        {
            if (ham.Length > 0 && (ham[0] == ' ' || ham[0] == '\t') && satirlar.Count > 0) satirlar[^1] += ham[1..];
            else satirlar.Add(ham);
        }

        string? ad = null;
        var teller = new List<string>();
        foreach (var satir in satirlar)
        {
            if (satir.StartsWith("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase)) { ad = null; teller.Clear(); continue; }
            if (satir.StartsWith("END:VCARD", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(ad))
                    foreach (var t in teller.Distinct())
                        _kisiler.Add((ad.Trim(), Sadelestir(ad), t));
                continue;
            }
            int iki = satir.IndexOf(':');
            if (iki <= 0) continue;
            string anahtar = satir[..iki], deger = satir[(iki + 1)..];
            string tur = anahtar.Split(';')[0].ToUpperInvariant();
            if (anahtar.Contains("QUOTED-PRINTABLE", StringComparison.OrdinalIgnoreCase)) deger = QpCoz(deger);

            if (tur == "FN") ad = deger;
            else if (tur == "N" && string.IsNullOrWhiteSpace(ad))
            {
                var p = deger.Split(';');
                ad = string.Join(" ", new[] { p.Length > 1 ? p[1] : "", p.Length > 0 ? p[0] : "" }.Where(s => s.Length > 0));
            }
            else if (tur == "TEL")
            {
                var n = Normallestir(deger);
                if (n != null) teller.Add(n);
            }
        }
        return _kisiler.Count;
    }

    /// Teşhis: tüm kayıtlar "ad|sade|numara" biçiminde
    public string Dok() => string.Join(" ; ", _kisiler.Select(k => $"{k.Ad}|{k.AdSade}|{k.Numara}"));

    /// Bildirimdeki gönderen adına göre numara: önce birebir, sonra sadeleştirilmiş eşitlik, sonra içerme.
    public string? Bul(string ad)
    {
        if (string.IsNullOrWhiteSpace(ad) || _kisiler.Count == 0) return null;
        string tam = ad.Trim();
        var k = _kisiler.FirstOrDefault(x => x.Ad == tam);
        if (k.Numara != null) return k.Numara;
        string sade = Sadelestir(ad);
        if (sade.Length == 0) return null;
        k = _kisiler.FirstOrDefault(x => x.AdSade == sade);
        if (k.Numara != null) return k.Numara;
        var adaylar = _kisiler.Where(x => x.AdSade.Length >= 3 && (x.AdSade.Contains(sade) || sade.Contains(x.AdSade))).Select(x => x.Numara).Distinct().ToList();
        return adaylar.Count == 1 ? adaylar[0] : null;   // birden çok aday varsa tahmin etme
    }

    /// Emoji, noktalama ve aksan kaldırılmış, küçük harf, tek boşluk
    public static string Sadelestir(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            var kat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (kat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else if (char.IsWhiteSpace(ch)) sb.Append(' ');
        }
        return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// "0532 123 45 67" → 905321234567 ; "+90 (532) ..." → 905321234567 ; yabancı numaralar olduğu gibi (ülke koduyla)
    public static string? Normallestir(string tel)
    {
        bool arti = tel.TrimStart().StartsWith('+');
        string d = new string(tel.Where(char.IsDigit).ToArray());
        if (d.Length < 10) return null;
        if (arti) return d.StartsWith("00") ? d[2..] : d;
        if (d.StartsWith("00")) return d[2..];
        if (d.Length == 11 && d.StartsWith('0')) return "9" + d;         // 05xx… → 905xx…
        if (d.Length == 10 && d.StartsWith('5')) return "90" + d;        // 5xx… → 905xx…
        return d;                                                         // 90… ya da başka ülke
    }

    private static string QpCoz(string s)
    {
        var bayt = new List<byte>();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '=' && i + 2 < s.Length && Uri.IsHexDigit(s[i + 1]) && Uri.IsHexDigit(s[i + 2]))
            { bayt.Add(Convert.ToByte(s.Substring(i + 1, 2), 16)); i += 2; }
            else if (s[i] == '=' && i + 1 >= s.Length) { }
            else bayt.AddRange(Encoding.UTF8.GetBytes(s[i].ToString()));
        }
        return Encoding.UTF8.GetString(bayt.ToArray());
    }
}
