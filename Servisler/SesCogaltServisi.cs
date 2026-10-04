using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DinamikAda.Servisler;

/// Varsayılan ses aygıtındaki sesi yakalar (WASAPI loopback) ve diğer aktif çıkış aygıtlarına
/// aynı anda yazar: ses birden çok hoparlörden/kulaklıktan birlikte çıkar.
/// Seviye eşitliği: loopback varsayılanın ana ses seviyesini zaten içerir, bu yüzden her hedef
/// aygıtın donanım seviyesi tam açılır (1.0); böylece tüm aygıtlar varsayılanla aynı seviyede duyulur
/// ve kullanıcı ana sesi değiştirince hepsi birlikte değişir.
/// Senkron: tüm hedefler tek kaynaktan aynı düşük gecikmeyle beslenir ve aynı anda başlatılır;
/// aygıt saatleri kaydıkça (clock drift) biriken gecikme sıfırlanarak sabit tutulur.
public sealed class SesCogaltServisi : IDisposable
{
    private const int GecikmeMs = 50;          // WasapiOut tampon gecikmesi (düşük: senkron; çok düşük: ses kesilir)
    private const double EnCokGecikmeMs = 130;  // tampon bunu aşarsa drift birikmiş demektir, senkron için sıfırlanır

    private sealed class Hedef
    {
        public IWavePlayer Cihaz = null!;
        public BufferedWaveProvider Tampon = null!;
        public IDisposable? Ara;
        public MMDevice Aygit = null!;
        public float EskiSeviye;
        public bool EskiSessiz;
        public bool SeviyeDegisti;
    }

    private WasapiLoopbackCapture? _yakala;
    private readonly List<Hedef> _hedefler = new();
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
            var fmt = _yakala.WaveFormat;   // varsayılan aygıtın karışım formatı (post-volume)

            foreach (var d in _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (d.ID == varsayilan.ID) continue;   // varsayılan zaten kendisi çalıyor
                try
                {
                    var tampon = new BufferedWaveProvider(fmt) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromMilliseconds(600) };
                    IWavePlayer cihaz = new WasapiOut(d, AudioClientShareMode.Shared, true, GecikmeMs);
                    IDisposable? ara = null;
                    try { cihaz.Init(tampon); }
                    catch
                    {
                        // Format uymadı: aygıtın karışım formatına yeniden örnekle
                        try { cihaz.Dispose(); } catch { }
                        var resampler = new MediaFoundationResampler(tampon, d.AudioClient.MixFormat) { ResamplerQuality = 40 };
                        ara = resampler;
                        cihaz = new WasapiOut(d, AudioClientShareMode.Shared, true, GecikmeMs);
                        cihaz.Init(resampler);
                    }

                    var h = new Hedef { Cihaz = cihaz, Tampon = tampon, Ara = ara, Aygit = d };
                    // Seviyeyi varsayılanla eşitle: hedef donanım sesini tam aç (loopback zaten varsayılan seviyesini taşır)
                    try
                    {
                        h.EskiSeviye = d.AudioEndpointVolume.MasterVolumeLevelScalar;
                        h.EskiSessiz = d.AudioEndpointVolume.Mute;
                        d.AudioEndpointVolume.MasterVolumeLevelScalar = 1.0f;
                        d.AudioEndpointVolume.Mute = false;
                        h.SeviyeDegisti = true;
                    }
                    catch { }
                    try { cihaz.Volume = 1.0f; } catch { }
                    _hedefler.Add(h);
                }
                catch (Exception e) { Durum = d.FriendlyName + ": " + e.Message; }
            }

            if (_hedefler.Count == 0) { Durdur(); return 0; }

            _yakala.DataAvailable += (_, ev) =>
            {
                foreach (var h in _hedefler)
                {
                    try
                    {
                        // Clock drift: gecikme birikmişse senkron için eski örnekleri at (küçük, seyrek bir sıçrama)
                        if (h.Tampon.BufferedDuration.TotalMilliseconds > EnCokGecikmeMs + GecikmeMs)
                            h.Tampon.ClearBuffer();
                        h.Tampon.AddSamples(ev.Buffer, 0, ev.BytesRecorded);
                    }
                    catch { }
                }
            };
            _yakala.StartRecording();
            // Tüm hedefleri aynı anda başlat: aralarında kayma olmasın
            foreach (var h in _hedefler) { try { h.Cihaz.Play(); } catch { } }

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
            // Hedef aygıtın donanım seviyesini eski haline döndür
            if (h.SeviyeDegisti)
                try { h.Aygit.AudioEndpointVolume.MasterVolumeLevelScalar = h.EskiSeviye; h.Aygit.AudioEndpointVolume.Mute = h.EskiSessiz; } catch { }
            try { h.Aygit.Dispose(); } catch { }
        }
        _hedefler.Clear();
        Aktif = false;
    }

    public void Dispose() { Durdur(); _enum.Dispose(); }
}
