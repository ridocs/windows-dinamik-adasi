using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace DinamikAda;

/// Arayüzün ihtiyaç duyduğu, donmuş (thread-güvenli) medya anlık görüntüsü.
public sealed record MedyaDurumu(
    bool VarMi,
    string Baslik,
    string Sanatci,
    bool Oynuyor,
    BitmapSource? Kapak,
    string Aumid = "")
{
    public static readonly MedyaDurumu Bos = new(false, "", "", false, null);
}

/// Windows'un sistem medya oturumunu (Spotify, tarayıcı, Groove vb.) dinler.
public sealed class MedyaServisi
{
    private GlobalSystemMediaTransportControlsSessionManager? _yonetici;
    private GlobalSystemMediaTransportControlsSession? _oturum;
    private readonly SemaphoreSlim _kilit = new(1, 1);

    public event Action<MedyaDurumu>? Degisti;

    public async Task BaslatAsync()
    {
        _yonetici = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _yonetici.CurrentSessionChanged += (_, _) => OturumBagla();
        _yonetici.SessionsChanged += (_, _) => OturumBagla();
        OturumBagla();
    }

    /// Çalmakta olan bir oturum varsa onu, yoksa Windows'un "geçerli" dediğini seçer.
    /// (Riot Client gibi başlatıcılar boş bir oturum açıp geçerli oturumu kapabiliyor.)
    private GlobalSystemMediaTransportControlsSession? EnIyiOturum()
    {
        if (_yonetici == null) return null;
        try
        {
            foreach (var s in _yonetici.GetSessions())
            {
                if (s.GetPlaybackInfo()?.PlaybackStatus
                    == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    return s;
            }
        }
        catch { /* liste alınamazsa geçerli oturuma düş */ }
        return _yonetici.GetCurrentSession();
    }

    private void OturumBagla()
    {
        if (_oturum != null)
        {
            _oturum.MediaPropertiesChanged -= OturumDegisti;
            _oturum.PlaybackInfoChanged -= OturumDegisti;
        }

        _oturum = EnIyiOturum();

        if (_oturum == null)
        {
            Degisti?.Invoke(MedyaDurumu.Bos);
            return;
        }

        _oturum.MediaPropertiesChanged += OturumDegisti;
        _oturum.PlaybackInfoChanged += OturumDegisti;
        _ = YenileAsync();
    }

    // Bağlı oturum duraklayıp başka biri çalmaya başlamış olabilir: her olayda en iyi oturumu yeniden seç.
    private void OturumDegisti(object? _, object __) => OturumBagla();

    private async Task YenileAsync()
    {
        var oturum = _oturum;
        if (oturum == null) return;

        await _kilit.WaitAsync();
        try
        {
            var ozellik = await oturum.TryGetMediaPropertiesAsync();
            var oynatma = oturum.GetPlaybackInfo();
            bool oynuyor = oynatma?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            BitmapSource? kapak = null;
            if (ozellik?.Thumbnail != null)
            {
                try { kapak = await KapakOkuAsync(ozellik.Thumbnail); }
                catch { /* kapak okunamazsa simgesiz devam */ }
            }

            var baslik = ozellik?.Title ?? "";

            // Başlatıcı artığı: duraklamış, kapaksız, sanatçısız ve süresi sıfır bir oturum
            // (ör. Riot Client) medya değildir; saat moduna düş.
            var cizelge = oturum.GetTimelineProperties();
            bool artik = !oynuyor
                         && kapak == null
                         && string.IsNullOrWhiteSpace(SanatciAdi(ozellik))
                         && cizelge.EndTime == TimeSpan.Zero;

            Degisti?.Invoke(new MedyaDurumu(
                VarMi: !string.IsNullOrWhiteSpace(baslik) && !artik,
                Baslik: baslik,
                Sanatci: SanatciAdi(ozellik),
                Oynuyor: oynuyor,
                Kapak: kapak,
                Aumid: oturum.SourceAppUserModelId ?? ""));
        }
        catch
        {
            Degisti?.Invoke(MedyaDurumu.Bos);
        }
        finally
        {
            _kilit.Release();
        }
    }

    private static string SanatciAdi(GlobalSystemMediaTransportControlsSessionMediaProperties? o)
    {
        if (o == null) return "";
        if (!string.IsNullOrWhiteSpace(o.Artist)) return o.Artist;
        return o.AlbumArtist ?? "";
    }

    /// Konum tahmini: son bildirilen konum + o andan beri geçen süre (oynuyorsa).
    public (TimeSpan Konum, TimeSpan Sure) ZamanCizelgesi()
    {
        var oturum = _oturum;
        if (oturum == null) return (TimeSpan.Zero, TimeSpan.Zero);

        var c = oturum.GetTimelineProperties();
        var oynuyor = oturum.GetPlaybackInfo()?.PlaybackStatus
                      == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        var konum = c.Position;
        if (oynuyor)
            konum += DateTimeOffset.Now - c.LastUpdatedTime;

        var sure = c.EndTime - c.StartTime;
        if (konum > sure) konum = sure;
        if (konum < TimeSpan.Zero) konum = TimeSpan.Zero;
        return (konum, sure);
    }

    public Task OynatDuraklatAsync() => _oturum?.TryTogglePlayPauseAsync().AsTask() ?? Task.CompletedTask;
    public Task SonrakiAsync() => _oturum?.TrySkipNextAsync().AsTask() ?? Task.CompletedTask;
    public Task OncekiAsync() => _oturum?.TrySkipPreviousAsync().AsTask() ?? Task.CompletedTask;

    /// Parça içinde konum değiştirir (destekleyen uygulamalarda: Spotify, tarayıcı).
    public async Task<bool> KonumAyarlaAsync(TimeSpan konum)
    {
        var o = _oturum;
        if (o == null) return false;
        try { return await o.TryChangePlaybackPositionAsync(konum.Ticks); }
        catch { return false; }
    }

    public bool KonumDegistirilebilir
    {
        get { try { return _oturum?.GetPlaybackInfo()?.Controls.IsPlaybackPositionEnabled ?? false; } catch { return false; } }
    }

    private static async Task<BitmapSource?> KapakOkuAsync(Windows.Storage.Streams.IRandomAccessStreamReference ref_)
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

        return SiyahKenarKirp(bmp);
    }

    /// Spotify küçük resimleri üst/altta siyah bant taşır; tüm kenarlardaki
    /// tekdüze koyu bantları tespit edip kırpar. Bant yoksa görüntü aynen döner.
    private static BitmapSource SiyahKenarKirp(BitmapSource kaynak)
    {
        var fb = new FormatConvertedBitmap(kaynak, PixelFormats.Bgra32, null, 0);
        int w = fb.PixelWidth, h = fb.PixelHeight;
        if (w < 8 || h < 8) return kaynak;

        int adim = w * 4;
        var px = new byte[adim * h];
        fb.CopyPixels(px, adim, 0);

        bool KoyuSatir(int y)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * adim + x * 4;
                if (px[i] + px[i + 1] + px[i + 2] > 36) return false;
            }
            return true;
        }
        bool KoyuSutun(int x)
        {
            for (int y = 0; y < h; y++)
            {
                int i = y * adim + x * 4;
                if (px[i] + px[i + 1] + px[i + 2] > 36) return false;
            }
            return true;
        }

        int ust = 0, alt = h - 1, sol = 0, sag = w - 1;
        while (ust < alt && KoyuSatir(ust)) ust++;
        while (alt > ust && KoyuSatir(alt)) alt--;
        while (sol < sag && KoyuSutun(sol)) sol++;
        while (sag > sol && KoyuSutun(sag)) sag--;

        int kw = sag - sol + 1, kh = alt - ust + 1;
        if (kw == w && kh == h) return kaynak;
        if (kw < 8 || kh < 8) return kaynak; // tamamen siyah bir kapak; dokunma

        var kirpik = new CroppedBitmap(fb, new Int32Rect(sol, ust, kw, kh));
        kirpik.Freeze();
        return kirpik;
    }
}
