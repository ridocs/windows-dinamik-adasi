using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DinamikAda.Servisler;

namespace DinamikAda.Kontroller;

/// Hazne paneli: liste/ızgara görünümü, düğmeler, hazneden dışarı sürükleme. İşi yapmaz, olay yükseltir.
public partial class HaznePaneli : UserControl
{
    // Izgara kutucuğu 108 + 8 kenar boşluğu; en fazla 5 yan yana, 4 satır görünür, gerisi kaydırılır
    private const double KutuGenislik = 116, KutuYukseklik = 100, ListeSatir = 42;
    private const int EnCokSutun = 5, EnAzSutun = 3, GorunurSatir = 4;
    private const double YanBosluk = 32 + 12;   // panel kenarları + kaydırma çubuğu payı

    private HazneDeposu? _depo;
    private string _gorunum = "liste";
    private Point _basma;
    private HazneOgesi? _basilan;

    /// "otomatik" | "tasi" | "kopyala" | "kisayol"
    public string SurukleEtkisi { get; set; } = "otomatik";

    public event Action<HazneOgesi?>? YukleIstendi;    // null: hepsi
    public event Action<HazneOgesi?>? LinkIstendi;     // null: hepsi
    public event Action? ZipIstendi;
    public event Action? PanoIstendi;
    public event Action? GeriIstendi;                   // saat / kısayol paneline dön
    public event Action<string>? GorunumDegisti;
    public event Action? TelefonaIstendi;               // hepsini WhatsApp ile kendine gönder
    public event Action? ClaudeIstendi;                 // metin dosyalarını Claude'a özetlet
    public event Action? Degisti;                       // içerik değişti: üst pencere boyutu yenilesin

    public HaznePaneli() { InitializeComponent(); Width = 380; }

    public void Bagla(HazneDeposu depo, string gorunum)
    {
        _depo = depo;
        Liste.ItemsSource = depo.Ogeler;
        depo.Ogeler.CollectionChanged += (_, _) => { Yenile(); Degisti?.Invoke(); };
        GorunumAyarla(gorunum);
    }

    public string Gorunum => _gorunum;
    public void GorunumAyarla(string gorunum)
    {
        _gorunum = gorunum == "izgara" ? "izgara" : "liste";
        bool izgara = _gorunum == "izgara";
        Liste.ItemTemplate = (DataTemplate)Resources[izgara ? "IzgaraSablon" : "ListeSablon"];
        Liste.ItemsPanel = izgara
            ? new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)))
            : new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
        Yenile();
    }

    private void Yenile()
    {
        int n = _depo?.Ogeler.Count ?? 0;
        Sayac.Text = n.ToString();
        Bos.Visibility = n == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var b in new[] { ZipDugme, LinkDugme, YukleDugme, TemizleDugme, TelefonDugme, ClaudeDugme }) b.IsEnabled = n > 0;
        GorunumDugme.Content = ((char)(_gorunum == "izgara" ? 0xE8FD : 0xF0E2)).ToString();

        if (_gorunum == "izgara")
        {
            int sutun = Math.Clamp(n, EnAzSutun, EnCokSutun);
            Width = sutun * KutuGenislik - 8 + YanBosluk;
            Kaydirici.MaxHeight = GorunurSatir * KutuYukseklik;
        }
        else
        {
            Width = 380;
            Kaydirici.MaxHeight = GorunurSatir * ListeSatir + 8;
        }

        Ipucu.Text = SurukleEtkisi switch
        {
            "kisayol" => "Ctrl+V panodan yapıştırır · sürükleyince kısayol oluşur",
            "kopyala" => "Ctrl+V panodan yapıştırır · sürükleyince kopyalar",
            "tasi" => "Ctrl+V panodan yapıştırır · sürükleyince taşır",
            _ => "Ctrl+V panodan yapıştırır · sürükle: taşır · Ctrl: kopyalar · Alt: kısayol",
        };
    }

    public void AyarlariYenile() => Yenile();

    private static HazneOgesi? Oge(object sender) => (sender as FrameworkElement)?.Tag as HazneOgesi ?? (sender as FrameworkElement)?.DataContext as HazneOgesi;

    private void OgeYukle_Click(object sender, RoutedEventArgs e) { e.Handled = true; YukleIstendi?.Invoke(Oge(sender)); }
    private void OgeLink_Click(object sender, RoutedEventArgs e) { e.Handled = true; LinkIstendi?.Invoke(Oge(sender)); }
    private void OgeSil_Click(object sender, RoutedEventArgs e) { e.Handled = true; var o = Oge(sender); if (o != null) _depo?.Cikar(o); }
    private void Yukle_Click(object sender, RoutedEventArgs e) => YukleIstendi?.Invoke(null);
    private void Link_Click(object sender, RoutedEventArgs e) => LinkIstendi?.Invoke(null);
    private void Zip_Click(object sender, RoutedEventArgs e) => ZipIstendi?.Invoke();
    private void Pano_Click(object sender, RoutedEventArgs e) => PanoIstendi?.Invoke();
    private void Geri_Click(object sender, RoutedEventArgs e) => GeriIstendi?.Invoke();
    private void Gorunum_Click(object sender, RoutedEventArgs e) { GorunumAyarla(_gorunum == "izgara" ? "liste" : "izgara"); GorunumDegisti?.Invoke(_gorunum); }
    private void Telefon_Click(object sender, RoutedEventArgs e) => TelefonaIstendi?.Invoke();
    private void Claude_Click(object sender, RoutedEventArgs e) => ClaudeIstendi?.Invoke();
    private void Temizle_Click(object sender, RoutedEventArgs e) => _depo?.Temizle();

    // ---- hazneden dışarı sürükleme (başka pencereye bırakma) ----

    private void Oge_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && DugmeIcinde(d)) { _basilan = null; return; }
        _basma = e.GetPosition(this);
        _basilan = (sender as FrameworkElement)?.DataContext as HazneOgesi;
    }

    private void Oge_MouseMove(object sender, MouseEventArgs e)
    {
        if (_basilan == null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _basma.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _basma.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var oge = _basilan;
        _basilan = null;
        var veri = new DataObject(DataFormats.FileDrop, new[] { oge.Yol });
        // Hedef (Explorer) izin verilen etkiler içinden seçer: Alt → kısayol, Ctrl → kopya, aksi hâlde taşı/kopya kuralı
        var etkiler = SurukleEtkisi switch
        {
            "kisayol" => DragDropEffects.Link,
            "kopyala" => DragDropEffects.Copy,
            "tasi" => DragDropEffects.Move,
            _ => DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link,
        };
        var sonuc = DragDrop.DoDragDrop((DependencyObject)sender, veri, etkiler);
        if (sonuc == DragDropEffects.Move && !System.IO.File.Exists(oge.Yol) && !System.IO.Directory.Exists(oge.Yol))
            _depo?.Cikar(oge);   // gerçekten taşındıysa hazneden düş
    }

    private static bool DugmeIcinde(DependencyObject d)
    {
        for (var k = d; k != null; k = System.Windows.Media.VisualTreeHelper.GetParent(k))
            if (k is Button) return true;
        return false;
    }
}
