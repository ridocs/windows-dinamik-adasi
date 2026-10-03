using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DinamikAda.Servisler;

/// Medya oturumunun AppUserModelId'sinden uygulama adı ve simgesi.
/// Paketli uygulama: AppInfo API. Masaüstü uygulaması (Spotify.exe, Chrome): çalışan süreçten simge.
public static class UygulamaBilgisi
{
    private static readonly Dictionary<string, (string, ImageSource?)> Onbellek = new();

    public static async Task<(string Ad, ImageSource? Simge)> AlAsync(string aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return ("", null);
        lock (Onbellek) { if (Onbellek.TryGetValue(aumid, out var o)) return o; }

        (string, ImageSource?) sonuc = ("", null);
        try { sonuc = await PaketliAsync(aumid); } catch { }
        if (sonuc.Item2 == null)
        {
            try { sonuc = Masaustu(aumid, sonuc.Item1); } catch { }
        }
        if (string.IsNullOrEmpty(sonuc.Item1)) sonuc.Item1 = SadeAd(aumid);

        lock (Onbellek) { Onbellek[aumid] = sonuc; }
        return sonuc;
    }

    private static async Task<(string, ImageSource?)> PaketliAsync(string aumid)
    {
        if (!aumid.Contains('!')) return ("", null);
        var bilgi = Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(aumid);
        var ad = bilgi.DisplayInfo.DisplayName;
        var ref_ = bilgi.DisplayInfo.GetLogo(new Windows.Foundation.Size(64, 64));
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
        return (ad, bmp);
    }

    private static (string, ImageSource?) Masaustu(string aumid, string bilinenAd)
    {
        // "Spotify.exe" → Spotify, "Chrome" → chrome, "MSEdge" → msedge
        var ipucu = Path.GetFileNameWithoutExtension(aumid);
        var adaylar = new[] { ipucu, ipucu.ToLowerInvariant(), "ms" + ipucu.ToLowerInvariant() };
        Process? surec = null;
        foreach (var a in adaylar)
        {
            surec = Process.GetProcessesByName(a).FirstOrDefault(p => { try { return p.MainModule != null; } catch { return false; } });
            if (surec != null) break;
        }
        if (surec == null) return (bilinenAd, null);

        string yol = surec.MainModule!.FileName;
        string ad = bilinenAd;
        if (string.IsNullOrEmpty(ad))
        {
            try
            {
                var fv = FileVersionInfo.GetVersionInfo(yol);
                ad = fv.FileDescription ?? fv.ProductName ?? ipucu;
            }
            catch { ad = ipucu; }
        }

        using var ikon = System.Drawing.Icon.ExtractAssociatedIcon(yol);
        if (ikon == null) return (ad, null);
        var src = Imaging.CreateBitmapSourceFromHIcon(ikon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
        src.Freeze();
        return (ad, src);
    }

    private static string SadeAd(string aumid)
    {
        if (aumid.Contains('!'))
        {
            var paket = aumid.Split('!')[0];           // SpotifyAB.SpotifyMusic_zpdnekdrzrea0
            var govde = paket.Split('_')[0];           // SpotifyAB.SpotifyMusic
            return govde.Split('.')[^1];               // SpotifyMusic
        }
        return Path.GetFileNameWithoutExtension(aumid);
    }
}
