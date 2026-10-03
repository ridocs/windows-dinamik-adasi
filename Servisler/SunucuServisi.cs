using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace DinamikAda.Servisler;

/// Uzak sunucu sağlığı: ping, web uçları (HTTP durum kodu), SSH ile yük/disk/bellek. 60 sn'de bir.
public sealed class SunucuServisi
{
    public sealed record Durum(bool Ayakta, int PingMs, string WebOzet, bool WebSorun, double Yuk, int DiskYuzde, int RamYuzde, string Uptime, string Hata);

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) };

    public Durum? Son { get; private set; }
    public DateTime SonZaman { get; private set; } = DateTime.MinValue;

    public async Task<Durum> OlcAsync(string adres, string kullanici, string anahtar, string urlSatirlari)
    {
        int pingMs = -1; bool ayakta = false;
        try
        {
            using var p = new Ping();
            var r = await p.SendPingAsync(adres, 3000);
            if (r.Status == IPStatus.Success) { ayakta = true; pingMs = (int)r.RoundtripTime; }
        }
        catch { }

        // Web uçları: 2xx/3xx ve 400/401/403 "cevap veriyor" sayılır (mTLS paneli sertifikasız 400 döner); zaman aşımı/5xx sorun
        var parcalar = new List<string>(); bool webSorun = false;
        foreach (var u in urlSatirlari.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            string ad = u.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            if (ad.Length > 28) ad = ad[..28] + "…";
            try
            {
                using var ist = new HttpRequestMessage(HttpMethod.Head, u);
                using var yanit = await Http.SendAsync(ist);
                int kod = (int)yanit.StatusCode;
                bool iyi = kod < 500;
                if (!iyi) webSorun = true;
                parcalar.Add($"{ad} {kod}");
            }
            catch { webSorun = true; parcalar.Add($"{ad} yok"); }
        }

        double yuk = -1; int disk = -1, ram = -1; string uptime = "", hata = "";
        if (ayakta)
        {
            string key = string.IsNullOrWhiteSpace(anahtar) ? "" : Environment.ExpandEnvironmentVariables(anahtar);
            if (key.Length == 0 || File.Exists(key))
            {
                var (kod, cikti, err) = await SshAsync(key, $"{kullanici}@{adres}",
                    "cut -d' ' -f1 /proc/loadavg; df -P / | awk 'NR==2{print $5}'; free | awk '/Mem:/{print int($3*100/$2)}'; uptime -p"); // iç çift tırnak yok: Windows argüman ayrıştırması bozuyordu
                if (kod == 0)
                {
                    var s = cikti.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
                    if (s.Length > 0) double.TryParse(s[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out yuk);
                    if (s.Length > 1) int.TryParse(s[1].TrimEnd('%'), out disk);
                    if (s.Length > 2) int.TryParse(s[2], out ram);
                    if (s.Length > 3) uptime = s[3].Replace("up ", "");
                }
                else hata = err.Split('\n').FirstOrDefault(x => x.Trim().Length > 0)?.Trim() ?? "ssh hatası";
            }
            else hata = "SSH anahtarı yok";
        }

        Son = new Durum(ayakta, pingMs, string.Join(" · ", parcalar), webSorun, yuk, disk, ram, uptime, hata);
        SonZaman = DateTime.Now;
        return Son;
    }

    private static async Task<(int Kod, string Cikti, string Hata)> SshAsync(string anahtar, string hedef, string komut)
    {
        try
        {
            string anahtarArg = anahtar.Length > 0 ? $"-i \"{anahtar}\" " : "";   // boşsa ssh varsayılan anahtarları / config
            var psi = new ProcessStartInfo("ssh", $"{anahtarArg}-o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=8 {hedef} \"{komut}\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            if (p == null) return (-1, "", "ssh başlatılamadı");
            var cikti = p.StandardOutput.ReadToEndAsync();
            var hata = p.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try { await p.WaitForExitAsync(cts.Token); } catch { try { p.Kill(); } catch { } return (-1, "", "ssh zaman aşımı"); }
            return (p.ExitCode, await cikti, await hata);
        }
        catch (Exception e) { return (-1, "", e.Message); }
    }
}
