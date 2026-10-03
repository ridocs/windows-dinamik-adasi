using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DinamikAda.Servisler;

/// Claude'a sor: API anahtarı girildiyse Messages API, yoksa makinedeki Claude Code CLI (`claude -p`, kullanıcının aboneliği).
public sealed class ClaudeServisi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };
    private static readonly string? Cli = CliBul();

    private string _apiKey = "", _model = "claude-sonnet-5-5", _ekNot = "";
    private readonly List<(string Rol, string Metin)> _gecmis = new();   // kısa sohbet bağlamı (son 6 tur)

    public static bool CliVar => Cli != null;
    public bool ApiVar => _apiKey.Length > 0;
    public bool Hazir => ApiVar || CliVar;
    public string Kaynak => ApiVar ? $"API · {_model}" : CliVar ? "Claude Code CLI" : "yok";

    public void Ayarla(string apiKey, string model, string ekNot)
    {
        _apiKey = apiKey.Trim();
        if (!string.IsNullOrWhiteSpace(model)) _model = model.Trim();
        _ekNot = ekNot.Trim();
    }

    public void GecmisiSil() => _gecmis.Clear();

    /// Sohbete arka plan bilgisi ekle (günün özeti gibi): sonraki sorularda Claude bunu bilir
    public void BaglamEkle(string bilgi)
    {
        _gecmis.Add(("user", "Arka plan bilgisi (yanıt verme, sadece aklında tut):\n" + bilgi));
        _gecmis.Add(("assistant", "Tamam, aklımda."));
    }

    private const string SistemTemel =
        "Sen Dinamik Ada adlı küçük bir masaüstü kapsülünün içinde yaşayan yardımcısın. Türkçe, kısa ve net yanıt ver: " +
        "çoğu soru için en çok 4-5 cümle ya da kısa bir madde listesi; uzun özet istenirse en çok 10 madde. " +
        "Markdown başlığı, tablo ve kod bloğu kullanma (düz metin, gerekirse kısa çizgiyle madde). Uzun tire karakteri (U+2014, U+2013) kullanma. " +
        "Çeviri istenirse yalnız çeviriyi ver. Araç kullanma, dosya okuma ya da komut çalıştırma; sana verilen metinle yetin.";

    /// Soru sor; önceki turlar bağlam olarak gider. Hata olursa (false, hata metni).
    public async Task<(bool Ok, string Yanit)> SorAsync(string soru, CancellationToken iptal = default)
    {
        if (!Hazir) return (false, "Claude kaynağı yok: ayarlardan API anahtarı girin ya da Claude Code kurulu olsun");
        var sonuc = ApiVar ? await ApiAsync(soru, iptal) : await CliAsync(soru, iptal);
        if (sonuc.Ok)
        {
            _gecmis.Add(("user", soru)); _gecmis.Add(("assistant", sonuc.Yanit));
            while (_gecmis.Count > 12) _gecmis.RemoveAt(0);
        }
        return sonuc;
    }

    private string Sistem => _ekNot.Length > 0 ? SistemTemel + "\nKullanıcının notu: " + _ekNot : SistemTemel;

    // ---------- Messages API ----------
    private async Task<(bool Ok, string Yanit)> ApiAsync(string soru, CancellationToken iptal)
    {
        try
        {
            var mesajlar = _gecmis.Select(m => new { role = m.Rol, content = m.Metin }).ToList();
            mesajlar.Add(new { role = "user", content = soru });
            var govde = JsonSerializer.Serialize(new { model = _model, max_tokens = 1024, system = Sistem, messages = mesajlar });
            using var istek = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            { Content = new StringContent(govde, Encoding.UTF8, "application/json") };
            istek.Headers.Add("x-api-key", _apiKey);
            istek.Headers.Add("anthropic-version", "2023-06-01");
            using var yanit = await Http.SendAsync(istek, iptal);
            string metin = await yanit.Content.ReadAsStringAsync(iptal);
            var kok = JsonDocument.Parse(metin).RootElement;
            if (!yanit.IsSuccessStatusCode)
                return (false, kok.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) ? m.GetString() ?? metin : $"HTTP {(int)yanit.StatusCode}");
            var sb = new StringBuilder();
            foreach (var blok in kok.GetProperty("content").EnumerateArray())
                if (blok.TryGetProperty("type", out var t) && t.GetString() == "text") sb.Append(blok.GetProperty("text").GetString());
            return (true, sb.ToString().Trim());
        }
        catch (Exception e) { return (false, e.Message); }
    }

    // ---------- Claude Code CLI ----------
    private static string? CliBul()
    {
        var adaylar = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.cmd"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "claude", "claude.exe"),
        };
        return adaylar.FirstOrDefault(File.Exists);
    }

    private async Task<(bool Ok, string Yanit)> CliAsync(string soru, CancellationToken iptal)
    {
        try
        {
            // Bağlam: önceki turlar tek metinde; sistem notu --append-system-prompt ile
            var sb = new StringBuilder();
            if (_gecmis.Count > 0)
            {
                sb.AppendLine("Önceki konuşma:");
                foreach (var (rol, metin) in _gecmis) sb.AppendLine((rol == "user" ? "Kullanıcı: " : "Sen: ") + metin);
                sb.AppendLine().AppendLine("Yeni soru:");
            }
            sb.Append(soru);

            string calisma = Path.Combine(Path.GetTempPath(), "dinamikada-claude");
            Directory.CreateDirectory(calisma);
            var psi = new ProcessStartInfo(Cli!)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = calisma,
            };
            psi.ArgumentList.Add("-p"); psi.ArgumentList.Add("--output-format"); psi.ArgumentList.Add("text");
            psi.ArgumentList.Add("--max-turns"); psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("--append-system-prompt"); psi.ArgumentList.Add(Sistem);
            if (!string.IsNullOrWhiteSpace(_model) && _model != "claude-sonnet-5-5") { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(_model); }
            psi.Environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1";

            using var p = Process.Start(psi);
            if (p == null) return (false, "claude başlatılamadı");
            var girdi = Encoding.UTF8.GetBytes(sb.ToString());
            await p.StandardInput.BaseStream.WriteAsync(girdi, iptal);
            p.StandardInput.Close();
            var cikti = p.StandardOutput.ReadToEndAsync(iptal);
            var hata = p.StandardError.ReadToEndAsync(iptal);
            using var zaman = CancellationTokenSource.CreateLinkedTokenSource(iptal);
            zaman.CancelAfter(TimeSpan.FromSeconds(120));
            try { await p.WaitForExitAsync(zaman.Token); }
            catch { try { p.Kill(true); } catch { } return (false, iptal.IsCancellationRequested ? "iptal edildi" : "Claude 120 sn içinde yanıt vermedi"); }
            string yanit = (await cikti).Trim();
            if (p.ExitCode != 0 || yanit.Length == 0)
            {
                string h = (await hata).Trim();
                return (false, h.Length > 0 ? h.Split('\n')[0] : $"claude çıkış kodu {p.ExitCode}");
            }
            return (true, yanit);
        }
        catch (Exception e) { return (false, e.Message); }
    }

    /// Hazne / dosya özetleri için: metin dosyalarını okur, çok uzunsa kırpar
    public static string DosyaMetni(IEnumerable<string> yollar, int enCok = 40000)
    {
        var uzantilar = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".markdown", ".csv", ".json", ".xml", ".yaml", ".yml", ".log", ".ini", ".cfg", ".toml", ".cs", ".js", ".ts", ".py", ".ps1", ".sh", ".html", ".css", ".sql", ".java", ".kt", ".go", ".rs", ".c", ".cpp", ".h", ".srt", ".vtt" };
        var sb = new StringBuilder();
        foreach (var yol in yollar)
        {
            if (!File.Exists(yol) || !uzantilar.Contains(Path.GetExtension(yol))) continue;
            string icerik;
            try { icerik = File.ReadAllText(yol); } catch { continue; }
            sb.AppendLine($"=== {Path.GetFileName(yol)} ===");
            int kalan = enCok - sb.Length;
            if (kalan <= 0) break;
            sb.AppendLine(icerik.Length > kalan ? icerik[..kalan] + "\n[... kırpıldı]" : icerik);
        }
        return sb.ToString();
    }
}
