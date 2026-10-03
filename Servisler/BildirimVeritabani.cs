using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace DinamikAda.Servisler;

/// Windows bildirim veritabanı (wpndatabase.db): toast'un ham XML'i, dolayısıyla UserNotificationListener'ın
/// vermediği "launch" argümanı burada. WhatsApp bu argümana sohbet kimliğini (numara@s.whatsapp.net) koyuyorsa
/// gönderen adı → numara eşlemesi kullanıcı yazmadan öğrenilir.
public static class BildirimVeritabani
{
    public sealed record HamToast(long Id, string Xml, long Varis);

    private static string Kaynak =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Notifications\wpndatabase.db");

    /// Verilen uygulama kimliğine (AUMID parçası) ait son toast'lar, en yeni başta.
    public static List<HamToast> SonToastlar(string aumidParcasi, int adet = 5)
    {
        var sonuc = new List<HamToast>();
        string tmp = Path.Combine(Path.GetTempPath(), $"dinamikada-wpn-{Environment.ProcessId}.db");
        try
        {
            if (!File.Exists(Kaynak)) return sonuc;
            File.Copy(Kaynak, tmp, true);
            foreach (var ek in new[] { "-wal", "-shm" })
                if (File.Exists(Kaynak + ek)) File.Copy(Kaynak + ek, tmp + ek, true);

            using var baglanti = new SqliteConnection($"Data Source={tmp};Mode=ReadOnly");
            baglanti.Open();
            using var komut = baglanti.CreateCommand();
            komut.CommandText = @"
                select n.Id, n.Payload, n.ArrivalTime
                from Notification n join NotificationHandler h on h.RecordId = n.HandlerId
                where n.Type = 'toast' and h.PrimaryId like $p
                order by n.ArrivalTime desc limit $n";
            komut.Parameters.AddWithValue("$p", "%" + aumidParcasi + "%");
            komut.Parameters.AddWithValue("$n", adet);
            using var okuyucu = komut.ExecuteReader();
            while (okuyucu.Read())
            {
                string xml = okuyucu[1] is byte[] b ? System.Text.Encoding.UTF8.GetString(b) : okuyucu[1]?.ToString() ?? "";
                sonuc.Add(new HamToast(okuyucu.GetInt64(0), xml, okuyucu.GetInt64(2)));
            }
        }
        catch { }
        finally
        {
            foreach (var ek in new[] { "", "-wal", "-shm" })
                try { File.Delete(tmp + ek); } catch { }
        }
        return sonuc;
    }

    /// Toast XML'inden launch argümanı
    public static string? LaunchArgumani(string xml)
    {
        var m = Regex.Match(xml, "launch=\"([^\"]*)\"");
        return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    /// Metinde WhatsApp numarası: 10–15 rakam, çoğunlukla "@s.whatsapp.net" ya da "@c.us" öncesinde
    public static string? NumaraBul(string? metin)
    {
        if (string.IsNullOrEmpty(metin)) return null;
        // Yalnız kesin kalıplar: JID (numara@s.whatsapp.net / @c.us) ya da phone=/jid= anahtarı.
        // Rastgele uzun rakam dizileri (bildirim kimliği, zaman damgası) telefon sanılmasın.
        var m = Regex.Match(metin, @"(?<!\d)(\d{10,15})@(s\.whatsapp\.net|c\.us)");
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(metin, @"(?:phone|jid|number)=\+?(\d{10,15})", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(metin, @"\+(\d{10,15})(?!\d)");   // açıkça + ile yazılmış uluslararası numara
        return m.Success ? m.Groups[1].Value : null;
    }

    /// Toast metinleri (başlık, gövde)
    public static List<string> Metinler(string xml) =>
        Regex.Matches(xml, "<text[^>]*>([^<]*)</text>").Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim()).ToList();
}
