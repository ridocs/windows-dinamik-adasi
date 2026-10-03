using System.IO;
using System.Windows.Media.Imaging;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DinamikAda.Servisler;

/// Tek bir Windows bildirimi (WhatsApp mesajı, Discord vb.)
public sealed record Bildirim(string Uygulama, string Baslik, string Govde, System.Windows.Media.ImageSource? Logo, DateTime Zaman,
                              string Aumid = "", uint Id = 0)
{
    public string ZamanMetni => Zaman.ToString("HH:mm");
    public string UstBilgi => string.IsNullOrEmpty(Uygulama) ? ZamanMetni : $"{Uygulama} · {ZamanMetni}";
}

/// Windows bildirim merkezini yoklar; yeni gelen toast'ları Bildirim olarak üretir.
/// Paketsiz uygulamada NotificationChanged olayı gelmediği için 2 sn'de bir sorgulanır.
public sealed class BildirimServisi
{
    private UserNotificationListener? _dinleyici;
    private readonly HashSet<uint> _gorulen = new();
    private bool _ilkTur = true;
    private readonly Dictionary<string, BitmapImage?> _logoOnbellek = new();

    public bool Erisim { get; private set; }
    public event Action<Bildirim>? Yeni;

    public async Task<bool> BaslatAsync()
    {
        try
        {
            _dinleyici = UserNotificationListener.Current;
            var sonuc = await _dinleyici.RequestAccessAsync();
            Erisim = sonuc == UserNotificationListenerAccessStatus.Allowed;
        }
        catch { Erisim = false; }
        return Erisim;
    }

    public async Task TikAsync()
    {
        if (!Erisim || _dinleyici == null) return;
        IReadOnlyList<UserNotification> liste;
        try { liste = await _dinleyici.GetNotificationsAsync(NotificationKinds.Toast); }
        catch { return; }

        var simdiki = new HashSet<uint>();
        foreach (var b in liste)
        {
            simdiki.Add(b.Id);
            if (_gorulen.Contains(b.Id)) continue;
            _gorulen.Add(b.Id);
            if (_ilkTur) continue; // açılıştaki eski bildirimleri gösterme

            try
            {
                string uygulama = b.AppInfo?.DisplayInfo?.DisplayName ?? "";
                var metinler = b.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?
                    .GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
                    ?? new List<string>();

                string baslik = metinler.Count > 0 ? metinler[0] : uygulama;
                string govde = metinler.Count > 1 ? string.Join("\n", metinler.Skip(1)) : "";
                var logo = await LogoAsync(b);

                Yeni?.Invoke(new Bildirim(uygulama, baslik, govde, logo, DateTime.Now, b.AppInfo?.AppUserModelId ?? "", b.Id));
            }
            catch { /* tek bir bildirim bozuksa atla */ }
        }

        // Kapatılan bildirimleri unut (kimlikler yeniden kullanılmaz ama küme şişmesin)
        _gorulen.IntersectWith(simdiki);
        _ilkTur = false;
    }

    /// Bildirimi Bildirim Merkezi'nden kaldırır (kapsülde okunup işlem yapıldıysa).
    public void Kaldir(uint id)
    {
        try { _dinleyici?.RemoveNotification(id); } catch { }
    }

    private async Task<BitmapImage?> LogoAsync(UserNotification b)
    {
        string anahtar = b.AppInfo?.AppUserModelId ?? "";
        if (_logoOnbellek.TryGetValue(anahtar, out var onbellek)) return onbellek;

        BitmapImage? logo = null;
        try
        {
            var ref_ = b.AppInfo?.DisplayInfo?.GetLogo(new Windows.Foundation.Size(48, 48));
            if (ref_ != null)
            {
                using var ra = await ref_.OpenReadAsync();
                using var akis = ra.AsStreamForRead();
                var bellek = new MemoryStream();
                await akis.CopyToAsync(bellek);
                bellek.Position = 0;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = bellek;
                bmp.EndInit();
                bmp.Freeze();
                logo = bmp;
            }
        }
        catch { }
        _logoOnbellek[anahtar] = logo;
        return logo;
    }
}
