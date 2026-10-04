using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DinamikAda.Servisler;

/// Kısa, ince arayüz ses efektleri (sentezlenmiş; harici dosya yok). NAudio ile çalınır.
public sealed class SesEfektServisi : IDisposable
{
    public enum Efekt { Ac, Kapan, Birak, Gonder, Basari, Hata, Tik }

    private const int Oran = 44100;
    public bool Acik { get; set; } = true;
    public float Seviye { get; set; } = 0.4f;

    private IWavePlayer? _cihaz;
    private DateTime _son = DateTime.MinValue;
    public string SonHata { get; private set; } = "";

    public void Cal(Efekt e, bool kis = false)
    {
        if (!Acik) return;
        // Çok sık tetiklenen (aç/kapan) efektlerde kısa throttle: hover spam'ini süz
        if ((e == Efekt.Ac || e == Efekt.Kapan) && (DateTime.Now - _son).TotalMilliseconds < 220) return;
        _son = DateTime.Now;
        try
        {
            var veri = Uret(e, kis ? Seviye * 0.6f : Seviye);
            var ms = new System.IO.MemoryStream(veri);
            var akis = new RawSourceWaveStream(ms, WaveFormat.CreateIeeeFloatWaveFormat(Oran, 1));
            try { _cihaz?.Stop(); _cihaz?.Dispose(); } catch { }
            _cihaz = new WasapiOut(AudioClientShareMode.Shared, 100);
            _cihaz.Init(akis);
            _cihaz.Play();
        }
        catch (Exception ex) { SonHata = ex.Message; }
    }

    /// Efekti float örneklerden üret (sinüs sweep + üstel zarf; whoosh için gürültü)
    private static byte[] Uret(Efekt e, float seviye)
    {
        (double f0, double f1, int ms, bool gurultu, double harmonik) = e switch
        {
            Efekt.Ac     => (520.0, 1040.0, 130, false, 2.0),
            Efekt.Kapan  => (1000.0, 440.0, 130, false, 2.0),
            Efekt.Birak  => (300.0, 760.0, 90,  false, 0.0),
            Efekt.Gonder => (1200.0, 300.0, 200, true,  0.0),
            Efekt.Basari => (880.0, 1320.0, 170, false, 1.5),
            Efekt.Hata   => (200.0, 160.0, 220, false, 3.0),
            _            => (900.0, 900.0, 45,  false, 0.0),   // Tik
        };
        int n = Oran * ms / 1000;
        var örnek = new float[n];
        var rnd = new Random();
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;                       // 0..1
            double f = f0 + (f1 - f0) * t;
            double faz = 2 * Math.PI * (f0 * (i / (double)Oran) + (f1 - f0) * (i / (double)Oran) * t / 2);
            double s;
            if (gurultu)
                s = (rnd.NextDouble() * 2 - 1) * Math.Pow(1 - t, 1.4);   // inen whoosh
            else
            {
                s = Math.Sin(faz);
                if (harmonik > 0) s += 0.35 * Math.Sin(faz * harmonik);
            }
            // Zarf: hızlı atak, üstel sönüm
            double atak = Math.Min(1, t / 0.04);
            double sönüm = Math.Exp(-3.0 * t);
            örnek[i] = (float)(s * atak * sönüm * seviye * 0.7);
        }
        var bayt = new byte[n * 4];
        Buffer.BlockCopy(örnek, 0, bayt, 0, bayt.Length);
        return bayt;
    }

    public void Dispose() { try { _cihaz?.Dispose(); } catch { } }
}
