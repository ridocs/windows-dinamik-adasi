using System.Diagnostics;
using System.IO;

namespace DinamikAda.Servisler;

/// Bırakılan dosyayı Windows'un yerleşik scp'siyle uzak sunucuya kopyalar.
/// Parola soramaz: SSH anahtarıyla erişim gerekir (BatchMode).
public static class YuklemeServisi
{
    public sealed record Sonuc(bool Basarili, string Mesaj);

    public static async Task<Sonuc> KopyalaAsync(string yol, string hedef, int port, string anahtar)
    {
        if (string.IsNullOrWhiteSpace(hedef) || !hedef.Contains(':'))
            return new Sonuc(false, "Hedef ayarlanmamış (kullanici@sunucu:/yol/)");

        string ad = Path.GetFileName(yol.TrimEnd('\\', '/'));
        var (sunucu, uzakYol) = Ayir(hedef);
        string ortakSecenek = $"-o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=12 -p {port}"
                              + (string.IsNullOrWhiteSpace(anahtar) ? "" : $" -i \"{anahtar}\"");

        // Hedef klasörü yoksa oluştur (ör. sunucuda ~/Desktop bulunmayabilir).
        // Tırnak içinde ~ genişlemez; ev dizinine göre yol kullanılır ("Desktop/").
        var mk = await CalistirAsync("ssh", $"{ortakSecenek} {sunucu} \"mkdir -p \\\"{uzakYol}\\\"\"");
        if (mk.Kod != 0) return new Sonuc(false, Kisalt(mk.Hata, "Sunucuya bağlanılamadı"));

        string scpSecenek = ortakSecenek.Replace("-p ", "-P ") + " -r";
        var kopya = await CalistirAsync("scp", $"{scpSecenek} \"{yol}\" \"{sunucu}:{uzakYol}\"");
        return kopya.Kod == 0
            ? new Sonuc(true, $"{ad} → {uzakYol}")
            : new Sonuc(false, Kisalt(kopya.Hata, "Kopyalama başarısız"));
    }

    /// "root@sunucu:~/Desktop/" → ("root@sunucu", "Desktop/"). ~ ve ~/ ev dizinine göre yola çevrilir;
    /// scp hem eski hem SFTP modunda göreli yolu ev dizininden açar.
    private static (string Sunucu, string Yol) Ayir(string hedef)
    {
        int i = hedef.IndexOf(':');
        string sunucu = hedef[..i];
        string yol = hedef[(i + 1)..].Trim();
        if (yol == "~" || yol == "~/" || yol.Length == 0) yol = ".";
        else if (yol.StartsWith("~/")) yol = yol[2..];
        return (sunucu, yol);
    }

    private static string Kisalt(string hata, string varsayilan)
    {
        var satir = hata.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0 && !s.StartsWith("Warning:"));
        if (string.IsNullOrEmpty(satir)) return varsayilan;
        return satir.Length > 90 ? satir[..90] + "…" : satir;
    }

    private static async Task<(int Kod, string Hata)> CalistirAsync(string program, string argumanlar)
    {
        try
        {
            var psi = new ProcessStartInfo(program, argumanlar)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            if (p == null) return (-1, program + " başlatılamadı");
            var hata = p.StandardError.ReadToEndAsync();
            var cikti = p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return (p.ExitCode, await hata);
        }
        catch (Exception e) { return (-1, e.Message); }
    }
}
