using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DinamikAda.Servisler;

/// Varsayılan ses aygıtındaki sesi yakalar (WASAPI loopback) ve diğer aktif çıkış aygıtlarına
/// aynı anda yazar: ses birden çok hoparlörden/kulaklıktan birlikte çıkar. Küçük bir gecikme olur.
public sealed class SesCogaltServisi : IDisposable
{
    private WasapiLoopbackCapture? _yakala;
    private readonly List<(IWavePlayer Cihaz, BufferedWaveProvider Tampon, IDisposable? Ara)> _hedefler = new();
    private readonly MMDeviceEnumerator _enum = new();

    public bool Aktif { get; private set; }
    public string Durum { get; private set; } = "";

    /// Çoğaltmayı başlat. Dönüş: kaç ek aygıta yazılıyor.
    public int Baslat()
    {
        Durdur();
        try
        {
            var varsayilan = _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _yakala = new WasapiLoopbackCapture(varsayilan);
            var fmt = _yakala.WaveFormat;   // varsayılan aygıtın karışım formatı (genelde 48k stereo float)

            foreach (var d in _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (d.ID == varsayilan.ID) continue;   // varsayılan zaten kendisi çalıyor
                try
                {
                    var tampon = new BufferedWaveProvider(fmt) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(2) };
                    var cihaz = new WasapiOut(d, AudioClientShareMode.Shared, true, 80);
                    IDisposable? ara = null;
                    try { cihaz.Init(tampon); }
                    catch
                    {
                        // Format uymadı: aygıtın karışım formatına yeniden örnekle
                        var resampler = new MediaFoundationResampler(tampon, d.AudioClient.MixFormat) { ResamplerQuality = 40 };
                        ara = resampler;
                        cihaz = new WasapiOut(d, AudioClientShareMode.Shared, true, 80);
                        cihaz.Init(resampler);
                    }
                    cihaz.Play();
                    _hedefler.Add((cihaz, tampon, ara));
                }
                catch (Exception e) { Durum = d.FriendlyName + ": " + e.Message; }
            }

            if (_hedefler.Count == 0) { Durdur(); return 0; }

            _yakala.DataAvailable += (_, ev) =>
            {
                foreach (var h in _hedefler)
                    try { h.Tampon.AddSamples(ev.Buffer, 0, ev.BytesRecorded); } catch { }
            };
            _yakala.RecordingStopped += (_, _) => { };
            _yakala.StartRecording();
            Aktif = true;
            Durum = $"{_hedefler.Count} ek aygıt";
            return _hedefler.Count;
        }
        catch (Exception e) { Durum = e.Message; Durdur(); return 0; }
    }

    public void Durdur()
    {
        try { _yakala?.StopRecording(); } catch { }
        try { _yakala?.Dispose(); } catch { }
        _yakala = null;
        foreach (var h in _hedefler)
        {
            try { h.Cihaz.Stop(); } catch { }
            try { h.Cihaz.Dispose(); } catch { }
            try { h.Ara?.Dispose(); } catch { }
        }
        _hedefler.Clear();
        Aktif = false;
    }

    public void Dispose() { Durdur(); _enum.Dispose(); }
}
