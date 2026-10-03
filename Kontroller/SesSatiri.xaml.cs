using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DinamikAda.Servisler;

namespace DinamikAda.Kontroller;

public partial class SesSatiri : UserControl
{
    private SesServisi? _ses;
    private bool _surukleniyor;
    private float _sonSeviye;
    private bool _sonSessiz;

    public SesSatiri()
    {
        InitializeComponent();
        // Ray yerleşmeden (genişlik 0) hesaplanan dolgu boş kalıyordu; boyut değişince son seviyeyi yeniden uygula
        Ray.SizeChanged += (_, _) => Guncelle(_sonSeviye, _sonSessiz);
    }

    public void Bagla(SesServisi ses) { _ses = ses; Guncelle(ses.Seviye, ses.Sessiz); }

    public void Guncelle(float seviye, bool sessiz)
    {
        _sonSeviye = seviye; _sonSessiz = sessiz;
        double icGenislik = Math.Max(0, Ray.ActualWidth);
        Dolgu.Width = icGenislik * Math.Clamp(seviye, 0, 1);
        Yuzde.Text = sessiz ? "sessiz" : $"%{(int)Math.Round(seviye * 100)}";
        SessizDugme.Content = sessiz || seviye <= 0 ? "" : seviye < 0.34 ? "" : seviye < 0.67 ? "" : "";
        Dolgu.Opacity = sessiz ? 0.35 : 1;
    }

    private void Sessiz_Click(object sender, RoutedEventArgs e)
    {
        _ses?.SessizDegistir();
        if (_ses != null) Guncelle(_ses.Seviye, _ses.Sessiz);
    }

    private void Ray_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _surukleniyor = true;
        Ray.CaptureMouse();
        Uygula(e.GetPosition(Ray).X);
    }

    private void Ray_MouseMove(object sender, MouseEventArgs e)
    {
        if (_surukleniyor) Uygula(e.GetPosition(Ray).X);
    }

    private void Ray_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _surukleniyor = false;
        Ray.ReleaseMouseCapture();
    }

    private void Uygula(double x)
    {
        if (_ses == null || Ray.ActualWidth <= 0) return;
        float oran = (float)Math.Clamp(x / Ray.ActualWidth, 0, 1);
        _ses.SeviyeAyarla(oran);
        Guncelle(oran, false);
    }
}
