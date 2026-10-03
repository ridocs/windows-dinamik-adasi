using System.Windows;
using System.Windows.Input;

namespace DinamikAda.Kontroller;

/// Tek satırlık ad sorma penceresi (düzen kaydetme). Enter = Kaydet, Esc = İptal.
public partial class AdSor : Window
{
    public string Ad => Kutu.Text.Trim();

    public AdSor(System.Windows.Forms.Screen ekran, string baslik, string ozet, string varsayilan = "")
    {
        InitializeComponent();
        Title = baslik;
        Ozet.Text = ozet;
        Kutu.Text = varsayilan;
        Loaded += (_, _) =>
        {
            EkranaOrtala(ekran);
            Kutu.Focus(); Kutu.SelectAll();
        };
    }

    private void EkranaOrtala(System.Windows.Forms.Screen e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Round(ActualWidth * dpi.DpiScaleX), h = (int)Math.Round(ActualHeight * dpi.DpiScaleY);
        SetWindowPos(hwnd, IntPtr.Zero, e.Bounds.X + (e.Bounds.Width - w) / 2, e.Bounds.Y + (e.Bounds.Height - h) / 2, 0, 0, 0x0001 | 0x0004);
    }

    private void Kutu_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Tamam_Click(sender, e);
        else if (e.Key == Key.Escape) Iptal_Click(sender, e);
    }

    private void Tamam_Click(object sender, RoutedEventArgs e)
    {
        if (Ad.Length == 0) { Kutu.Focus(); return; }
        DialogResult = true;
    }

    private void Iptal_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
