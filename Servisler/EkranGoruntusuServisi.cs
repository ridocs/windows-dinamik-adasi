using System.IO;
using System.Runtime.InteropServices;
using Drawing = System.Drawing;

namespace DinamikAda.Servisler;

/// Ön plandaki pencereyi ya da tüm ekranı PNG olarak yakalar. Dosyayı Resimler\DinamikAda altına
/// kaydeder ve yolunu döndürür. Hazneye eklenip sunucuya yüklenerek link/QR ile paylaşılabilir.
public static class EkranGoruntusuServisi
{
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT r, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; public int W => Right - Left; public int H => Bottom - Top; }

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    /// Verilen pencereyi yakala; geçersiz/küçültülmüşse tüm ekranı yakala. Dönüş: kaydedilen dosya yolu ya da null.
    public static string? Yakala(IntPtr pencere = default)
    {
        try
        {
            RECT r = default;
            bool pencereGecerli = pencere != IntPtr.Zero && !IsIconic(pencere)
                && (DwmGetWindowAttribute(pencere, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf<RECT>()) == 0 && r.W > 0 && r.H > 0
                    || GetWindowRect(pencere, out r) && r.W > 0 && r.H > 0);

            Drawing.Rectangle alan;
            if (pencereGecerli)
                alan = new Drawing.Rectangle(r.Left, r.Top, r.W, r.H);
            else
            {
                var sb = System.Windows.Forms.SystemInformation.VirtualScreen;   // tüm monitörler
                alan = new Drawing.Rectangle(sb.Left, sb.Top, sb.Width, sb.Height);
            }

            if (alan.Width <= 0 || alan.Height <= 0) return null;

            using var bmp = new Drawing.Bitmap(alan.Width, alan.Height, Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(alan.Left, alan.Top, 0, 0, alan.Size, Drawing.CopyPixelOperation.SourceCopy);

            string klasor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DinamikAda");
            Directory.CreateDirectory(klasor);
            string yol = Path.Combine(klasor, "ekran-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            bmp.Save(yol, Drawing.Imaging.ImageFormat.Png);
            return File.Exists(yol) ? yol : null;
        }
        catch { return null; }
    }
}
