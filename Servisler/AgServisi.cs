using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DinamikAda.Servisler;

/// Ağ hızı (tüm fiziksel bağdaştırıcıların toplamı, bayt/sn) ve Tailscale VPN durumu.
/// Tik() saniyede bir çağrılır; VPN kontrolü 5 sn'de bir yapılır.
public sealed class AgServisi
{
    public double IndirmeBs { get; private set; }   // bayt/sn
    public double YuklemeBs { get; private set; }
    public bool VpnKurulu { get; private set; }     // Tailscale kurulu (CLI ya da bağdaştırıcı)
    public bool VpnBagli { get; private set; }      // tailscale status: Running + çevrimiçi + 100.x adres
    public bool VpnBaglaniyor { get; private set; } // başlıyor / koordinasyon sunucusuna ulaşamıyor / giriş gerekli
    public string VpnIp { get; private set; } = "";
    public string VpnAd { get; private set; } = "";
    public string VpnMesaj { get; private set; } = "";   // Tailscale'in sağlık mesajı ya da durum adı

    private long _oncekiAl = -1, _oncekiGonder = -1;
    private readonly Stopwatch _sayac = Stopwatch.StartNew();
    private int _tik;
    private static readonly string? TailscaleExe = new[] { @"C:\Program Files\Tailscale\tailscale.exe", @"C:\Program Files (x86)\Tailscale\tailscale.exe" }.FirstOrDefault(System.IO.File.Exists);
    private bool _tsCalisiyor;

    /// `tailscale status --json`: bağdaştırıcıdan daha doğru (adaptör Up görünse de 100.x adres olmayabilir)
    private async Task TailscaleOlcAsync()
    {
        if (TailscaleExe == null || _tsCalisiyor) return;
        _tsCalisiyor = true;
        try
        {
            var psi = new ProcessStartInfo(TailscaleExe, "status --json") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi);
            if (p == null) return;
            string cikti = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            if (cikti.TrimStart().StartsWith("{"))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(cikti);
                var kok = doc.RootElement;
                string durum = kok.TryGetProperty("BackendState", out var bs) ? bs.GetString() ?? "" : "";
                bool online = kok.TryGetProperty("Self", out var self) && self.TryGetProperty("Online", out var on) && on.ValueKind == System.Text.Json.JsonValueKind.True;
                string ip = "";
                if (kok.TryGetProperty("Self", out self) && self.TryGetProperty("TailscaleIPs", out var ips) && ips.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var a in ips.EnumerateArray()) { var s = a.GetString() ?? ""; if (s.StartsWith("100.")) { ip = s; break; } }
                string saglik = kok.TryGetProperty("Health", out var h) && h.ValueKind == System.Text.Json.JsonValueKind.Array && h.GetArrayLength() > 0 ? h[0].GetString() ?? "" : "";
                VpnKurulu = true;
                VpnBagli = durum == "Running" && online && ip.Length > 0;
                VpnBaglaniyor = !VpnBagli && durum != "Stopped" && (durum == "Starting" || durum == "NeedsLogin" || durum == "NeedsMachineAuth" || durum == "Running" || saglik.Length > 0);
                VpnIp = ip;
                VpnAd = kok.TryGetProperty("Self", out self) && self.TryGetProperty("HostName", out var hn) ? hn.GetString() ?? "" : "";
                VpnMesaj = VpnBagli ? "" : saglik.Length > 0 ? Turkcelestir(saglik) : durum switch
                {
                    "Stopped" => "Tailscale kapalı",
                    "NeedsLogin" => "Tailscale girişi gerekli",
                    "NeedsMachineAuth" => "cihaz onayı bekliyor",
                    "Starting" => "Tailscale başlıyor",
                    "NoState" => "Tailscale başlıyor",
                    _ => durum,
                };
            }
        }
        catch { }
        finally { _tsCalisiyor = false; }
    }

    private static string Turkcelestir(string saglik)
    {
        if (saglik.Contains("coordination server", StringComparison.OrdinalIgnoreCase)) return "koordinasyon sunucusuna ulaşılamıyor";
        if (saglik.Contains("starting", StringComparison.OrdinalIgnoreCase)) return "Tailscale başlıyor";
        if (saglik.Contains("not logged in", StringComparison.OrdinalIgnoreCase) || saglik.Contains("Log in", StringComparison.OrdinalIgnoreCase)) return "Tailscale girişi gerekli";
        if (saglik.Contains("DERP", StringComparison.OrdinalIgnoreCase)) return "aktarma sunucusuna bağlanılamıyor";
        return saglik.Length > 60 ? saglik[..60] + "…" : saglik;
    }

    public void Tik()
    {
        try
        {
            long al = 0, gonder = 0;
            bool vpnVar = false, vpnUp = false; string ip = "", ad = "";
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                string tanim = ni.Description + " " + ni.Name;
                bool tunel = tanim.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)
                          || tanim.Contains("WireGuard", StringComparison.OrdinalIgnoreCase)
                          || tanim.Contains("TAP-", StringComparison.OrdinalIgnoreCase)
                          || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel;
                if (tanim.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) && (_tik % 5 == 0 || !VpnKurulu))
                {
                    vpnVar = true;
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (var a in ni.GetIPProperties().UnicastAddresses)
                            if (a.Address.AddressFamily == AddressFamily.InterNetwork && a.Address.ToString().StartsWith("100."))
                            { vpnUp = true; ip = a.Address.ToString(); ad = ni.Name; break; }
                    }
                }
                if (tunel || ni.OperationalStatus != OperationalStatus.Up) continue;   // VPN trafiği fiziksel kartta zaten sayılır
                var s = ni.GetIPStatistics();
                al += s.BytesReceived; gonder += s.BytesSent;
            }

            double sn = _sayac.Elapsed.TotalSeconds;
            if (_oncekiAl >= 0 && sn > 0.2)
            {
                IndirmeBs = Math.Max(0, (al - _oncekiAl) / sn);
                YuklemeBs = Math.Max(0, (gonder - _oncekiGonder) / sn);
            }
            _oncekiAl = al; _oncekiGonder = gonder; _sayac.Restart();

            if (TailscaleExe != null) { if (_tik % 5 == 0) _ = TailscaleOlcAsync(); }
            else if (_tik % 5 == 0 || !VpnKurulu)
            {
                // CLI yoksa bağdaştırıcıdan tahmin: Up + 100.x adres
                VpnKurulu = vpnVar;
                VpnBagli = vpnUp;
                VpnBaglaniyor = vpnVar && !vpnUp;
                VpnIp = ip; VpnAd = ad;
                VpnMesaj = vpnUp ? "" : "Tailscale bağlı değil";
            }
            _tik++;
        }
        catch { }
    }

    /// 0 B/s → "0 KB/s", 532 000 → "520 KB/s", 2 400 000 → "2,3 MB/s"
    public static string Bicimle(double bs)
    {
        if (bs < 1024 * 1024) return $"{Math.Round(bs / 1024)} KB/s";
        return $"{bs / (1024 * 1024):0.0} MB/s";
    }
}
