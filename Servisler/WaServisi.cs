using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace DinamikAda.Servisler;

/// wa-servis (Node, whatsapp-web.js) köprüsü: süreci başlatır, yerel API ile konuşur.
public sealed class WaServisi : IDisposable
{
    public sealed record Durum(bool hazir, bool qrVar, string ben, int bekleyen);
    public sealed record Gelen(string id, string numara, string ad, string grup, string metin, long zaman, string jid = "");

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private Process? _surec;
    private readonly int _port;
    private readonly string _klasor;

    public bool Hazir { get; private set; }
    public bool QrVar { get; private set; }
    public string Ben { get; private set; } = "";
    public bool SurecCalisiyor => _surec is { HasExited: false } || DisSurecVar();

    public WaServisi(int port)
    {
        _port = port;
        _klasor = Path.Combine(AppContext.BaseDirectory, "wa-servis");
        if (!Directory.Exists(_klasor))
        {
            // Geliştirme yerleşimi: bin\Release\...\ altından proje köküne çık
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 5 && d != null; i++, d = d.Parent)
            {
                var aday = Path.Combine(d.FullName, "wa-servis");
                if (File.Exists(Path.Combine(aday, "index.js"))) { _klasor = aday; break; }
            }
        }
    }

    private string Url(string yol) => $"http://127.0.0.1:{_port}{yol}";

    /// Servis yoksa node ile gizli başlatır. node ya da index.js yoksa false.
    public bool Baslat()
    {
        if (DisSurecVar()) return true;
        if (!File.Exists(Path.Combine(_klasor, "index.js")) || !Directory.Exists(Path.Combine(_klasor, "node_modules"))) return false;
        try
        {
            var psi = new ProcessStartInfo("node", "index.js")
            {
                WorkingDirectory = _klasor,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            psi.Environment["WA_PORT"] = _port.ToString();
            _surec = Process.Start(psi);
            return _surec != null;
        }
        catch { return false; }
    }

    private bool DisSurecVar()
    {
        try
        {
            using var r = Http.Send(new HttpRequestMessage(HttpMethod.Get, Url("/durum")), HttpCompletionOption.ResponseHeadersRead);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<Durum?> DurumAsync()
    {
        try
        {
            var d = await Http.GetFromJsonAsync<Durum>(Url("/durum"));
            if (d != null) { Hazir = d.hazir; QrVar = d.qrVar; Ben = d.ben; }
            return d;
        }
        catch { Hazir = false; QrVar = false; return null; }
    }

    public async Task<BitmapImage?> QrAsync()
    {
        try
        {
            var bayt = await Http.GetByteArrayAsync(Url("/qr"));
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bayt);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    public async Task<List<Gelen>> GelenAsync()
    {
        try { return await Http.GetFromJsonAsync<List<Gelen>>(Url("/gelen")) ?? new(); }
        catch { return new(); }
    }

    /// numara ya da ad ile gönderir; (başarı, mesaj/kime)
    public async Task<(bool Ok, string Mesaj)> GonderAsync(string? numara, string? ad, string metin, string? jid = null)
    {
        try
        {
            var r = await Http.PostAsJsonAsync(Url("/gonder"), new { numara, ad, metin, jid });
            var govde = await r.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(govde);
            if (r.IsSuccessStatusCode) return (true, doc.RootElement.TryGetProperty("kime", out var k) ? k.GetString() ?? "" : "");
            return (false, doc.RootElement.TryGetProperty("hata", out var h) ? h.GetString() ?? r.StatusCode.ToString() : r.StatusCode.ToString());
        }
        catch (Exception e) { return (false, e.Message); }
    }

    /// Dosya gönderir; numara ve ad boşsa kendine ("Siz" sohbeti) gider
    public async Task<(bool Ok, string Mesaj)> GonderDosyaAsync(string? numara, string? ad, string yol, string? metin = null)
    {
        try
        {
            using var istemci = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var r = await istemci.PostAsJsonAsync(Url("/gonder-dosya"), new { numara, ad, yol, metin });
            var govde = await r.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(govde);
            if (r.IsSuccessStatusCode) return (true, doc.RootElement.TryGetProperty("kime", out var k) ? k.GetString() ?? "" : "");
            return (false, doc.RootElement.TryGetProperty("hata", out var h) ? h.GetString() ?? r.StatusCode.ToString() : r.StatusCode.ToString());
        }
        catch (Exception e) { return (false, e.Message); }
    }

    public async Task<string?> KisiNumarasiAsync(string ad)
    {
        try
        {
            var r = await Http.GetAsync(Url("/kisi?ad=" + Uri.EscapeDataString(ad)));
            if (!r.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("numara", out var n) ? n.GetString() : null;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        try { if (_surec is { HasExited: false }) _surec.Kill(entireProcessTree: true); } catch { }
    }
}
