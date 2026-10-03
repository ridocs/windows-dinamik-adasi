using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DinamikAda.Servisler;

/// Ana ses seviyesi ve varsayılan ses aygıtı değişimi (kulaklık tak/çıkar).
/// Olaylar arka plan iş parçacığından gelir; dinleyen taraf UI'a geçirir.
public sealed class SesServisi : IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator _sayici = new();
    private MMDevice? _aygit;
    private string _aygitId = "";
    private bool _kendimDegistirdim;

    /// (seviye 0..1, sessiz mi)
    public event Action<float, bool>? SeviyeDegisti;
    /// Varsayılan çıkış aygıtı değişti: yeni aygıtın adı
    public event Action<string>? AygitDegisti;

    public float Seviye => Guvenli(() => _aygit?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0f);
    public bool Sessiz => Guvenli(() => _aygit?.AudioEndpointVolume.Mute ?? false);
    public string AygitAdi => Guvenli(() => _aygit?.FriendlyName ?? "");

    public void Baslat()
    {
        AygitBagla();
        _sayici.RegisterEndpointNotificationCallback(this);
    }

    private void AygitBagla()
    {
        try
        {
            var yeni = _sayici.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (_aygit != null)
            {
                _aygit.AudioEndpointVolume.OnVolumeNotification -= SeviyeBildirimi;
                _aygit.Dispose();
            }
            _aygit = yeni;
            _aygitId = yeni.ID;
            _aygit.AudioEndpointVolume.OnVolumeNotification += SeviyeBildirimi;
        }
        catch
        {
            _aygit = null;
            _aygitId = "";
        }
    }

    private void SeviyeBildirimi(AudioVolumeNotificationData d)
    {
        // Kendi kaydırıcımızdan gelen değişiklik için "ses tuşu" duyurusu çıkmasın
        if (_kendimDegistirdim) { _kendimDegistirdim = false; return; }
        SeviyeDegisti?.Invoke(d.MasterVolume, d.Muted);
    }

    public void SeviyeAyarla(float oran)
    {
        if (_aygit == null) return;
        try
        {
            _kendimDegistirdim = true;
            _aygit.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(oran, 0f, 1f);
            if (oran > 0 && _aygit.AudioEndpointVolume.Mute) _aygit.AudioEndpointVolume.Mute = false;
        }
        catch { _kendimDegistirdim = false; }
    }

    public void SessizDegistir()
    {
        if (_aygit == null) return;
        try { _kendimDegistirdim = true; _aygit.AudioEndpointVolume.Mute = !_aygit.AudioEndpointVolume.Mute; }
        catch { _kendimDegistirdim = false; }
    }

    // ---- IMMNotificationClient ----
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow != DataFlow.Render || role != Role.Multimedia) return;
        if (defaultDeviceId == _aygitId) return;
        AygitBagla();
        AygitDegisti?.Invoke(AygitAdi);
    }
    public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
    public void OnDeviceAdded(string pwstrDeviceId) { }
    public void OnDeviceRemoved(string deviceId) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

    private static T Guvenli<T>(Func<T> f) { try { return f(); } catch { return default!; } }

    public void Dispose()
    {
        try { _sayici.UnregisterEndpointNotificationCallback(this); } catch { }
        _aygit?.Dispose();
        _sayici.Dispose();
    }
}
