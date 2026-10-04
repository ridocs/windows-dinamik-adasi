using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace DinamikAda.Servisler;

/// Metin/bağlantıyı QR koduna çevirir. Bilgisayardaki bir linki ya da metni telefonla (iOS/Android)
/// okutup anında açmak/almak için. Saf C# (QRCoder), harici kurulum gerektirmez.
public static class QrServisi
{
    private static readonly byte[] Koyu = { 24, 24, 28 };      // modül rengi
    private static readonly byte[] Acik = { 245, 245, 248 };   // zemin

    public static BitmapSource? Uret(string metin, int modulPiksel = 8)
    {
        if (string.IsNullOrWhiteSpace(metin)) return null;
        try
        {
            using var gen = new QRCodeGenerator();
            var veri = gen.CreateQrCode(metin, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(veri);
            byte[] bayt = png.GetGraphic(modulPiksel, Koyu, Acik);

            var bmp = new BitmapImage();
            using var ms = new MemoryStream(bayt);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
