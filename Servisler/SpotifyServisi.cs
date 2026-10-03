using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DinamikAda.Servisler;

/// Spotify Web API: PKCE ile giriş (gizli anahtar gerekmez), beğen / beğeniyi kaldır, sıradaki parçalar.
/// Kullanıcı developer.spotify.com'da bir uygulama açar, Client ID'yi ayarlara yazar, yönlendirme adresi RedirectUri olur.
public sealed class SpotifyServisi
{
    public const string RedirectUri = "http://127.0.0.1:5462/callback";
    private const string Scope = "user-library-read user-library-modify user-read-playback-state user-read-currently-playing";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public sealed record Parca(string Id, string Ad, string Sanatci);

    private string _clientId = "", _refresh = "", _access = "";
    private DateTime _accessBitis = DateTime.MinValue;

    /// Yenileme anahtarı Spotify tarafından döndürülürse (rotasyon) kaydedilsin diye haber verir
    public event Action<string>? YenilemeDegisti;

    public bool Hazir => _clientId.Length > 0 && _refresh.Length > 0;
    public string SonHata { get; private set; } = "";

    public void Ayarla(string clientId, string refresh)
    {
        if (clientId != _clientId || refresh != _refresh) { _access = ""; _accessBitis = DateTime.MinValue; }
        _clientId = clientId.Trim(); _refresh = refresh.Trim();
    }

    // ---------- Giriş (PKCE) ----------

    /// Tarayıcıda Spotify izin sayfasını açar, 127.0.0.1:5462'de geri dönüşü bekler (en çok 3 dk).
    public static async Task<(bool Ok, string Refresh, string Kullanici, string Hata)> BaglanAsync(string clientId)
    {
        clientId = clientId.Trim();
        if (clientId.Length < 16) return (false, "", "", "Client ID boş ya da kısa");
        string verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string state = Base64Url(RandomNumberGenerator.GetBytes(12));

        using var dinleyici = new HttpListener();
        dinleyici.Prefixes.Add("http://127.0.0.1:5462/");
        try { dinleyici.Start(); } catch (Exception e) { return (false, "", "", "5462 portu dinlenemedi: " + e.Message); }

        string url = "https://accounts.spotify.com/authorize?response_type=code&client_id=" + Uri.EscapeDataString(clientId)
                   + "&scope=" + Uri.EscapeDataString(Scope) + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
                   + "&state=" + state + "&code_challenge_method=S256&code_challenge=" + challenge;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) { return (false, "", "", "Tarayıcı açılamadı: " + e.Message); }

        string code = "";
        try
        {
            var baglamGorevi = dinleyici.GetContextAsync();
            var biten = await Task.WhenAny(baglamGorevi, Task.Delay(TimeSpan.FromMinutes(3)));
            if (biten != baglamGorevi) return (false, "", "", "3 dakika içinde izin verilmedi");
            var ctx = baglamGorevi.Result;
            var q = System.Web.HttpUtility.ParseQueryString(ctx.Request.Url?.Query ?? "");
            string hata = q["error"] ?? "";
            if ((q["state"] ?? "") != state) hata = "state uyuşmadı";
            code = q["code"] ?? "";
            string html = hata.Length == 0 && code.Length > 0
                ? "<html><body style='font-family:Segoe UI;background:#0E0E12;color:#F2F2F4;display:flex;align-items:center;justify-content:center;height:100vh'><div><h2 style='color:#D97757'>Dinamik Ada Spotify'a bağlandı</h2><p>Bu sekmeyi kapatabilirsiniz.</p></div></body></html>"
                : $"<html><body style='font-family:Segoe UI'><h2>Bağlanamadı</h2><p>{WebUtility.HtmlEncode(hata)}</p></body></html>";
            var bayt = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8"; ctx.Response.ContentLength64 = bayt.Length;
            await ctx.Response.OutputStream.WriteAsync(bayt); ctx.Response.Close();
            if (hata.Length > 0) return (false, "", "", hata);
        }
        finally { try { dinleyici.Stop(); } catch { } }

        // Kodu anahtara çevir
        using var istek = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = RedirectUri,
                ["client_id"] = clientId, ["code_verifier"] = verifier,
            })
        };
        using var yanit = await Http.SendAsync(istek);
        string govde = await yanit.Content.ReadAsStringAsync();
        if (!yanit.IsSuccessStatusCode) return (false, "", "", "Anahtar alınamadı: " + Kisalt(govde));
        var kok = JsonDocument.Parse(govde).RootElement;
        string refresh = kok.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        string access = kok.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : "";
        if (refresh.Length == 0) return (false, "", "", "refresh_token gelmedi");

        string kullanici = "";
        try
        {
            using var me = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
            me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            using var mr = await Http.SendAsync(me);
            if (mr.IsSuccessStatusCode)
            {
                var m = JsonDocument.Parse(await mr.Content.ReadAsStringAsync()).RootElement;
                kullanici = m.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "";
                if (kullanici.Length == 0 && m.TryGetProperty("id", out var id)) kullanici = id.GetString() ?? "";
            }
        }
        catch { }
        return (true, refresh, kullanici, "");
    }

    // ---------- Anahtar ----------

    private async Task<string?> TokenAsync()
    {
        if (!Hazir) return null;
        if (_access.Length > 0 && DateTime.Now < _accessBitis) return _access;
        try
        {
            using var istek = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                { ["grant_type"] = "refresh_token", ["refresh_token"] = _refresh, ["client_id"] = _clientId })
            };
            using var yanit = await Http.SendAsync(istek);
            string govde = await yanit.Content.ReadAsStringAsync();
            if (!yanit.IsSuccessStatusCode) { SonHata = "yenileme: " + Kisalt(govde); return null; }
            var kok = JsonDocument.Parse(govde).RootElement;
            _access = kok.GetProperty("access_token").GetString() ?? "";
            int sn = kok.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
            _accessBitis = DateTime.Now.AddSeconds(Math.Max(60, sn - 60));
            if (kok.TryGetProperty("refresh_token", out var rt) && rt.GetString() is { Length: > 0 } yeni && yeni != _refresh)
            { _refresh = yeni; YenilemeDegisti?.Invoke(yeni); }
            SonHata = "";
            return _access;
        }
        catch (Exception e) { SonHata = e.Message; return null; }
    }

    private async Task<(HttpStatusCode Kod, string Govde)> IstekAsync(HttpMethod yontem, string yol)
    {
        var token = await TokenAsync();
        if (token == null) return (HttpStatusCode.Unauthorized, "");
        using var istek = new HttpRequestMessage(yontem, "https://api.spotify.com/v1" + yol);
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (yontem == HttpMethod.Put || yontem == HttpMethod.Delete) istek.Content = new StringContent("", Encoding.UTF8, "application/json");
        using var yanit = await Http.SendAsync(istek);
        string govde = await yanit.Content.ReadAsStringAsync();
        // Spotify, Web API'yi uygulama sahibi Premium değilse kapatıyor (403): bir kez öğren, boşuna isteme
        if (yanit.StatusCode == HttpStatusCode.Forbidden && govde.Contains("premium subscription required", StringComparison.OrdinalIgnoreCase))
        { PremiumGerekli = true; SonHata = "Spotify Web API için uygulama sahibinde Premium gerekli"; }
        return (yanit.StatusCode, govde);
    }

    /// Web API 403 "premium required" döndü: beğen için masaüstü kısayolu, sıradakiler yok
    public bool PremiumGerekli { get; private set; }

    /// Tanı: ham GET, durum kodu ve gövdenin başı
    public async Task<string> HamAsync(string yol)
    {
        try { var (kod, govde) = await IstekAsync(HttpMethod.Get, yol); return $"{(int)kod} {Kisalt(govde.Replace('\n', ' '))}"; }
        catch (Exception e) { return "hata " + e.Message; }
    }

    // ---------- İşlevler ----------

    /// Şu an çalan parça ve beğenilmiş mi. Çalan yoksa (null, false).
    public async Task<(Parca? Parca, bool Begenildi)> SimdikiAsync()
    {
        try
        {
            var (kod, govde) = await IstekAsync(HttpMethod.Get, "/me/player/currently-playing?additional_types=track");
            if (kod == HttpStatusCode.NoContent) { SonHata = "Spotify çalan parça bildirmedi (204)"; return (null, false); }
            if (kod != HttpStatusCode.OK || govde.Length == 0) { SonHata = $"currently-playing {(int)kod}"; return (null, false); }
            var kok = JsonDocument.Parse(govde).RootElement;
            if (!kok.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) return (null, false);
            var p = ParcaOku(item);
            if (p == null) return (null, false);
            var (k2, g2) = await IstekAsync(HttpMethod.Get, "/me/tracks/contains?ids=" + p.Id);
            bool begenildi = k2 == HttpStatusCode.OK && g2.TrimStart().StartsWith("[true", StringComparison.OrdinalIgnoreCase);
            return (p, begenildi);
        }
        catch (Exception e) { SonHata = e.Message; return (null, false); }
    }

    /// Çalan parça API'den gelmezse (204, gecikme): adı ve sanatçısıyla ara, beğenilmiş mi bak
    public async Task<(Parca? Parca, bool Begenildi)> AraAsync(string baslik, string sanatci)
    {
        try
        {
            string q = Uri.EscapeDataString($"track:{baslik}" + (sanatci.Length > 0 ? $" artist:{sanatci.Split(',')[0].Trim()}" : ""));
            var (kod, govde) = await IstekAsync(HttpMethod.Get, $"/search?type=track&limit=1&q={q}");
            if (kod != HttpStatusCode.OK) { SonHata = $"search {(int)kod}"; return (null, false); }
            var kok = JsonDocument.Parse(govde).RootElement;
            if (!kok.TryGetProperty("tracks", out var t) || !t.TryGetProperty("items", out var items) || items.GetArrayLength() == 0) return (null, false);
            var p = ParcaOku(items[0]);
            if (p == null) return (null, false);
            var (k2, g2) = await IstekAsync(HttpMethod.Get, "/me/tracks/contains?ids=" + p.Id);
            return (p, k2 == HttpStatusCode.OK && g2.TrimStart().StartsWith("[true", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception e) { SonHata = e.Message; return (null, false); }
    }

    public async Task<bool> BegenAyarlaAsync(string id, bool begen)
    {
        try
        {
            var (kod, _) = await IstekAsync(begen ? HttpMethod.Put : HttpMethod.Delete, "/me/tracks?ids=" + id);
            return kod == HttpStatusCode.OK || kod == HttpStatusCode.NoContent;
        }
        catch (Exception e) { SonHata = e.Message; return false; }
    }

    /// Sıradaki parçalar (en çok n)
    public async Task<List<Parca>> KuyrukAsync(int n = 6)
    {
        var liste = new List<Parca>();
        try
        {
            var (kod, govde) = await IstekAsync(HttpMethod.Get, "/me/player/queue");
            if (kod != HttpStatusCode.OK) return liste;
            var kok = JsonDocument.Parse(govde).RootElement;
            if (kok.TryGetProperty("queue", out var q) && q.ValueKind == JsonValueKind.Array)
                foreach (var e in q.EnumerateArray()) { var p = ParcaOku(e); if (p != null) liste.Add(p); if (liste.Count >= n) break; }
        }
        catch (Exception e) { SonHata = e.Message; }
        return liste;
    }

    private static Parca? ParcaOku(JsonElement e)
    {
        if (!e.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) return null;
        string ad = e.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        string sanatci = e.TryGetProperty("artists", out var a) && a.ValueKind == JsonValueKind.Array
            ? string.Join(", ", a.EnumerateArray().Select(x => x.TryGetProperty("name", out var an) ? an.GetString() : "").Where(x => !string.IsNullOrEmpty(x)))
            : "";
        return new Parca(id.GetString()!, ad, sanatci);
    }

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Kisalt(string s) => s.Length > 160 ? s[..160] + "…" : s;
}
