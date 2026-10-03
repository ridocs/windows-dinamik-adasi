using System.ComponentModel;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DinamikAda.Servisler;

/// Ses karışımı: çıkış aygıtları, uygulama ses oturumları (aygıt bazında), uygulama başına seviye / sessiz / çıkış aygıtı.
public sealed class KarisimServisi
{
    public sealed record Aygit(string Id, string Ad, string Kisa);

    public sealed class Oturum : INotifyPropertyChanged
    {
        public string Ad { get; init; } = "";
        public string Exe { get; init; } = "";
        public string Surec { get; init; } = "";      // süreç adı (anahtar)
        public List<uint> Pidler { get; } = new();
        internal List<SimpleAudioVolume> Sesler { get; } = new();
        public string AygitId { get; set; } = "";       // şu an çaldığı aygıt
        public string AygitKisa { get; set; } = "";
        public string AygitAd { get; set; } = "";
        public bool Kalici { get; set; }                 // kullanıcı bu uygulamaya aygıt atamış
        public System.Windows.Media.ImageSource? Simge { get; set; }

        private float _seviye; private bool _sessiz;
        public float Seviye { get => _seviye; set { if (Math.Abs(_seviye - value) > 0.001f) { _seviye = value; Degisti(nameof(Seviye)); Degisti(nameof(Yuzde)); } } }
        public bool Sessiz { get => _sessiz; set { if (_sessiz != value) { _sessiz = value; Degisti(nameof(Sessiz)); Degisti(nameof(SesGlif)); } } }
        public string Yuzde => $"%{(int)Math.Round(_seviye * 100)}";
        public string SesGlif => _sessiz || _seviye <= 0 ? "" : _seviye < 0.34 ? "" : _seviye < 0.67 ? "" : "";
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Degisti(string ad) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(ad));
        internal void Yenile() { Degisti(nameof(AygitKisa)); Degisti(nameof(AygitAd)); Degisti(nameof(Kalici)); }
    }

    private readonly MMDeviceEnumerator _enum = new();
    private static readonly string[] Atlanan = { "Idle", "System", "audiodg", "DinamikAda" };

    public List<Aygit> Aygitlar()
    {
        var liste = new List<Aygit>();
        try
        {
            foreach (var d in _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                liste.Add(new Aygit(d.ID, d.FriendlyName, Kisalt(d.FriendlyName)));
        }
        catch { }
        return liste;
    }

    public string VarsayilanAygitId
    {
        get { try { return _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID; } catch { return ""; } }
    }

    /// "LG ULTRAGEAR (NVIDIA High Definition Audio)" → "LG ULTRAGEAR"; en çok 14 karakter
    public static string Kisalt(string ad)
    {
        int i = ad.IndexOf(" (", StringComparison.Ordinal);
        string k = (i > 0 ? ad[..i] : ad).Trim();
        return k.Length > 14 ? k[..14] + "…" : k;
    }

    /// Etkin ses oturumları, uygulama (exe) bazında birleştirilmiş
    public List<Oturum> Oturumlar(Dictionary<string, Oturum>? onceki = null)
    {
        var sonuc = new Dictionary<string, Oturum>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var d in _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                AudioSessionManager mgr;
                try { mgr = d.AudioSessionManager; mgr.RefreshSessions(); } catch { continue; }
                for (int i = 0; i < mgr.Sessions.Count; i++)
                {
                    AudioSessionControl s;
                    try { s = mgr.Sessions[i]; } catch { continue; }
                    uint pid; try { pid = s.GetProcessID; } catch { continue; }
                    if (pid == 0) continue;
                    if (s.State == AudioSessionState.AudioSessionStateExpired) continue;
                    string ad = "", exe = "";
                    try
                    {
                        using var p = Process.GetProcessById((int)pid);
                        ad = p.ProcessName;
                        try { exe = p.MainModule?.FileName ?? ""; } catch { }
                    }
                    catch { continue; }
                    if (Atlanan.Contains(ad, StringComparer.OrdinalIgnoreCase)) continue;
                    string anahtar = ad;
                    if (!sonuc.TryGetValue(anahtar, out var o))
                    {
                        o = onceki != null && onceki.TryGetValue(anahtar, out var eski) ? eski : new Oturum { Ad = OzetServisi.UygulamaAdi(ad), Exe = exe, Surec = ad };
                        o.Pidler.Clear(); o.Sesler.Clear();
                        o.AygitId = d.ID; o.AygitAd = d.FriendlyName; o.AygitKisa = Kisalt(d.FriendlyName);
                        sonuc[anahtar] = o;
                    }
                    o.Pidler.Add(pid);
                    o.Sesler.Add(s.SimpleAudioVolume);
                    if (s.State == AudioSessionState.AudioSessionStateActive) { o.AygitId = d.ID; o.AygitAd = d.FriendlyName; o.AygitKisa = Kisalt(d.FriendlyName); }
                }
            }
            foreach (var o in sonuc.Values)
            {
                if (o.Sesler.Count > 0) { try { o.Seviye = o.Sesler[0].Volume; o.Sessiz = o.Sesler[0].Mute; } catch { } }
                string? kalici = o.Pidler.Count > 0 ? AudioPolicyConfig.Oku(o.Pidler[0]) : null;
                o.Kalici = !string.IsNullOrEmpty(kalici);
                o.Yenile();
            }
        }
        catch { }
        return sonuc.Values.OrderBy(o => o.Ad, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void SeviyeAyarla(Oturum o, float v)
    {
        v = Math.Clamp(v, 0f, 1f);
        foreach (var s in o.Sesler) { try { s.Volume = v; if (v > 0 && s.Mute) s.Mute = false; } catch { } }
        o.Seviye = v; if (v > 0) o.Sessiz = false;
    }

    public void SessizAyarla(Oturum o, bool sessiz)
    {
        foreach (var s in o.Sesler) { try { s.Mute = sessiz; } catch { } }
        o.Sessiz = sessiz;
    }

    /// Uygulamanın çıkışını aygıta yönlendir (null: sistem varsayılanı). Aynı adlı tüm süreçlere uygulanır.
    public bool Yonlendir(Oturum o, string? aygitId)
    {
        var pidler = new HashSet<uint>(o.Pidler);
        try { foreach (var p in Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(o.Exe.Length > 0 ? o.Exe : o.Ad))) { pidler.Add((uint)p.Id); p.Dispose(); } } catch { }
        bool ok = true;
        foreach (var pid in pidler) ok &= AudioPolicyConfig.Ayarla(pid, aygitId);
        return ok;
    }

    public string SonHata => AudioPolicyConfig.SonHata;
}
