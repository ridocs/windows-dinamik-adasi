using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DinamikAda.Servisler;

/// Geliştirici için hızlı metin dönüşümleri. Hepsi saf .NET, harici bağımlılık yok.
/// Her işlem (sonuç, başarılı mı) döndürür; başarısızsa sonuç bir açıklama metnidir.
public static class GelistiriciAraclari
{
    public static (string Sonuc, bool Ok) Base64Kodla(string g)
    {
        try { return (Convert.ToBase64String(Encoding.UTF8.GetBytes(g)), true); }
        catch (Exception e) { return (e.Message, false); }
    }

    public static (string Sonuc, bool Ok) Base64Coz(string g)
    {
        try
        {
            string t = g.Trim();
            int pay = t.Length % 4;
            if (pay > 0) t = t.PadRight(t.Length + (4 - pay), '=');   // eksik dolguyu tamamla
            return (Encoding.UTF8.GetString(Convert.FromBase64String(t)), true);
        }
        catch { return ("Geçerli base64 değil.", false); }
    }

    public static (string Sonuc, bool Ok) UrlKodla(string g)
    {
        try { return (Uri.EscapeDataString(g), true); }
        catch (Exception e) { return (e.Message, false); }
    }

    public static (string Sonuc, bool Ok) UrlCoz(string g)
    {
        try { return (Uri.UnescapeDataString(g), true); }
        catch (Exception e) { return (e.Message, false); }
    }

    public static (string Sonuc, bool Ok) JsonDuzenle(string g)
    {
        try
        {
            using var belge = JsonDocument.Parse(g);
            return (JsonSerializer.Serialize(belge, new JsonSerializerOptions { WriteIndented = true }), true);
        }
        catch (JsonException e) { return ("Geçerli JSON değil: " + e.Message, false); }
    }

    public static (string Sonuc, bool Ok) Sha256(string g)
    {
        try { return (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(g))).ToLowerInvariant(), true); }
        catch (Exception e) { return (e.Message, false); }
    }

    public static (string Sonuc, bool Ok) Md5(string g)
    {
        try { return (Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(g))).ToLowerInvariant(), true); }
        catch (Exception e) { return (e.Message, false); }
    }

    /// JWT'yi çöz: başlık ve gövdeyi okunur JSON olarak göster (imza doğrulanmaz).
    public static (string Sonuc, bool Ok) JwtCoz(string g)
    {
        try
        {
            var parca = g.Trim().Split('.');
            if (parca.Length < 2) return ("JWT üç bölümlüdür (başlık.gövde.imza).", false);
            string Coz(string b)
            {
                b = b.Replace('-', '+').Replace('_', '/');
                int pay = b.Length % 4;
                if (pay > 0) b = b.PadRight(b.Length + (4 - pay), '=');
                var ham = Encoding.UTF8.GetString(Convert.FromBase64String(b));
                using var belge = JsonDocument.Parse(ham);
                return JsonSerializer.Serialize(belge, new JsonSerializerOptions { WriteIndented = true });
            }
            return ("── Başlık ──\n" + Coz(parca[0]) + "\n\n── Gövde ──\n" + Coz(parca[1]), true);
        }
        catch { return ("Geçerli JWT değil.", false); }
    }

    /// Epoch saniye/milisaniye ise tarihe, tarih ise epoch'a çevir.
    public static (string Sonuc, bool Ok) EpochCevir(string g)
    {
        string t = g.Trim();
        if (long.TryParse(t, out long sayi))
        {
            try
            {
                var dto = t.Length >= 13 ? DateTimeOffset.FromUnixTimeMilliseconds(sayi) : DateTimeOffset.FromUnixTimeSeconds(sayi);
                return ($"Yerel: {dto.LocalDateTime:yyyy-MM-dd HH:mm:ss}\nUTC:   {dto.UtcDateTime:yyyy-MM-dd HH:mm:ss}", true);
            }
            catch { return ("Aralık dışı epoch.", false); }
        }
        if (DateTime.TryParse(t, out var dt))
        {
            var dto = new DateTimeOffset(dt.ToUniversalTime());
            return ($"saniye: {dto.ToUnixTimeSeconds()}\nms:     {dto.ToUnixTimeMilliseconds()}", true);
        }
        return ("Sayı (epoch) ya da tarih girin.", false);
    }

    public static (string Sonuc, bool Ok) UuidUret() => (Guid.NewGuid().ToString(), true);

    /// Satır, kelime, karakter sayısı.
    public static (string Sonuc, bool Ok) Say(string g)
    {
        int satir = g.Length == 0 ? 0 : g.Replace("\r\n", "\n").Split('\n').Length;
        int kelime = g.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return ($"satır: {satir}\nkelime: {kelime}\nkarakter: {g.Length}", true);
    }
}
