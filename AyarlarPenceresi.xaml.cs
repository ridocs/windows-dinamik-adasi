using System.Windows;
using Forms = System.Windows.Forms;

namespace DinamikAda;

public partial class AyarlarPenceresi : Window
{
    private readonly Ayarlar _ayar;
    private readonly Action<Ayarlar> _uygula;
    private bool _yukleniyor = true;

    public AyarlarPenceresi(Ayarlar mevcut, Action<Ayarlar> uygula)
    {
        InitializeComponent();
        _ayar = mevcut.Kopya();
        _uygula = uygula;

        var ekranlar = Forms.Screen.AllScreens;
        for (int i = 0; i < ekranlar.Length; i++)
        {
            var e = ekranlar[i];
            EkranKutu.Items.Add($"{i + 1}. ekran  ({e.Bounds.Width}×{e.Bounds.Height}{(e.Primary ? ", birincil" : "")})");
        }
        EkranKutu.SelectedIndex = Math.Clamp(_ayar.Ekran, 0, Math.Max(0, ekranlar.Length - 1));

        UstBosluk.Value = _ayar.UstBosluk;
        Olcek.Value = _ayar.Olcek;
        Saydamlik.Value = _ayar.Saydamlik;
        TamEkrandaGizle.IsChecked = _ayar.TamEkrandaGizle;
        OyunKatmaniAcik.IsChecked = _ayar.OyunKatmaniAcik;
        WindowsIleBaslat.IsChecked = Baslangic.Acik;

        SesAcik.IsChecked = _ayar.SesAcik;
        KulaklikAcik.IsChecked = _ayar.KulaklikAcik;
        PilAcik.IsChecked = _ayar.PilAcik;
        MikrofonKameraAcik.IsChecked = _ayar.MikrofonKameraAcik;
        BildirimAcik.IsChecked = _ayar.BildirimAcik;
        HavaAcik.IsChecked = _ayar.HavaAcik;
        ToplantiModuAcik.IsChecked = _ayar.ToplantiModuAcik;
        AgAcik.IsChecked = _ayar.AgAcik;
        DuzenListesiDoldur();
        SozlerAcik.IsChecked = _ayar.SozlerAcik;
        ClaudeAcik.IsChecked = _ayar.ClaudeAcik;
        ClaudeApiKey.Text = _ayar.ClaudeApiKey;
        ClaudeModel.Text = _ayar.ClaudeModel;
        ClaudeNot.Text = _ayar.ClaudeNot;
        ClaudeKaynakMetin.Text = Servisler.ClaudeServisi.CliVar ? "Bu makinede Claude Code bulundu; anahtar girmeden çalışır." : "Claude Code bulunamadı; çalışması için API anahtarı gerekir.";
        SpotifyClientId.Text = _ayar.SpotifyClientId;
        SpotifyDurumGoster();
        HavaSehir.Text = _ayar.HavaSehir;
        PomodoroDakika.Value = _ayar.PomodoroDakika;
        DuyuruSaniye.Value = _ayar.DuyuruSaniye;

        SurukleHedef.Text = _ayar.SurukleHedef;
        SuruklePort.Text = _ayar.SuruklePort.ToString();
        SurukleAnahtar.Text = _ayar.SurukleAnahtar;
        CanavarAcik.IsChecked = _ayar.CanavarAcik;
        UykuDakika.Value = _ayar.UykuDakika;
        MolaDakika.Value = _ayar.MolaDakika;
        DogumGunu.Text = _ayar.DogumGunu;
        OzetAcik.IsChecked = _ayar.OzetAcik;
        OzetSaat.Text = _ayar.OzetSaat;

        SecimAyarla(HazneModu, _ayar.HazneModu);
        SecimAyarla(HazneGorunum, _ayar.HazneGorunum);
        SecimAyarla(HazneSurukleEtkisi, _ayar.HazneSurukleEtkisi);
        LinkHedef.Text = _ayar.LinkHedef;
        LinkOnek.Text = _ayar.LinkOnek;
        Kisayollar.Text = _ayar.Kisayollar;
        Rehber.Text = _ayar.Rehber;
        WhatsAppOtomatikGonder.IsChecked = _ayar.WhatsAppOtomatikGonder;
        WaServisAcik.IsChecked = _ayar.WaServisAcik;
        RehberDosyasi.Text = _ayar.RehberDosyasi;
        SunucuAcik.IsChecked = _ayar.SunucuAcik;
        SunucuAdres.Text = _ayar.SunucuAdres;
        SunucuSshKullanici.Text = _ayar.SunucuSshKullanici;
        SunucuAnahtar.Text = _ayar.SunucuAnahtar;
        SunucuUrller.Text = _ayar.SunucuUrller;

        _yukleniyor = false;
        Deger_Degisti(this, null!);

        // Kapsülün bulunduğu ekranın ortasında aç (oyun başka ekrandaysa onu rahatsız etmesin)
        Loaded += (_, _) => EkranaOrtala(ekranlar, _ayar.Ekran);
        SourceInitialized += (_, _) => KoyuBaslikCubugu();
    }

    /// Sol menüde seçilen bölümün panelini göster, diğerlerini gizle
    private void Bolum_Secildi(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Icerik == null) return;
        var paneller = new FrameworkElement[] { BolumGenel, BolumModuller, BolumMuzik, BolumClaude, BolumHazne, BolumKisayol, BolumWhatsApp, BolumSunucu, BolumCanavar };   // sol menü sırasıyla aynı
        int secili = Menu.SelectedIndex;
        for (int i = 0; i < paneller.Length; i++)
            paneller[i].Visibility = i == secili ? Visibility.Visible : Visibility.Collapsed;
        Kaydirici?.ScrollToTop();
    }

    /// Windows 11 başlık çubuğunu koyu yap (DWMWA_USE_IMMERSIVE_DARK_MODE)
    private void KoyuBaslikCubugu()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int acik = 1;
            DwmSetWindowAttribute(hwnd, 20, ref acik, sizeof(int));
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private void EkranaOrtala(Forms.Screen[] ekranlar, int dizin)
    {
        if (ekranlar.Length == 0) return;
        var e = ekranlar[Math.Clamp(dizin, 0, ekranlar.Length - 1)];
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Round(ActualWidth * dpi.DpiScaleX), h = (int)Math.Round(ActualHeight * dpi.DpiScaleY);
        int x = e.Bounds.X + (e.Bounds.Width - w) / 2, y = e.Bounds.Y + (e.Bounds.Height - h) / 2;
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); // NOSIZE | NOZORDER | NOACTIVATE
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private void Deger_Degisti(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_yukleniyor) return;
        UstBoslukMetin.Text = $"{(int)UstBosluk.Value}";
        OlcekMetin.Text = Olcek.Value.ToString("0.00");
        SaydamlikMetin.Text = $"{(int)Math.Round(Saydamlik.Value * 100)}%";
        PomodoroMetin.Text = $"{(int)PomodoroDakika.Value}";
        DuyuruMetin.Text = $"{(int)DuyuruSaniye.Value}";
        UykuMetin.Text = $"{(int)UykuDakika.Value}";
        MolaMetin.Text = (int)MolaDakika.Value == 0 ? "kapalı" : $"{(int)MolaDakika.Value}";
    }

    private void Kaydet_Click(object sender, RoutedEventArgs e)
    {
        _ayar.Ekran = Math.Max(0, EkranKutu.SelectedIndex);
        _ayar.UstBosluk = Math.Round(UstBosluk.Value);
        _ayar.Olcek = Math.Round(Olcek.Value, 2);
        _ayar.Saydamlik = Math.Round(Saydamlik.Value, 2);
        _ayar.TamEkrandaGizle = TamEkrandaGizle.IsChecked == true;
        _ayar.OyunKatmaniAcik = OyunKatmaniAcik.IsChecked == true;
        _ayar.WindowsIleBaslat = WindowsIleBaslat.IsChecked == true;

        _ayar.SesAcik = SesAcik.IsChecked == true;
        _ayar.KulaklikAcik = KulaklikAcik.IsChecked == true;
        _ayar.PilAcik = PilAcik.IsChecked == true;
        _ayar.MikrofonKameraAcik = MikrofonKameraAcik.IsChecked == true;
        _ayar.BildirimAcik = BildirimAcik.IsChecked == true;
        _ayar.HavaAcik = HavaAcik.IsChecked == true;
        _ayar.ToplantiModuAcik = ToplantiModuAcik.IsChecked == true;
        _ayar.AgAcik = AgAcik.IsChecked == true;
        _ayar.SozlerAcik = SozlerAcik.IsChecked == true;
        _ayar.ClaudeAcik = ClaudeAcik.IsChecked == true;
        _ayar.ClaudeApiKey = ClaudeApiKey.Text.Trim();
        _ayar.ClaudeModel = ClaudeModel.Text.Trim();
        _ayar.ClaudeNot = ClaudeNot.Text.Trim();
        _ayar.SpotifyClientId = SpotifyClientId.Text.Trim();
        _ayar.HavaSehir = HavaSehir.Text.Trim();
        _ayar.PomodoroDakika = (int)PomodoroDakika.Value;
        _ayar.DuyuruSaniye = (int)DuyuruSaniye.Value;

        _ayar.SurukleHedef = SurukleHedef.Text.Trim();
        _ayar.SuruklePort = int.TryParse(SuruklePort.Text.Trim(), out var port) && port > 0 && port < 65536 ? port : 22;
        _ayar.SurukleAnahtar = SurukleAnahtar.Text.Trim();
        _ayar.CanavarAcik = CanavarAcik.IsChecked == true;
        _ayar.UykuDakika = (int)UykuDakika.Value;
        _ayar.MolaDakika = (int)MolaDakika.Value;
        _ayar.DogumGunu = DogumGunu.Text.Trim();
        _ayar.OzetAcik = OzetAcik.IsChecked == true;
        _ayar.OzetSaat = System.Text.RegularExpressions.Regex.IsMatch(OzetSaat.Text.Trim(), @"^\d{2}:\d{2}$") ? OzetSaat.Text.Trim() : "21:00";

        _ayar.HazneModu = Secim(HazneModu, "hazne");
        _ayar.HazneGorunum = Secim(HazneGorunum, "liste");
        _ayar.HazneSurukleEtkisi = Secim(HazneSurukleEtkisi, "otomatik");
        _ayar.LinkHedef = LinkHedef.Text.Trim();
        _ayar.LinkOnek = LinkOnek.Text.Trim();
        _ayar.Kisayollar = Kisayollar.Text.Trim();
        _ayar.Rehber = Rehber.Text.Trim();
        _ayar.WhatsAppOtomatikGonder = WhatsAppOtomatikGonder.IsChecked == true;
        _ayar.WaServisAcik = WaServisAcik.IsChecked == true;
        _ayar.RehberDosyasi = RehberDosyasi.Text.Trim();
        _ayar.SunucuAcik = SunucuAcik.IsChecked == true;
        _ayar.SunucuAdres = SunucuAdres.Text.Trim();
        _ayar.SunucuSshKullanici = SunucuSshKullanici.Text.Trim();
        _ayar.SunucuAnahtar = SunucuAnahtar.Text.Trim();
        _ayar.SunucuUrller = SunucuUrller.Text.Trim();

        Baslangic.Ayarla(_ayar.WindowsIleBaslat);
        try { _ayar.Kaydet(); } catch { }
        _uygula(_ayar);
        Close();
    }

    private void Iptal_Click(object sender, RoutedEventArgs e) => Close();

    private void SpotifyDurumGoster()
    {
        bool bagli = _ayar.SpotifyRefreshToken.Length > 0;
        SpotifyDurumMetin.Text = bagli ? $"Bağlı{(_ayar.SpotifyKullanici.Length > 0 ? ": " + _ayar.SpotifyKullanici : "")} (Kaydet ile kalıcı olur)" : "Bağlı değil";
        SpotifyKopar.Visibility = bagli ? Visibility.Visible : Visibility.Collapsed;
        SpotifyBaglanDugme.Content = bagli ? "Yeniden bağlan" : "Spotify'a bağlan";
    }

    private async void SpotifyBaglan_Click(object sender, RoutedEventArgs e)
    {
        SpotifyBaglanDugme.IsEnabled = false;
        SpotifyDurumMetin.Text = "Tarayıcıda Spotify açıldı, izin verin…";
        var (ok, refresh, kullanici, hata) = await Servisler.SpotifyServisi.BaglanAsync(SpotifyClientId.Text);
        SpotifyBaglanDugme.IsEnabled = true;
        if (ok) { _ayar.SpotifyRefreshToken = refresh; _ayar.SpotifyKullanici = kullanici; _ayar.SpotifyClientId = SpotifyClientId.Text.Trim(); SpotifyDurumGoster(); }
        else SpotifyDurumMetin.Text = "Bağlanamadı: " + hata;
    }

    private void SpotifyKopar_Click(object sender, RoutedEventArgs e)
    {
        _ayar.SpotifyRefreshToken = ""; _ayar.SpotifyKullanici = "";
        SpotifyDurumGoster();
    }

    private void DuzenListesiDoldur()
    {
        DuzenListe.Items.Clear();
        foreach (var d in _ayar.Duzenler) DuzenListe.Items.Add($"{d.Ad}  ({d.Pencereler.Count} pencere)");
    }

    /// Kopya listede sil: paylaşılan listeyi değil yeni listeyi yaz, İptal edilirse eskisi kalsın
    private void DuzenSil_Click(object sender, RoutedEventArgs e)
    {
        int i = DuzenListe.SelectedIndex;
        if (i < 0 || i >= _ayar.Duzenler.Count) return;
        _ayar.Duzenler = _ayar.Duzenler.Where((_, n) => n != i).ToList();
        DuzenListesiDoldur();
    }

    private static void SecimAyarla(System.Windows.Controls.ComboBox kutu, string deger)
    {
        for (int i = 0; i < kutu.Items.Count; i++)
            if (kutu.Items[i] is System.Windows.Controls.ComboBoxItem it && (it.Tag as string) == deger) { kutu.SelectedIndex = i; return; }
        kutu.SelectedIndex = 0;
    }

    private static string Secim(System.Windows.Controls.ComboBox kutu, string varsayilan) =>
        (kutu.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? varsayilan;
}
