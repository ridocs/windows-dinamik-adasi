using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DinamikAda.Servisler;

namespace DinamikAda.Kontroller;

/// Kurulu uygulamalardan seçim: arama kutusu, simgeli liste; "Dosyadan seç" yedek.
public partial class UygulamaSecici : Window
{
    public sealed class Satir : INotifyPropertyChanged
    {
        public string Ad { get; init; } = "";
        public string Hedef { get; init; } = "";
        public string Tur { get; init; } = "";
        public bool Paketli { get; init; }
        private ImageSource? _simge;
        public ImageSource? Simge { get => _simge; set { _simge = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Simge))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly List<Satir> _hepsi = new();
    private readonly ObservableCollection<Satir> _gorunen = new();
    private readonly Dictionary<string, ImageSource?> _simgeOnbellek = new(StringComparer.OrdinalIgnoreCase);

    public (string Ad, string Hedef)? Secim { get; private set; }

    public UygulamaSecici(System.Windows.Forms.Screen ekran)
    {
        InitializeComponent();
        Liste.ItemsSource = _gorunen;
        Liste.SelectionChanged += (_, _) => EkleDugme.IsEnabled = Liste.SelectedItem != null;
        Loaded += async (_, _) =>
        {
            EkranaOrtala(ekran);
            Arama.Focus();
            await YukleAsync();
        };
    }

    private async Task YukleAsync()
    {
        Sayac.Text = "Uygulamalar taranıyor…";
        var liste = await Task.Run(KuruluUygulamalar.Listele);
        foreach (var u in liste)
            _hepsi.Add(new Satir { Ad = u.Ad, Hedef = u.Hedef, Tur = u.Tur, Paketli = u.Paketli });
        Suz();

        // Simgeler arka planda: exe'ler hızlı, paket logoları tek tek
        foreach (var s in _hepsi)
        {
            if (!IsLoaded) return;
            try
            {
                if (!_simgeOnbellek.TryGetValue(s.Hedef, out var simge))
                {
                    simge = s.Paketli
                        ? (await UygulamaBilgisi.AlAsync(s.Hedef["shell:AppsFolder\\".Length..])).Simge
                        : await Task.Run(() => HazneDeposu.DosyaSimgesi(s.Hedef));
                    _simgeOnbellek[s.Hedef] = simge;
                }
                s.Simge = simge;
            }
            catch { }
        }
    }

    private void Suz()
    {
        string q = Arama.Text.Trim();
        AramaIpucu.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _gorunen.Clear();
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        foreach (var s in _hepsi)
            if (q.Length == 0 || tr.CompareInfo.IndexOf(s.Ad, q, System.Globalization.CompareOptions.IgnoreCase) >= 0)
                _gorunen.Add(s);
        Sayac.Text = $"{_gorunen.Count} uygulama";
        if (_gorunen.Count > 0 && q.Length > 0) Liste.SelectedIndex = 0;
    }

    private void Arama_TextChanged(object sender, TextChangedEventArgs e) => Suz();

    private void Liste_DoubleClick(object sender, MouseButtonEventArgs e) => Ekle_Click(sender, e);

    private void Ekle_Click(object sender, RoutedEventArgs e)
    {
        if (Liste.SelectedItem is not Satir s) return;
        Secim = (s.Ad, s.Hedef);
        DialogResult = true;
        Close();
    }

    private void Dosya_Click(object sender, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Kısayol çubuğuna ekle",
            Filter = "Uygulama ve kısayol|*.exe;*.lnk;*.url;*.bat;*.cmd|Tüm dosyalar|*.*",
            CheckFileExists = true,
        };
        if (d.ShowDialog(this) != true) return;
        string yol = d.FileName, ad = System.IO.Path.GetFileNameWithoutExtension(yol);
        try
        {
            var fv = System.Diagnostics.FileVersionInfo.GetVersionInfo(yol);
            if (!string.IsNullOrWhiteSpace(fv.FileDescription) && fv.FileDescription.Length <= 24) ad = fv.FileDescription;
        }
        catch { }
        Secim = (ad, yol);
        DialogResult = true;
        Close();
    }

    private void Iptal_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; Close(); }
        else if (e.Key == Key.Enter && Liste.SelectedItem != null) Ekle_Click(this, e);
        else if (e.Key == Key.Down && Arama.IsKeyboardFocused && _gorunen.Count > 0) { Liste.Focus(); Liste.SelectedIndex = Math.Max(0, Liste.SelectedIndex); }
        base.OnKeyDown(e);
    }

    private void EkranaOrtala(System.Windows.Forms.Screen e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Round(ActualWidth * dpi.DpiScaleX), h = (int)Math.Round(ActualHeight * dpi.DpiScaleY);
        SetWindowPos(hwnd, IntPtr.Zero, e.Bounds.X + (e.Bounds.Width - w) / 2, e.Bounds.Y + (e.Bounds.Height - h) / 2, 0, 0, 0x0001 | 0x0004);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
