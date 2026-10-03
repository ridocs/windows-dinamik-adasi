using System.Windows;
using System.Windows.Controls;
using DinamikAda.Servisler;

namespace DinamikAda.Kontroller;

public partial class PomodoroSatiri : UserControl
{
    private Pomodoro? _p;

    public PomodoroSatiri() { InitializeComponent(); }

    public void Bagla(Pomodoro p) { _p = p; Guncelle(); }

    public void Guncelle()
    {
        if (_p == null) return;
        Sure.Text = _p.Metin;
        BaslatDugme.Content = _p.Calisiyor ? "" : "";
        Durum.Text = !_p.Aktif ? "hazır" : _p.Calisiyor ? "odaklanma" : "duraklatıldı";
    }

    private void Baslat_Click(object sender, RoutedEventArgs e) { _p?.BaslatDuraklat(); Guncelle(); }
    private void Sifirla_Click(object sender, RoutedEventArgs e) { _p?.Sifirla(); Guncelle(); }
    private void Ekle_Click(object sender, RoutedEventArgs e) { _p?.Ekle(5); Guncelle(); }
}
