using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DinamikAda.Servisler;

/// Kısa, tok arayüz ses efektleri (sentezlenmiş; harici dosya yok). NAudio/WasapiOut ile çalınır.
public sealed class SesEfektServisi : IDisposable
{
    public enum Efekt { Ac, Kapan, Birak, Gonder, Temizle, Basari, Hata, Tik }

    private const int Oran = 44100;
    public bool Acik { get; set; } = true;
    public float Seviye { get; set; } = 0.5f;
    public string SonHata { get; private set; } = "";

    private IWavePlayer? _cihaz;
    private DateTime _son = DateTime.MinValue;

    public void Cal(Efekt e)
    {
        if (!Acik) return;
        if ((DateTime.Now - _son).TotalMilliseconds < 70) return;   // aynı anda üst üste çalmayı süz
        _son = DateTime.Now;
        try
        {
            var veri = Uret(e, Seviye);
            var akis = new RawSourceWaveStream(new System.IO.MemoryStream(veri), WaveFormat.CreateIeeeFloatWaveFormat(Oran, 1));
            try { _cihaz?.Stop(); _cihaz?.Dispose(); } catch { }
            _cihaz = new WasapiOut(AudioClientShareMode.Shared, 100);
            _cihaz.Init(akis);
            _cihaz.Play();
        }
        catch (Exception ex) { SonHata = ex.Message; }
    }

    /// Tok efekt: ana ton + bir oktav alt (sub) + yumuşak atak, yuvarlak üstel sönüm. Gönder/Temizle için frekans süpürür.
    private static byte[] Uret(Efekt e, float seviye)
    {
        // f0→f1 süpürme, süre(ms), gürültü karışımı, iki ton (Basari), sönüm hızı
        (double f0, double f1, int ms, double gurultu, double ikinci, double sonum) = e switch
        {
            Efekt.Birak   => (190.0, 150.0, 120, 0.0,   0.0,   4.5),   // tok "pat": dosya düştü
            Efekt.Gonder  => (360.0, 940.0, 175, 0.0,   0.0,   2.6),   // yükselen "vuuf": gönderildi
            Efekt.Temizle => (760.0, 170.0, 230, 0.25,  0.0,   2.2),   // inen süpürme: temizlendi
            Efekt.Basari  => (587.0, 587.0, 230, 0.0,   880.0, 2.0),   // D5+A5 ding
            Efekt.Hata    => (190.0, 150.0, 240, 0.08,  0.0,   2.3),   // alçak buzz
            Efekt.Tik     => (430.0, 430.0, 55,  0.0,   0.0,   7.0),   // kısa tok tık
            Efekt.Ac      => (400.0, 640.0, 170, 0.0,   0.0,   3.8),   // yumuşak yükseliş
            Efekt.Kapan   => (940.0, 460.0, 140, 0.0,   0.0,   3.2),
            _             => (500.0, 500.0, 60,  0.0,   0.0,   6.0),
        };
        int n = Oran * ms / 1000;
        var örnek = new float[n];
        var rnd = new Random();
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;                 // 0..1
            double sn = (double)i / Oran;             // saniye
            double f = f0 + (f1 - f0) * t;            // anlık frekans (doğrusal süpürme)
            double faz = 2 * Math.PI * f * sn;
            double s;
            if (gurultu > 0 && (e == Efekt.Temizle || e == Efekt.Hata))
                s = Math.Sin(faz) * (1 - gurultu) + (rnd.NextDouble() * 2 - 1) * gurultu;
            else
                s = Math.Sin(faz);
            s += 0.4 * Math.Sin(Math.PI * f * sn);    // bir oktav alt (sub) → tokluk
            if (ikinci > 0) s += 0.6 * Math.Sin(2 * Math.PI * ikinci * sn);
            double atakSn = e == Efekt.Ac ? 0.035 : 0.012;
            double atak = Math.Min(1, t / atakSn);   // Ac: daha yavaş, yumuşak giriş
            double env = Math.Exp(-sonum * t);
            double kis = e == Efekt.Ac ? 0.7 : 1.0;   // fare giriş sesi biraz daha kısık
            örnek[i] = (float)(s * atak * env * seviye * 0.55 * kis);
        }
        var bayt = new byte[n * 4];
        Buffer.BlockCopy(örnek, 0, bayt, 0, bayt.Length);
        return bayt;
    }

    public void Dispose() { try { _cihaz?.Dispose(); } catch { } }
}
