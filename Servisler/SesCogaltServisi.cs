using System.IO;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DinamikAda.Servisler;

/// Varsayilan ses aygitindaki sesi yakalar (WASAPI loopback) ve diger aktif cikis aygitlarina
/// ayni anda yazar: ses birden cok hoparlorden/kulakliktan birlikte cikar.
/// Seviye esitligi: loopback varsayilanin ana ses seviyesini zaten icerir, bu yuzden her hedef
/// aygitin donanim seviyesi tam acilir (1.0); boylece tum aygitlar varsayilanla ayni seviyede duyulur.
/// Senkron: tum hedefler tek kaynaktan ayni dusuk gecikmeyle beslenir ve ayni anda baslatilir.
/// Thread guvenligi: hedefler degismez bir dizi (snapshot); yakalama is parcacigi yalniz o anki
/// diziyi gezer, durdurma yeni (bos) diziyle degistirir. Cokme kurtarma: baslatinca degistirilen
/// aygit seviyeleri diske yazilir; uygulama Durdur cagrilmadan olurse sonraki acilista geri alinir.
public sealed class SesCogaltServisi : IDisposable
{
    private const int GecikmeMs = 50;
    private const double EnCokGecikmeMs = 130;

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

    private static string KurtarmaDosyasi =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinamikAda", "coklu-kurtarma.json");

    private WasapiLoopbackCapture? _yakala;
    private volatile Hedef[] _hedefler = Array.Empty<Hedef>();
    private readonly MMDeviceEnumerator _enum = new();

    public bool Aktif { get; private set; }
    public string Durum { get; private set; } = "";

    public int Baslat()
    {
        Durdur();
        var liste = new List<Hedef>();
        var kurtarma = new Dictionary<string, float[]>();
        MMDevice? varsayilan = null;
        try
        {
            varsayilan = _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _yakala = new WasapiLoopbackCapture(varsayilan);
            var fmt = _yakala.WaveFormat;

            foreach (var d in _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (d.ID == varsayilan.ID) { d.Dispose(); continue; }
                IWavePlayer? cihaz = null;
                IDisposable? ara = null;
                try
                {
                    var tampon = new BufferedWaveProvider(fmt) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromMilliseconds(600) };
                    cihaz = new WasapiOut(d, AudioClientShareMode.Shared, true, GecikmeMs);
                    try { cihaz.Init(tampon); }
                    catch
                    {
                        cihaz.Dispose();
                        var resampler = new MediaFoundationResampler(tampon, d.AudioClient.MixFormat) { ResamplerQuality = 40 };
                        ara = resampler;
                        cihaz = new WasapiOut(d, AudioClientShareMode.Shared, true, GecikmeMs);
                        cihaz.Init(resampler);
                    }

                    var h = new Hedef { Cihaz = cihaz, Tampon = tampon, Ara = ara, Aygit = d };
                    try
                    {
                        h.EskiSeviye = d.AudioEndpointVolume.MasterVolumeLevelScalar;
                        h.EskiSessiz = d.AudioEndpointVolume.Mute;
                        kurtarma[d.ID] = new[] { h.EskiSeviye, h.EskiSessiz ? 1f : 0f };
                        d.AudioEndpointVolume.MasterVolumeLevelScalar = 1.0f;
                        d.AudioEndpointVolume.Mute = false;
                        h.SeviyeDegisti = true;
                    }
                    catch { }
                    try { cihaz.Volume = 1.0f; } catch { }
                    liste.Add(h);
                }
                catch (Exception e)
                {
                    Durum = d.FriendlyName + ": " + e.Message;
                    try { cihaz?.Dispose(); } catch { }
                    try { ara?.Dispose(); } catch { }
                    try { d.Dispose(); } catch { }
                }
            }

            try { varsayilan.Dispose(); } catch { }

            if (liste.Count == 0) { Durdur(); return 0; }

            KurtarmaYaz(kurtarma);
            _hedefler = liste.ToArray();

            _yakala.DataAvailable += (_, ev) =>
            {
                var hed = _hedefler;
                foreach (var h in hed)
                {
                    try
                    {
                        if (h.Tampon.BufferedDuration.TotalMilliseconds > EnCokGecikmeMs + GecikmeMs)
                            h.Tampon.ClearBuffer();
                        h.Tampon.AddSamples(ev.Buffer, 0, ev.BytesRecorded);
                    }
                    catch { }
                }
            };
            _yakala.StartRecording();
            foreach (var h in liste) { try { h.Cihaz.Play(); } catch { } }

            Aktif = true;
            Durum = liste.Count + " ek aygit";
            return liste.Count;
        }
        catch (Exception e)
        {
            Durum = e.Message;
            try { varsayilan?.Dispose(); } catch { }
            Durdur();
            return 0;
        }
    }

    public void Durdur()
    {
        try { _yakala?.StopRecording(); } catch { }
        try { _yakala?.Dispose(); } catch { }
        _yakala = null;

        var eski = _hedefler;
        _hedefler = Array.Empty<Hedef>();
        foreach (var h in eski)
        {
            try { h.Cihaz.Stop(); } catch { }
            try { h.Cihaz.Dispose(); } catch { }
            try { h.Ara?.Dispose(); } catch { }
            if (h.SeviyeDegisti)
                try { h.Aygit.AudioEndpointVolume.MasterVolumeLevelScalar = h.EskiSeviye; h.Aygit.AudioEndpointVolume.Mute = h.EskiSessiz; } catch { }
            try { h.Aygit.Dispose(); } catch { }
        }
        if (eski.Length > 0) KurtarmaSil();
        Aktif = false;
    }

    private static void KurtarmaYaz(Dictionary<string, float[]> veri)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(KurtarmaDosyasi)!);
            File.WriteAllText(KurtarmaDosyasi, JsonSerializer.Serialize(veri));
        }
        catch { }
    }

    private static void KurtarmaSil()
    {
        try { if (File.Exists(KurtarmaDosyasi)) File.Delete(KurtarmaDosyasi); } catch { }
    }

    /// Uygulama baslarken cagrilir: onceki oturum Durdur cagirmadan olduyse aygit seviyelerini geri al.
    public static void KurtarmaGeriYukle()
    {
        try
        {
            if (!File.Exists(KurtarmaDosyasi)) return;
            var veri = JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(KurtarmaDosyasi));
            if (veri is { Count: > 0 })
            {
                using var en = new MMDeviceEnumerator();
                foreach (var (id, deger) in veri)
                {
                    if (deger is not { Length: >= 2 }) continue;
                    try
                    {
                        using var d = en.GetDevice(id);
                        d.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(deger[0], 0f, 1f);
                        d.AudioEndpointVolume.Mute = deger[1] >= 0.5f;
                    }
                    catch { }
                }
            }
        }
        catch { }
        finally { KurtarmaSil(); }
    }

    public void Dispose() { Durdur(); try { _enum.Dispose(); } catch { } }
}
