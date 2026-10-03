using System.Diagnostics;
using System.IO;

namespace DinamikAda.Servisler;

/// NVIDIA GPU sıcaklığı ve yükü (nvidia-smi). Yalnız istenince ölçer (oyun katmanı açıkken 3 sn'de bir).
public sealed class GpuServisi
{
    private readonly string? _smi = Bul();
    private DateTime _son = DateTime.MinValue;
    private bool _calisiyor;

    public bool Var => _smi != null;
    public int Sicaklik { get; private set; } = -1;
    public int Kullanim { get; private set; } = -1;

    private static string? Bul()
    {
        var adaylar = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"),
            @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe",
        };
        return adaylar.FirstOrDefault(File.Exists);
    }

    public void Tik(int saniye = 3)
    {
        if (_smi == null || _calisiyor || (DateTime.Now - _son).TotalSeconds < saniye) return;
        _ = OlcAsync();
    }

    private async Task OlcAsync()
    {
        _calisiyor = true;
        try
        {
            var psi = new ProcessStartInfo(_smi!, "--query-gpu=temperature.gpu,utilization.gpu --format=csv,noheader,nounits")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using var p = Process.Start(psi);
            if (p == null) return;
            string satir = (await p.StandardOutput.ReadLineAsync()) ?? "";
            await p.WaitForExitAsync();
            var parca = satir.Split(',', StringSplitOptions.TrimEntries);
            if (parca.Length >= 2 && int.TryParse(parca[0], out int t) && int.TryParse(parca[1], out int k)) { Sicaklik = t; Kullanim = k; }
        }
        catch { }
        finally { _son = DateTime.Now; _calisiyor = false; }
    }
}
