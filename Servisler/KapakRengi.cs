using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DinamikAda.Servisler;

/// Kapaktan baskın rengi çıkarır: doygun ve orta parlaklıktaki piksellerin ortalaması.
/// Kapsülün koyu zemininde okunur kalsın diye parlaklığı yukarı çekilir.
public static class KapakRengi
{
    public static Color Hesapla(BitmapSource kaynak, Color varsayilan)
    {
        try
        {
            // Küçült: 32x32 yeter, hızlıdır
            var kucuk = new TransformedBitmap(kaynak, new ScaleTransform(32.0 / kaynak.PixelWidth, 32.0 / kaynak.PixelHeight));
            var fb = new FormatConvertedBitmap(kucuk, PixelFormats.Bgra32, null, 0);
            int w = fb.PixelWidth, h = fb.PixelHeight, adim = w * 4;
            var px = new byte[adim * h];
            fb.CopyPixels(px, adim, 0);

            double r = 0, g = 0, b = 0; int n = 0;
            double r2 = 0, g2 = 0, b2 = 0; int n2 = 0; // yedek: tüm pikseller
            for (int i = 0; i < px.Length; i += 4)
            {
                byte bb = px[i], gg = px[i + 1], rr = px[i + 2];
                int max = Math.Max(rr, Math.Max(gg, bb)), min = Math.Min(rr, Math.Min(gg, bb));
                double doygunluk = max == 0 ? 0 : (max - min) / (double)max;
                r2 += rr; g2 += gg; b2 += bb; n2++;
                if (doygunluk > 0.3 && max > 60 && max < 245)
                {
                    r += rr; g += gg; b += bb; n++;
                }
            }
            if (n < 8) { if (n2 == 0) return varsayilan; r = r2; g = g2; b = b2; n = n2; }

            var renk = Color.FromRgb((byte)(r / n), (byte)(g / n), (byte)(b / n));
            return Aydinlat(renk, 0.62);
        }
        catch { return varsayilan; }
    }

    /// Rengin en parlak bileşenini hedefe (0..1) çeker; koyu kapaklar da görünür olsun.
    private static Color Aydinlat(Color c, double hedefParlaklik)
    {
        int max = Math.Max(c.R, Math.Max(c.G, c.B));
        if (max == 0) return c;
        double carpan = hedefParlaklik * 255 / max;
        if (carpan < 1) return c; // zaten parlak
        return Color.FromRgb(
            (byte)Math.Min(255, c.R * carpan),
            (byte)Math.Min(255, c.G * carpan),
            (byte)Math.Min(255, c.B * carpan));
    }
}
