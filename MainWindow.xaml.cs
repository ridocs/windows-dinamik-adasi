using System.Globalization;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DinamikAda.Servisler;
using Forms = System.Windows.Forms;

namespace DinamikAda;

public partial class MainWindow : Window
{
    // Kapsül ölçüleri (DIP, ölçek uygulanmadan önce)
    private const double KompaktYukseklik = 36;
    private const double KompaktIkiSatirYukseklik = 54;
    private const double KompaktMedyaGenislik = 224;
    private const double GenisMedyaGenislik = 380, GenisMedyaYukseklik = 142;   // XAML Height ile aynı; söz satırı +24
    private const double GenisBosGenislik = 380, GenisBosYukseklik = 222;   // XAML Height ile aynı tutulmalı

    private Ayarlar _ayar;

    private readonly MedyaServisi _medya = new();
    private readonly SesServisi _ses = new();
    private readonly PilServisi _pil = new();
    private readonly GizlilikServisi _gizlilik = new();
    private readonly BildirimServisi _bildirim = new();
    private readonly HavaServisi _hava = new();
    private readonly Pomodoro _pomodoro = new();
    private readonly DuyuruKuyrugu _kuyruk = new();
    private readonly EtkinlikServisi _etkinlik = new();
    private readonly HazneDeposu _hazne = new();           // bırakılan dosyalar
    private bool _hazneGoster;                              // ataç düğmesiyle hazne istendi (daralınca sıfırlanır)
    private DateTime _hazneSonEkleme = DateTime.MinValue;   // son bırakma: 1 dk boyunca hover hazneyi açar
    private readonly SistemServisi _sistem = new();         // CPU / RAM halkaları
    private readonly AgServisi _ag = new();                 // ağ hızı + Tailscale
    private readonly SozServisi _soz = new();               // şarkı sözleri (lrclib)
    private readonly GpuServisi _gpu = new();               // oyun katmanı: GPU sıcaklık/yük
    private readonly ClaudeServisi _claude = new();         // Claude'a sor
    private readonly SesEfektServisi _efekt = new();        // arayüz ses efektleri
    // Oyun oturumu, ses profili, izleme modu
    private DateTime _oyunBaslangic = DateTime.MinValue;
    private int _oyunGpuTepe = -1;
    private string _oyunSurec = "";
    private float _oyunMuzikEski = -1f;                                  // Spotify sesi (profil geri alınsın)
    private readonly List<KarisimServisi.Oturum> _oyunYonlendirilen = new();
    private DateTime _videoBaslangic = DateTime.MinValue;
    private string _videoBaslik = "";
    private bool _sesSeridi;                                             // tam ekran videoda ses değişince 2 sn şerit
    private readonly DispatcherTimer _sesSeridiZaman = new() { Interval = TimeSpan.FromMilliseconds(2200) };
    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint esFlags);
    private const uint ES_CONTINUOUS = 0x80000000, ES_DISPLAY_REQUIRED = 0x00000002;
    private bool _videoAktif;
    private string _sesSeridiMetin = "";
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
    private readonly KarisimServisi _karisim = new();       // uygulama başına ses ve çıkış aygıtı
    private readonly SesOsdServisi _sesOsd = new();         // Windows ses barını gizle
    private readonly DispatcherTimer _osdZaman = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private int _osdSayac;
    private readonly System.Collections.ObjectModel.ObservableCollection<KarisimServisi.Oturum> _karisimListe = new();
    private readonly DispatcherTimer _karisimZaman = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _karisimGoster, _karisimYenileniyor;
    private readonly DispatcherTimer _sesRozetZaman = new() { Interval = TimeSpan.FromMilliseconds(1600) };   // medya panelinde ses rozeti
    private readonly OzetServisi _ozet = new();             // günün özeti + kesintisiz çalışma sayacı
    private string _ozetGosterildi = "";
    private string _haftaGosterildi = "";                    // "yyyy-MM-dd": o günün akşam özeti gösterildi
    private DateTime _sonMolaUyari = DateTime.MinValue;
    private bool _yorgun;                                   // mola hatırlatıldı, henüz ara verilmedi
    private string _sapkaGunu = "";                         // şapka hesabı günde bir
    private bool _soruGoster, _soruBekliyor;                // soru paneli istendi / yanıt bekleniyor
    private DateTime _soruSonKullanim = DateTime.MinValue;
    private IntPtr _onPlanOnceki;                           // fare kapsüle gelmeden önceki ön plan penceresi (seçili metin için)
    private bool _mini;                                     // oyun katmanı etkin (tam ekran oyun önde)
    private bool _testMini;                                 // test kancası "mini 1"
    private const double MiniYukseklik = 26;
    private static readonly string[] VideoSurecleri = { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "vlc", "mpc-hc64", "mpc-hc", "PotPlayerMini64", "PotPlayer64", "Video.UI", "wmplayer", "mpv", "Spotify", "Netflix", "ApplicationFrameHost" };
    private readonly SpotifyServisi _spotify = new();       // beğen / sıradakiler
    private SpotifyServisi.Parca? _spotifyParca;            // şu an çalan (Spotify'a göre)
    private bool _spotifyBegenildi;
    private const double SozEkYukseklik = 34;               // söz satırı görünürken medya paneline eklenen yükseklik
    private string _sonSozBaslik = "", _sonSozSanatci = "";
    private bool _testBos;                                  // test kancası: boş paneli zorla
    private bool? _vpnOnceki;                               // bağlantı değişim duyurusu için
    private readonly SunucuServisi _sunucu = new();         // uzak sunucu sağlığı (SRV halkası)
    private bool? _sunucuOncekiSaglikli;                    // geçiş duyuruları için
    private WaServisi? _wa;                                 // WhatsApp Web köprüsü (arka planda gönder/al)
    private bool _waBaslatildi;
    private DateTime _waSonQr = DateTime.MinValue;
    private DateTime _waSonBaslatma = DateTime.MinValue;   // servis düşerse 30 sn'de bir yeniden başlat
    private readonly List<Bildirim> _okunmamis = new();   // fare gelene kadar kalan mesajlar (en yeni başta)
    private bool _toplanti;                                // mikrofon açık: duyurular sessiz
    private int _toplantiSayac;                            // toplantıda bastırılan duyuru sayısı
    private bool _bildirimGoruldu;                         // geniş mesaj paneli açıldı; fare çekilince okundu say
    private bool _onPlanTamEkran;
    private bool _surukleme;
    private bool _dropSonrasi;   // dosya bırakıldı, fare hâlâ üstünde: duyuruyu hemen göster, genişleme yok
    private readonly DispatcherTimer _surukleCikis = new() { Interval = TimeSpan.FromMilliseconds(180) };

    private MedyaDurumu _durum = MedyaDurumu.Bos;
    private bool _genis;
    private bool _tamEkranGizli;
    private Duyuru? _aktifDuyuru;
    private IntPtr _hwnd;
    private int _saniyeSayac;
    private string _sonAumid = "";

    private readonly DispatcherTimer _tik = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _saniye = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _daraltGecikme = new() { Interval = TimeSpan.FromMilliseconds(260) };
    private readonly DispatcherTimer _duyuruSuresi = new();

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly Brush _varsayilanKenar;
    private readonly Brush _varsayilanDolgu;

    public MainWindow(Ayarlar ayar)
    {
        InitializeComponent();
        _ayar = ayar;
        _varsayilanKenar = Ada.BorderBrush;
        _varsayilanDolgu = IlerlemeDolgu.Background;

        SourceInitialized += (_, _) => { _hwnd = new WindowInteropHelper(this).Handle; OdakAlmaz(); };
        Loaded += Pencere_Loaded;
        DpiChanged += (_, _) => Konumla();

        _tik.Tick += (_, _) => Tik();
        _saniye.Tick += async (_, _) => await SaniyeAsync();
        _daraltGecikme.Tick += (_, _) => { _daraltGecikme.Stop(); if (_aktifDuyuru == null && !_odakSerbest) Daralt(); };   // cevap yazılırken kapanma
        _duyuruSuresi.Tick += (_, _) => { _duyuruSuresi.Stop(); _aktifDuyuru = null; SonrakiDuyuru(); };
        _surukleCikis.Tick += (_, _) => { _surukleCikis.Stop(); if (_surukleme) { _surukleme = false; _aktifDuyuru = null; Daralt(); } };
        _etkinlik.Degisti += (m, a) => Dispatcher.BeginInvoke(() => CanavarUygula(m, a));
    }

    private async void Pencere_Loaded(object sender, RoutedEventArgs e)
    {
        NoiseDokusuUygula();
        AyarlariUygula(_ayar, ilk: true);
        DurumUygula(MedyaDurumu.Bos);

        // Servis olayları (arka plan iş parçacıklarından gelir, UI'a geçirilir)
        _medya.Degisti += d => Dispatcher.BeginInvoke(() => DurumUygula(d));
        _ses.SeviyeDegisti += (s, m) => Dispatcher.BeginInvoke(() => SesDegisti(s, m));
        _ses.AygitDegisti += ad => Dispatcher.BeginInvoke(() =>
        {
            BosSes.Guncelle(_ses.Seviye, _ses.Sessiz);
            if (_ayar.KulaklikAcik) _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Ses çıkışı değişti", ad, Simge: "", SaniyeOverride: 4));
        });
        _pil.Olay += d => { if (_ayar.PilAcik) _kuyruk.Ekle(d); };
        _gizlilik.Degisti += (m, k) => Dispatcher.BeginInvoke(() => GizlilikUygula(m, k));
        _bildirim.Yeni += b => Dispatcher.BeginInvoke(() => BildirimGeldi(b));
        _hava.Degisti += () => Dispatcher.BeginInvoke(HavaUygula);
        _pomodoro.Degisti += PomodoroUygula;
        _pomodoro.Bitti += () =>
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Süre doldu", "Pomodoro tamamlandı, mola zamanı", Simge: "", SaniyeOverride: 8));
            _ozet.Say("pomodoro");
            try { SystemSounds.Asterisk.Play(); } catch { }
        };
        _kuyruk.Degisti += () => Dispatcher.BeginInvoke(DuyuruGeldi);
        _sesRozetZaman.Tick += (_, _) => { _sesRozetZaman.Stop(); MedyaSesRozet.Visibility = Visibility.Collapsed; };
        // Toplantıda yalnız toplantı duyuruları ve gerçek uyarılar geçer; gerisi sayılır
        _kuyruk.Suzgec = d =>
        {
            // Oyun / tam ekran: bildirim ve bilgi duyuruları gösterilmez (okunmamış rozeti şeritte kalır); yalnız gerçek uyarılar geçer
            if ((_mini || _onPlanTamEkran) && d.Tur != DuyuruTuru.Uyari && d.Anahtar != "mik" && d.Anahtar != "hizli") return false;
            if (!_toplanti) return true;
            if (d.Anahtar == "mik" || d.Baslik.StartsWith("Toplantı", StringComparison.Ordinal)) return true;
            if (d.Tur == DuyuruTuru.Uyari && d.Anahtar != "kam") return true;   // pil azaldı, hata gibi
            _toplantiSayac++;
            return false;
        };

        MedyaPomodoro.Bagla(_pomodoro);
        BosPomodoro.Bagla(_pomodoro);

        // Rehber (.vcf): ad → numara, WhatsApp cevabı için
        RehberTemizle();
        RehberiYukle();

        // WhatsApp Web köprüsü: arka planda gönder/al (node + wa-servis)
        _wa = new WaServisi(_ayar.WaServisPort);
        if (_ayar.WaServisAcik) { _waBaslatildi = _wa.Baslat(); Gunluk($"wa-servis baslat: {_waBaslatildi}"); }
        try { _waLogo = (await UygulamaBilgisi.AlAsync("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App")).Simge; } catch { }

        // Hazne
        _hazne.Yukle();
        HaznePanel.Bagla(_hazne, _ayar.HazneGorunum);
        HaznePanel.YukleIstendi += o => _ = HazneYukle(o, link: false);
        HaznePanel.LinkIstendi += o => _ = HazneYukle(o, link: true);
        HaznePanel.ZipIstendi += HazneZip;
        HaznePanel.PanoIstendi += HaznePano;
        HaznePanel.GeriIstendi += () => { _hazneGoster = false; _hazneSonEkleme = DateTime.MinValue; Genislet(); };
        HaznePanel.TelefonaIstendi += o => _ = HazneTelefonaAsync(o);
        _ozet.Yukle();
        _hatirlatici.Yukle();
        _osdZaman.Tick += (_, _) => { _sesOsd.Bastir(); if (++_osdSayac >= 12) _osdZaman.Stop(); };
        _sesSeridiZaman.Tick += (_, _) => { _sesSeridiZaman.Stop(); _sesSeridi = false; if (_onPlanTamEkran && !_mini && _ayar.TamEkrandaGizle) { _tamEkranGizli = true; Ada.Visibility = Visibility.Hidden; } else if (!_genis) Daralt(animasyonlu: false); };
        _indirme.Indirildi += yol => Dispatcher.BeginInvoke(() => IndirmeTamamlandi(yol));
        if (_ayar.IndirmeIzleAcik && _indirme.Baslat()) Gunluk("indirme izleniyor: " + _indirme.Klasor);
        KarisimListe.ItemsSource = _karisimListe;
        _karisimZaman.Tick += (_, _) => { if (_karisimGoster && GenisKarisim.Visibility == Visibility.Visible) KarisimYenile(yenidenBoyutla: true); else _karisimZaman.Stop(); };
        _fareBekleZaman.Tick += (_, _) => { _fareBekleZaman.Stop(); if (!Ada.IsMouseOver && _genis) { _daraltGecikme.Stop(); _daraltGecikme.Start(); } };
        KompaktCanavar.MouseLeftButtonDown += (_, e) => { KompaktCanavar.Tepki(); e.Handled = true; };
        GenisCanavar.MouseLeftButtonDown += (_, e) => { GenisCanavar.Tepki(); e.Handled = true; };
        HaznePanel.GorunumDegisti += g => { _ayar.HazneGorunum = g; try { _ayar.Kaydet(); } catch { } if (_genis && GenisHazne.Visibility == Visibility.Visible) Genislet(); };
        HaznePanel.ClaudeIstendi += o =>
        {
            var yollar = o != null ? new[] { o.Yol } : _hazne.Ogeler.Select(x => x.Yol);
            string metin = ClaudeServisi.DosyaMetni(yollar);
            if (metin.Length == 0) { _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Özetlenecek metin dosyası yok", "txt, md, csv, json, kod dosyaları okunur", Simge: "", SaniyeOverride: 4)); return; }
            _hazneGoster = false;
            SoruPaneliAc(odakla: false);
            _ = SoruGonderAsync((o != null ? "Şu dosyayı Türkçe özetle:" : "Şu dosyaları Türkçe özetle; her dosya için en çok 3 madde:") + "\n\n" + metin,
                                o != null ? o.Ad + " özeti" : "Haznedeki dosyaları özetle");
        };
        HaznePanel.Degisti += () => { HazneGostergeGuncelle(); if (_genis) Genislet(); };
        HazneGostergeGuncelle();

        try { _ses.Baslat(); BosSes.Bagla(_ses); } catch { }

        Tik();
        _tik.Start();
        _saniye.Start();
        TestKancasiBaslat();
        KlavyeKancasiBaslat();

        try { await _medya.BaslatAsync(); } catch { }
        try { await _bildirim.BaslatAsync(); } catch { }
        _ = _hava.TikAsync(_ayar.HavaSehir);
    }

    // ---------- Ayarlar ----------

    public void AyarlariUygula(Ayarlar yeni, bool ilk = false)
    {
        _ayar = yeni;
        AdaOlcek.ScaleX = AdaOlcek.ScaleY = Math.Clamp(yeni.Olcek, 0.6, 1.6);
        Ada.Opacity = Math.Clamp(yeni.Saydamlik, 0.3, 1.0);
        Ada.Margin = new Thickness(0, Math.Max(0, yeni.UstBosluk), 0, 0);
        _pomodoro.VarsayilanSure(yeni.PomodoroDakika);
        if (!yeni.HavaAcik) { KompaktHava.Visibility = Visibility.Collapsed; GenisHava.Visibility = Visibility.Collapsed; }
        else HavaUygula();
        if (!yeni.MikrofonKameraAcik) GizlilikUygula(false, false);
        CanavarUygula(_etkinlik.Mod, _etkinlik.Aciklama);
        HaznePanel.SurukleEtkisi = yeni.HazneSurukleEtkisi;
        HaznePanel.GorunumAyarla(yeni.HazneGorunum);
        KisayollariKur();
        _spotify.Ayarla(yeni.SpotifyClientId, yeni.SpotifyRefreshToken);
        _claude.Ayarla(yeni.ClaudeApiKey, yeni.ClaudeModel, yeni.ClaudeNot);
        _efekt.Acik = yeni.SesEfektleriAcik; _efekt.Seviye = yeni.SesEfektSeviye / 100f;
        if (ilk) _spotify.YenilemeDegisti += yeniAnahtar => Dispatcher.BeginInvoke(() => { _ayar.SpotifyRefreshToken = yeniAnahtar; try { _ayar.Kaydet(); } catch { } });
        if (!ilk) { _sonSozBaslik = ""; if (_durum.VarMi) { _ = SozYukleAsync(_durum); _ = SpotifyDurumAsync(); } else SozGorunumAyarla(false); }
        if (!yeni.TamEkrandaGizle && _tamEkranGizli) { _tamEkranGizli = false; Ada.Visibility = Visibility.Visible; }
        Konumla();
        if (!ilk) { _ = _hava.TikAsync(yeni.HavaSehir); if (!_genis && _aktifDuyuru == null) Daralt(animasyonlu: false); }
    }

    /// Pencereyi seçili ekranın üst ortasına fiziksel piksel koordinatlarıyla yerleştirir.
    private void Konumla()
    {
        if (_hwnd == IntPtr.Zero) return;
        var ekranlar = Forms.Screen.AllScreens;
        if (ekranlar.Length == 0) return;
        var e = ekranlar[Math.Clamp(_ayar.Ekran, 0, ekranlar.Length - 1)];
        var dpi = VisualTreeHelper.GetDpi(this);
        int wpx = (int)Math.Round(Width * dpi.DpiScaleX);
        int x = e.Bounds.Left + (e.Bounds.Width - wpx) / 2;
        int y = e.Bounds.Top;
        SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // ---------- Medya durumu ----------

    private async void DurumUygula(MedyaDurumu d)
    {
        bool oncekiVarMi = _durum.VarMi;
        _durum = d;

        KompaktIcerikGuncelle();

        if (d.VarMi)
        {
            if (d.Baslik != _sonSozBaslik) { _sonSozSatiri = ""; KompaktBaslik.Text = d.Baslik; } else KompaktBaslikGuncelle();
            GenisBaslik.Text = d.Baslik;
            GenisSanatci.Text = d.Sanatci;
            KompaktKapak.Source = d.Kapak;
            GenisKapak.Source = d.Kapak;
            OynatDugmesi.Content = d.Oynuyor ? "" : "";
            EkolayzerAyarla(d.Oynuyor);
            VurguUygula(d.Kapak != null ? KapakRengi.Hesapla(d.Kapak, Colors.White) : (Color?)null);
        }
        else
        {
            EkolayzerAyarla(false);
            VurguUygula(null);
        }

        if (_genis) Genislet(); else if (_aktifDuyuru == null) Daralt(animasyonlu: oncekiVarMi != d.VarMi || !IsLoaded);

        // Parça değiştiyse: sözleri getir, Spotify beğeni durumunu sor
        if (!d.VarMi || d.Baslik != _sonSozBaslik || d.Sanatci != _sonSozSanatci)
        {
            _sonSozBaslik = d.Baslik; _sonSozSanatci = d.Sanatci;
            _ = SozYukleAsync(d);
            _ = SpotifyDurumAsync();
        }

        // Kaynak uygulama adı ve simgesi (önbellekli, asenkron)
        if (d.VarMi && d.Aumid != _sonAumid)
        {
            _sonAumid = d.Aumid;
            var (ad, simge) = await UygulamaBilgisi.AlAsync(d.Aumid);
            if (_sonAumid != d.Aumid) return;
            GenisUygulamaAd.Text = ad;
            GenisUygulamaSimge.Source = simge;
            GenisUygulama.Visibility = string.IsNullOrEmpty(ad) && simge == null ? Visibility.Collapsed : Visibility.Visible;
        }
        else if (!d.VarMi)
        {
            _sonAumid = "";
            GenisUygulama.Visibility = Visibility.Collapsed;
        }
    }

    /// 64×64 rastgele gri noktalardan döşeme dokusu: düz siyahı kırar, bantlaşmayı gizler.
    private void NoiseDokusuUygula()
    {
        const int boyut = 64;
        var bmp = new System.Windows.Media.Imaging.WriteableBitmap(boyut, boyut, 96, 96, PixelFormats.Bgra32, null);
        var px = new byte[boyut * boyut * 4];
        var r = new Random(7);
        for (int i = 0; i < px.Length; i += 4)
        {
            byte g = (byte)r.Next(0, 256);
            px[i] = g; px[i + 1] = g; px[i + 2] = g;
            px[i + 3] = (byte)r.Next(0, 28);   // çok düşük alfa: doku hissedilir, okunmaz
        }
        bmp.WritePixels(new Int32Rect(0, 0, boyut, boyut), px, boyut * 4, 0);
        bmp.Freeze();
        Doku.Fill = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, boyut, boyut),
            Stretch = Stretch.None,
        };
        Doku.Opacity = 1;
    }

    /// Kapak renginden kenar, ilerleme ve gölge rengi
    private void VurguUygula(Color? renk)
    {
        if (renk == null)
        {
            Ada.BorderBrush = _varsayilanKenar;
            IlerlemeDolgu.Background = _varsayilanDolgu;
            AdaGolge.Color = Colors.Black;
            AdaGolge.Opacity = 0.5;
            return;
        }
        var r = renk.Value;
        Ada.BorderBrush = new SolidColorBrush(Color.FromArgb(0x70, r.R, r.G, r.B));
        IlerlemeDolgu.Background = new SolidColorBrush(r);
        AdaGolge.Color = Color.FromRgb((byte)(r.R / 2), (byte)(r.G / 2), (byte)(r.B / 2));
        AdaGolge.Opacity = 0.65;
    }

    private void EkolayzerAyarla(bool oynuyor)
    {
        var sb = (Storyboard)Resources["EkolayzerAnim"];
        if (oynuyor) sb.Begin(this, true);
        else
        {
            sb.Stop(this);
            Cubuk1Olcek.ScaleY = 0.35; Cubuk2Olcek.ScaleY = 0.5; Cubuk3Olcek.ScaleY = 0.35;
        }
    }

    // ---------- Zamanlayıcılar ----------

    private void Tik()
    {
        var simdi = DateTime.Now;
        string saat = simdi.ToString("HH:mm", Tr);
        KompaktSaat.Text = saat; GenisSaat.Text = saat; GenisBosSaat.Text = saat;
        GenisBosTarih.Text = simdi.ToString("d MMMM", Tr).ToUpper(Tr) + "  ·  " + simdi.ToString("dddd", Tr).ToUpper(Tr);

        _pil.Tik();
        string pil = _pil.Metin;
        KompaktPil.Text = pil; GenisPil.Text = pil; GenisBosPil.Text = pil;

        _pomodoro.Tik();
        if ((_mini || _testMini || _sesSeridi) && MiniKatman.Visibility == Visibility.Visible) { if (_testMini) _gpu.Tik(); MiniGuncelle(); }

        if (_durum.VarMi)
        {
            var (konum, sure) = _medya.ZamanCizelgesi();
            if (_genis)
            {
                double oran = sure.TotalSeconds > 0 ? konum.TotalSeconds / sure.TotalSeconds : 0;
                IlerlemeDolgu.Width = Math.Max(0, IlerlemeRay.ActualWidth * Math.Clamp(oran, 0, 1));
                GecenSure.Text = SureBicimle(konum);
                KalanSure.Text = "-" + SureBicimle(sure - konum);
            }
            // Söz satırı: geniş panelde kendi satırında, kompakt görünümde şarkı adının yerinde
            if (SozMetin.Visibility == Visibility.Visible && _soz.SatirAl(konum, sure) is { } satir)
            {
                _sonSozSatiri = satir;
                SozSatiriGoster(satir);
                if (!_genis) KompaktBaslikGuncelle();
            }
        }
    }

    private string _sonSozSatiri = "";

    /// Kompakt başlık: söz varsa o anki satır (enstrümantal arada şarkı adı), yoksa şarkı adı
    private void KompaktBaslikGuncelle()
    {
        bool sozGoster = _ayar.SozlerAcik && SozMetin.Visibility == Visibility.Visible && _sonSozSatiri.Length > 0 && _sonSozSatiri != "♪";
        string hedef = sozGoster ? _sonSozSatiri : _durum.Baslik;
        if (KompaktBaslik.Text != hedef) KompaktBaslik.Text = hedef;
    }

    // ---------- Telefondan bilgisayara ve indirme izleme ----------

    private readonly IndirmeIzleyici _indirme = new();

    /// Kendi WhatsApp sohbetine ("Siz") attığın dosya ve bağlantılar: hazneye düşer
    private void TelefondanIsle(WaServisi.Telefondan t)
    {
        if (!_ayar.TelefondanHazneyeAcik) return;
        string? yol = null; string ad;
        if (t.tur == "dosya") { yol = t.yol; ad = t.ad; }
        else
        {
            // Bağlantı: internet kısayolu dosyası (.url), çift tıklayınca tarayıcıda açılır
            try
            {
                string klasor = System.IO.Path.Combine(Ayarlar.Klasor, "gelen");
                System.IO.Directory.CreateDirectory(klasor);
                var uri = new Uri(t.url);
                string temel = (uri.Host.Replace("www.", "") + (uri.AbsolutePath.Length > 1 ? " " + uri.AbsolutePath.Trim('/').Replace('/', ' ') : "")).Trim();
                foreach (var c in System.IO.Path.GetInvalidFileNameChars()) temel = temel.Replace(c, '_');
                if (temel.Length > 60) temel = temel[..60];
                yol = System.IO.Path.Combine(klasor, temel + ".url");
                for (int i = 2; System.IO.File.Exists(yol); i++) yol = System.IO.Path.Combine(klasor, $"{temel} ({i}).url");
                System.IO.File.WriteAllText(yol, "[InternetShortcut]\r\nURL=" + t.url + "\r\n");
                ad = temel;
            }
            catch (Exception e) { Gunluk("telefondan link yazilamadi: " + e.Message); return; }
        }
        if (yol == null || _hazne.Ekle(yol) == null) return;
        _hazneSonEkleme = DateTime.Now;
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, t.tur == "dosya" ? "Telefondan dosya geldi" : "Telefondan bağlantı geldi", ad, Simge: "", SaniyeOverride: 5));
        Gunluk($"telefondan: {t.tur} {ad}");
    }

    /// İndirilenler klasörüne düşen dosya: hazneye al ve duyur
    private void IndirmeTamamlandi(string yol)
    {
        if (!_ayar.IndirmeIzleAcik) return;
        if (_hazne.Ekle(yol) == null) return;
        _hazneSonEkleme = DateTime.Now;
        long boyut = 0; try { boyut = new System.IO.FileInfo(yol).Length; } catch { }
        string boyutMetin = boyut >= 1 << 20 ? $"{boyut / 1048576.0:0.0} MB" : $"{Math.Max(1, boyut / 1024)} KB";
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "İndirme tamamlandı", $"{System.IO.Path.GetFileName(yol)} · {boyutMetin} · haznede", Simge: "", SaniyeOverride: 5));
        Gunluk($"indirme: {yol} {boyut}");
    }

    // ---------- Oyun oturumu, ses profili, izleme modu ----------

    /// Oyun katmanı açıldı / kapandı: oturum süresi ve GPU tepe, ses profili
    private void OyunDurumDegisti(bool basladi)
    {
        if (basladi)
        {
            _oyunBaslangic = DateTime.Now; _oyunGpuTepe = -1; _oyunSurec = OnPlanSurecAdi();
            if (_ayar.OyunSesProfiliAcik) OyunSesProfili(true);
            return;
        }
        if (_oyunBaslangic == DateTime.MinValue) return;
        var sure = DateTime.Now - _oyunBaslangic;
        _oyunBaslangic = DateTime.MinValue;
        if (_ayar.OyunSesProfiliAcik) OyunSesProfili(false);
        if (sure.TotalMinutes < 1) return;
        _ozet.OyunEkle((int)sure.TotalSeconds, _oyunGpuTepe, OzetServisi.UygulamaAdi(_oyunSurec));
        string gpu = _oyunGpuTepe >= 0 ? $" · GPU tepe {_oyunGpuTepe}°" : "";
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, $"Oyun bitti: {OzetServisi.Sure((int)sure.TotalSeconds)}", $"{OzetServisi.UygulamaAdi(_oyunSurec)}{gpu} · bugün toplam {OzetServisi.Sure(_ozet.Bugun.Oyun)}", Simge: "", SaniyeOverride: 6));
        Gunluk($"oyun oturumu: {_oyunSurec} {sure.TotalMinutes:0} dk gpu tepe {_oyunGpuTepe}");
    }

    /// Oyun ve Discord kulaklığa, müzik kısık; çıkınca geri
    private void OyunSesProfili(bool basla)
    {
        try
        {
            var oturumlar = _karisim.Oturumlar();
            if (basla)
            {
                _oyunYonlendirilen.Clear(); _oyunMuzikEski = -1f;
                string kulaklik = _ayar.OyunKulaklikAd.Trim();
                var aygit = kulaklik.Length > 0 ? _karisim.Aygitlar().FirstOrDefault(a => a.Ad.Contains(kulaklik, StringComparison.OrdinalIgnoreCase)) : null;
                foreach (var o in oturumlar)
                {
                    bool oyunOturumu = o.Surec.Equals(_oyunSurec, StringComparison.OrdinalIgnoreCase);
                    bool discord = o.Surec.Equals("Discord", StringComparison.OrdinalIgnoreCase);
                    bool muzik = o.Surec.Equals("Spotify", StringComparison.OrdinalIgnoreCase);
                    if ((oyunOturumu || discord) && aygit != null && !o.Kalici && !o.AygitId.Equals(aygit.Id, StringComparison.OrdinalIgnoreCase))
                    { if (_karisim.Yonlendir(o, aygit.Id)) _oyunYonlendirilen.Add(o); }
                    if (muzik) { _oyunMuzikEski = o.Seviye; _karisim.SeviyeAyarla(o, _ayar.OyunMuzikSeviye / 100f); }
                }
                if (_oyunYonlendirilen.Count > 0 || _oyunMuzikEski >= 0)
                    Gunluk($"oyun ses profili: yonlendirilen={string.Join(",", _oyunYonlendirilen.Select(o => o.Surec))} muzik={_oyunMuzikEski}");
            }
            else
            {
                foreach (var o in _oyunYonlendirilen) _karisim.Yonlendir(o, null);
                _oyunYonlendirilen.Clear();
                if (_oyunMuzikEski >= 0)
                {
                    var sp = oturumlar.FirstOrDefault(o => o.Surec.Equals("Spotify", StringComparison.OrdinalIgnoreCase));
                    if (sp != null) _karisim.SeviyeAyarla(sp, _oyunMuzikEski);
                    _oyunMuzikEski = -1f;
                }
            }
        }
        catch (Exception e) { Gunluk("oyun ses profili hatasi: " + e.Message); }
    }

    /// Tam ekran video (tarayıcı / oynatıcı) başladı / bitti: ekran uyumasın, kaldığın yer
    private void IzlemeDurumDegisti(bool basladi)
    {
        if (!_ayar.IzlemeModuAcik) return;
        if (basladi)
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED);
            _videoBaslangic = DateTime.Now; _videoBaslik = BaslikTemizle(OnPlanBaslik());
            return;
        }
        SetThreadExecutionState(ES_CONTINUOUS);
        if (_videoBaslangic == DateTime.MinValue) return;
        var sure = DateTime.Now - _videoBaslangic; _videoBaslangic = DateTime.MinValue;
        if (sure.TotalMinutes < 2 || _videoBaslik.Length == 0) return;
        _ozet.IzlemeEkle(_videoBaslik, (int)sure.TotalMinutes);
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Kaldığın yer", $"{_videoBaslik} · {OzetServisi.Sure((int)sure.TotalSeconds)}", Simge: "", SaniyeOverride: 6));
        Gunluk($"izleme: {_videoBaslik} {sure.TotalMinutes:0} dk");
    }

    private string OnPlanBaslik()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return "";
            var sb = new System.Text.StringBuilder(512);
            GetWindowText(h, sb, 512);
            return sb.ToString();
        }
        catch { return ""; }
    }

    /// "Bloodhounds 2. Sezon 5. Bölüm izle | Site - Google Chrome" → "Bloodhounds 2. Sezon 5. Bölüm"
    private static string BaslikTemizle(string b)
    {
        foreach (var son in new[] { " - Google Chrome", " - Microsoft Edge", " — Mozilla Firefox", " - Mozilla Firefox", " - Brave", " - Opera", " - VLC media player", " - Netflix" })
            if (b.EndsWith(son, StringComparison.OrdinalIgnoreCase)) b = b[..^son.Length];
        int i = b.LastIndexOf(" | ", StringComparison.Ordinal); if (i > 0) b = b[..i];
        b = System.Text.RegularExpressions.Regex.Replace(b, @"\s+(izle|full izle|türkçe dublaj|türkçe altyazı)\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        return b.Length > 70 ? b[..70] + "…" : b;
    }

    /// Tam ekran video gizliyken ses değişince 2 sn ince şerit
    private void SesSeridiGoster()
    {
        if (!_ayar.IzlemeModuAcik || _mini) return;
        _sesSeridi = true;
        if (_tamEkranGizli) Ada.Visibility = Visibility.Visible;
        if (!_genis) Daralt(animasyonlu: false);
        _sesSeridiZaman.Stop(); _sesSeridiZaman.Start();
    }

    /// Oyunda Ctrl+Alt+Y: son WhatsApp mesajına hazır cevap
    private async Task HizliYanitGonderAsync()
    {
        var b = _okunmamis.FirstOrDefault(x => x.Uygulama.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) || x.Aumid.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase));
        if (b == null || _wa is not { Hazir: true } || string.IsNullOrWhiteSpace(_ayar.HizliYanit)) return;
        var s = await _wa.GonderAsync(RehberNumara(b.Baslik), b.Baslik, _ayar.HizliYanit, _waJid.GetValueOrDefault(b.Baslik));
        _kuyruk.Ekle(s.Ok
            ? new Duyuru(DuyuruTuru.Basari, "Hazır cevap gitti", b.Baslik, Simge: "", SaniyeOverride: 3, Anahtar: "hizli")
            : new Duyuru(DuyuruTuru.Uyari, "Hazır cevap gönderilemedi", s.Mesaj, Simge: "", SaniyeOverride: 4, Anahtar: "hizli"));
        if (s.Ok) { _okunmamis.Clear(); KompaktBildirimGuncelle(); if (_mini) MiniGuncelle(); }
        Gunluk($"hizli yanit: {b.Baslik} ok={s.Ok} {s.Mesaj}");
    }

    // ---------- Ses karışımı ----------

    private void KarisimAc_Click(object sender, RoutedEventArgs e) { _karisimGoster = true; _genis = true; _daraltGecikme.Stop(); FareBekleBaslat(); Genislet(); }
    private void KarisimKapat_Click(object sender, RoutedEventArgs e) { _karisimGoster = false; _karisimZaman.Stop(); if (_genis) Genislet(); }

    /// Oturum listesini servisten tazele; satır nesnelerini koru ki kaydırıcı sıçramasın
    private void KarisimYenile(bool yenidenBoyutla = false)
    {
        _karisimYenileniyor = true;
        try
        {
            var onceki = _karisimListe.ToDictionary(o => o.Surec, StringComparer.OrdinalIgnoreCase);
            var yeni = _karisim.Oturumlar(onceki);
            foreach (var o in yeni) if (o.Simge == null && o.Exe.Length > 0 && System.IO.File.Exists(o.Exe)) { try { o.Simge = HazneDeposu.DosyaSimgesi(o.Exe); } catch { } }
            int eskiSayi = _karisimListe.Count;
            for (int i = _karisimListe.Count - 1; i >= 0; i--) if (!yeni.Any(y => y.Surec.Equals(_karisimListe[i].Surec, StringComparison.OrdinalIgnoreCase))) _karisimListe.RemoveAt(i);
            foreach (var o in yeni) if (!_karisimListe.Contains(o)) _karisimListe.Add(o);
            KarisimBos.Visibility = _karisimListe.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            string vars = _karisim.VarsayilanAygitId;
            var aygit = _karisim.Aygitlar().FirstOrDefault(a => a.Id == vars);
            KarisimVarsayilan.Text = aygit != null ? "varsayılan: " + aygit.Kisa : "";
            if (yenidenBoyutla && _karisimListe.Count != eskiSayi && _genis && GenisKarisim.Visibility == Visibility.Visible) Genislet();
        }
        finally { _karisimYenileniyor = false; }
    }

    private void KarisimSes_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_karisimYenileniyor || sender is not Slider s || s.Tag is not KarisimServisi.Oturum o) return;
        if (Math.Abs(o.Seviye - e.NewValue) < 0.005) return;
        _karisim.SeviyeAyarla(o, (float)e.NewValue);
    }

    private void KarisimSessiz_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is KarisimServisi.Oturum o) _karisim.SessizAyarla(o, !o.Sessiz);
    }

    /// Aygıt adına tıkla: menüde tüm çıkış aygıtları ve "Sistem varsayılanı"; geçerli olan işaretli
    private void KarisimAygit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not KarisimServisi.Oturum o) return;
        var aygitlar = _karisim.Aygitlar();
        if (aygitlar.Count == 0) return;
        string? kaliciId = o.Pidler.Count > 0 ? AudioPolicyConfig.Oku(o.Pidler[0]) : null;
        string varsayilanId = _karisim.VarsayilanAygitId;

        var menu = new ContextMenu { PlacementTarget = b.IsLoaded ? b : KarisimListe, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        menu.Items.Add(new MenuItem { Header = $"{o.Ad} sesini şuradan çal:", IsEnabled = false, FontWeight = FontWeights.SemiBold });
        foreach (var a in aygitlar)
        {
            var oge = new MenuItem
            {
                Header = a.Ad + (a.Id == varsayilanId ? "  (sistem varsayılanı)" : ""),
                IsCheckable = true,
                IsChecked = kaliciId != null ? a.Id.Equals(kaliciId, StringComparison.OrdinalIgnoreCase) : a.Id.Equals(o.AygitId, StringComparison.OrdinalIgnoreCase),
            };
            var secilen = a;
            oge.Click += (_, _) => KarisimYonlendir(o, secilen.Id, secilen.Kisa);
            menu.Items.Add(oge);
        }
        menu.Items.Add(new Separator());
        var vars = new MenuItem { Header = "Sistem varsayılanını izle", IsCheckable = true, IsChecked = kaliciId == null, ToolTip = "Özel atama kaldırılır; Windows'un varsayılan aygıtı neyse oradan çalar" };
        vars.Click += (_, _) => KarisimYonlendir(o, null, "sistem varsayılanı");
        menu.Items.Add(vars);
        _sonMenu = menu; menu.IsOpen = true;
    }

    private ContextMenu? _sonMenu;   // test kancası menüyü kapatabilsin
    private void KarisimYonlendir(KarisimServisi.Oturum o, string? hedefId, string hedefAd)
    {
        bool ok = _karisim.Yonlendir(o, hedefId);
        Gunluk($"karisim yonlendir: {o.Surec} pid={string.Join(",", o.Pidler)} -> {hedefAd} ok={ok} {_karisim.SonHata}");
        _kuyruk.Ekle(ok
            ? new Duyuru(DuyuruTuru.Basari, $"{o.Ad} → {hedefAd}", hedefId == null ? "" : "uygulama sesini bu aygıttan çalar", Simge: "", SaniyeOverride: 3, Anahtar: "karisim")
            : new Duyuru(DuyuruTuru.Uyari, "Yönlendirilemedi", _karisim.SonHata, Simge: "", SaniyeOverride: 4, Anahtar: "karisim"));
        Dispatcher.BeginInvoke(async () => { await Task.Delay(700); if (_karisimGoster) KarisimYenile(); }, System.Windows.Threading.DispatcherPriority.Background);
    }

    // ---------- Claude'a sor ----------


    private readonly HatirlaticiServisi _hatirlatici = new();

    private void NotKaydet(string metin)
    {
        if (metin.Length == 0) return;
        try
        {
            string yol = string.IsNullOrWhiteSpace(_ayar.NotDosyasi)
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Dinamik Ada Notlar.md")
                : _ayar.NotDosyasi;
            if (!System.IO.File.Exists(yol)) System.IO.File.WriteAllText(yol, "# Dinamik Ada Notları\n\n", System.Text.Encoding.UTF8);
            System.IO.File.AppendAllText(yol, $"- {DateTime.Now:dd.MM HH:mm} — {metin}\n", System.Text.Encoding.UTF8);
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Not kaydedildi", metin.Length > 50 ? metin[..50] + "…" : metin, Simge: "", SaniyeOverride: 4));
            Gunluk($"not: {metin}");
        }
        catch (Exception e) { _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Not kaydedilemedi", e.Message, Simge: "", SaniyeOverride: 4)); }
    }

    private void HatirlaticiKur(string metin)
    {
        var sonuc = _hatirlatici.Ekle(metin);
        if (sonuc == null)
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Zaman anlaşılmadı", "örn: hatırlat: 14:30 toplantı · 15 dk sonra çay · yarın 09:00 X", Simge: "", SaniyeOverride: 6));
            return;
        }
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Hatırlatıcı kuruldu", $"{HatirlaticiServisi.Bicimle(sonuc.Value.Zaman)} · {sonuc.Value.Metin}", Simge: "", SaniyeOverride: 5));
        Gunluk($"hatirlatici: {sonuc.Value.Zaman:g} {sonuc.Value.Metin}");
    }

    private void HatirlaticiKontrol()
    {
        foreach (var h in _hatirlatici.Gecenler())
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "⏰ Hatırlatma", h.Metin, Simge: "", SaniyeOverride: 15, Anahtar: "hatirla-" + h.Id));
            Gunluk($"hatirlatma dustu: {h.Metin}");
        }
    }
    // ---------- Günün özeti ve ruh hâli ----------

    private void OzetGoster(bool otomatik, bool haftalik = false)
    {
        string metin = haftalik ? _ozet.HaftalikOzet() : _ozet.Ozet();
        if (!haftalik) _ozetGosterildi = DateTime.Today.ToString("yyyy-MM-dd");
        _claude.BaglamEkle((haftalik ? "Kullanıcının son 7 gün özeti:\n" : "Kullanıcının bugünkü özeti:\n") + metin);
        SoruBaslik.Text = haftalik ? "Haftalık Rapor" : "Günün özeti";
        SoruCevap.Text = metin;
        SoruDurum.Text = "yerel veri · Claude'a bu özet hakkında soru sorabilirsin";
        SoruCevapKaydir.Visibility = Visibility.Visible; SoruAltSatir.Visibility = Visibility.Visible;
        SoruIpucu.Text = "Özet hakkında sor… Enter gönderir";
        if (otomatik)
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, haftalik ? "Haftalık rapor hazır" : "Günün özeti hazır", metin.Split('\n')[0], Simge: "", SaniyeOverride: 6));
        _soruGoster = true; _soruSonKullanim = DateTime.Now;
        if (_genis || !otomatik) { _genis = true; _daraltGecikme.Stop(); Genislet(); }
        Gunluk((haftalik ? "haftalik" : "ozet") + " gosterildi: " + metin.Replace("\n", " | "));
    }

    /// Saniyede bir: özet verisi, akşam özeti saati, mola hatırlatma, uykulu gözler, özel gün şapkası
    private void RuhHaliTik()
    {
        _ozet.Tik(OnPlanSurecAdi(), _durum.VarMi && _durum.Oynuyor, _toplanti);

        string bugun = DateTime.Today.ToString("yyyy-MM-dd");
        if (_ayar.OzetAcik && DateTime.Now.ToString("HH:mm") == _ayar.OzetSaat && !_mini)
        {
            string yilHafta = System.Globalization.ISOWeek.GetYear(DateTime.Now) + "-" + System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);
            if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday && _haftaGosterildi != yilHafta) { _haftaGosterildi = yilHafta; OzetGoster(otomatik: true, haftalik: true); }
            else if (_ozetGosterildi != bugun) OzetGoster(otomatik: true);
        }

        // Mola: kesintisiz MolaDakika boyunca başındaysa bir kez hatırlat; 3 dk ara verince sıfırlanır
        if (_ayar.MolaDakika > 0)
        {
            if (_ozet.SurekliAktifSaniye >= _ayar.MolaDakika * 60 && (DateTime.Now - _sonMolaUyari).TotalMinutes >= _ayar.MolaDakika && !_mini)
            {
                _sonMolaUyari = DateTime.Now; _yorgun = true;
                _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Mola zamanı", $"{_ayar.MolaDakika} dakikadır aralıksız başındasın; birkaç dakika kalk", Simge: "", SaniyeOverride: 7, Anahtar: "mola"));
            }
            if (_yorgun && _ozet.Bosta) _yorgun = false;
        }

        int saat = DateTime.Now.Hour;
        bool uykulu = _yorgun || saat >= 23 || saat < 6;
        if (KompaktCanavar.Uykulu != uykulu) { KompaktCanavar.Uykulu = uykulu; GenisCanavar.Uykulu = uykulu; }

        if (_sapkaGunu != bugun) { _sapkaGunu = bugun; SapkaGuncelle(); }
    }

    /// Özel gün şapkası: yılbaşı, millî bayramlar, dinî bayramlar (2026-2027), doğum günü
    private void SapkaGuncelle()
    {
        var t = DateTime.Today;
        (Color Renk, string Ad)? sapka = null;
        string ga = t.ToString("dd.MM");
        if (ga == "01.01" || ga == "31.12") sapka = (Color.FromRgb(0xE0, 0x3C, 0x31), "Yılbaşı");
        else if (ga is "23.04" or "19.05" or "30.08" or "29.10") sapka = (Color.FromRgb(0xE0, 0x3C, 0x31), ga == "29.10" ? "Cumhuriyet Bayramı" : "Millî bayram");
        else if (BayramMi(t)) sapka = (Color.FromRgb(0xF2, 0xC1, 0x4E), "Bayram");
        else if (!string.IsNullOrWhiteSpace(_ayar.DogumGunu) && _ayar.DogumGunu.Trim() == ga) sapka = (Color.FromRgb(0x7C, 0xC7, 0xFF), "Doğum günün kutlu olsun");
        KompaktCanavar.SapkaAyarla(sapka?.Renk, sapka?.Ad);
        GenisCanavar.SapkaAyarla(sapka?.Renk, sapka?.Ad);
        if (sapka != null) Gunluk("sapka: " + sapka.Value.Ad);
    }

    private static bool BayramMi(DateTime t)
    {
        // Ramazan ve Kurban Bayramı günleri (arife hariç)
        var gunler = new (DateTime Bas, int Gun)[]
        {
            (new DateTime(2026, 3, 20), 3), (new DateTime(2026, 5, 27), 4),
            (new DateTime(2027, 3, 9), 3), (new DateTime(2027, 5, 16), 4),
            (new DateTime(2028, 2, 26), 3), (new DateTime(2028, 5, 5), 4),
        };
        return gunler.Any(g => t >= g.Bas && t < g.Bas.AddDays(g.Gun));
    }

    private void SoruPaneliAc(bool odakla)
    {
        FareBekleBaslat();
        if (SoruBaslik.Text != "Claude'a sor") { SoruBaslik.Text = "Claude'a sor"; SoruIpucu.Text = "Bir şey sor… Enter gönderir"; }
        _soruGoster = true;
        _soruSonKullanim = DateTime.Now;
        _genis = true; _daraltGecikme.Stop();
        Genislet();
        if (odakla) Dispatcher.BeginInvoke(() => SoruKutu_Tik(SoruKutu, null!), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void SoruKutu_Tik(object sender, MouseButtonEventArgs e)
    {
        OdakIzinVer(true);
        var kutu = sender as TextBox ?? SoruKutu;
        Dispatcher.BeginInvoke(() => { kutu.Focus(); Keyboard.Focus(kutu); });
    }

    private void SoruKutu_TextChanged(object sender, TextChangedEventArgs e) =>
        SoruIpucu.Visibility = SoruKutu.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void SoruKutu_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { e.Handled = true; SoruGonder_Click(sender, e); }
        else if (e.Key == Key.Escape) { e.Handled = true; SoruKapat_Click(sender, e); }
    }

    private void SoruGonder_Click(object sender, RoutedEventArgs e)
    {
        string soru = SoruKutu.Text.Trim();
        if (soru.Length == 0 || _soruBekliyor) return;
        SoruKutu.Text = "";
        var on = System.Text.RegularExpressions.Regex.Match(soru, @"^(not|hatırlat|hatirlat|anımsat|hatırlatma)\s*:?\s+(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (on.Success)
        {
            if (on.Groups[1].Value.StartsWith("not", StringComparison.OrdinalIgnoreCase)) NotKaydet(on.Groups[2].Value.Trim());
            else HatirlaticiKur(on.Groups[2].Value.Trim());
            return;
        }
        _ = SoruGonderAsync(soru);
    }

    private void SoruKapat_Click(object sender, RoutedEventArgs e)
    {
        _soruGoster = false;
        Keyboard.ClearFocus();
        OdakIzinVer(false);
        if (_genis) Genislet();
    }

    private string _sonSoru = "";

    /// Claude yanıtını masaüstüne profesyonel .md ve yazdırılabilir .html olarak kaydeder
    private void SoruKaydet_Click(object sender, RoutedEventArgs e)
    {
        string icerik = SoruCevap.Text.Trim();
        if (icerik.Length == 0 || icerik == "Claude düşünüyor…") return;
        string baslik = SoruBaslik.Text == "Günün özeti" ? "Günün Özeti" : (_sonSoru.Length > 0 ? KisaBaslik(_sonSoru) : "Claude Yanıtı");
        var now = DateTime.Now;
        string damga = now.ToString("yyyy-MM-dd HH-mm");
        string masaustu = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string taban = DosyaAdiTemizle($"{baslik} - {damga}");
        string mdYol = System.IO.Path.Combine(masaustu, taban + ".md");
        string htmlYol = System.IO.Path.Combine(masaustu, taban + ".html");
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {baslik}").AppendLine();
            sb.AppendLine($"{now:d MMMM yyyy, HH:mm} · Dinamik Ada · Claude").AppendLine();
            if (_sonSoru.Length > 0 && SoruBaslik.Text != "Günün özeti") sb.AppendLine($"> **Soru:** {_sonSoru}").AppendLine();
            sb.AppendLine("---").AppendLine();
            sb.AppendLine(icerik).AppendLine().AppendLine("---");
            sb.AppendLine("<sub>Dinamik Ada ile oluşturuldu.</sub>");
            System.IO.File.WriteAllText(mdYol, sb.ToString(), System.Text.Encoding.UTF8);
            System.IO.File.WriteAllText(htmlYol, RaporHtml(baslik, now, SoruBaslik.Text == "Günün özeti" ? "" : _sonSoru, icerik), System.Text.Encoding.UTF8);
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Masaüstüne kaydedildi", taban + ".md · .html (yazdır → PDF)", Simge: "", SaniyeOverride: 6));
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{mdYol}\"")); } catch { }
            Gunluk($"claude kaydet: {mdYol}");
        }
        catch (Exception ex) { _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Kaydedilemedi", ex.Message, Simge: "", SaniyeOverride: 5)); }
    }

    private static string KisaBaslik(string s)
    {
        s = s.Split('\n')[0].Trim();
        if (s.Length > 48) s = s[..48].TrimEnd() + "…";
        return s.Length == 0 ? "Claude Yanıtı" : s;
    }

    private static string DosyaAdiTemizle(string s)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, ' ');
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length > 90 ? s[..90].Trim() : s;
    }

    /// Basit Markdown → profesyonel, yazdırmaya uygun HTML (açık tema, A4)
    private static string RaporHtml(string baslik, DateTime zaman, string soru, string md)
    {
        var g = new System.Text.StringBuilder();
        bool liste = false;
        foreach (var hamSatir in md.Replace("\r", "").Split('\n'))
        {
            string satir = HtmlKac(hamSatir.TrimEnd());
            satir = System.Text.RegularExpressions.Regex.Replace(satir, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            satir = System.Text.RegularExpressions.Regex.Replace(satir, @"(?<!\*)\*(?!\*)(.+?)\*(?!\*)", "<em>$1</em>");
            string t = satir.TrimStart();
            if (t.StartsWith("- ") || t.StartsWith("• ") || System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d+[.)]\s"))
            {
                if (!liste) { g.Append("<ul>"); liste = true; }
                g.Append("<li>").Append(System.Text.RegularExpressions.Regex.Replace(t, @"^(-|•|\d+[.)])\s+", "")).Append("</li>");
                continue;
            }
            if (liste) { g.Append("</ul>"); liste = false; }
            if (t.Length == 0) continue;
            if (t.StartsWith("### ")) g.Append("<h3>").Append(t[4..]).Append("</h3>");
            else if (t.StartsWith("## ")) g.Append("<h2>").Append(t[3..]).Append("</h2>");
            else if (t.StartsWith("# ")) { /* başlık üstte var */ }
            else if (t == "---") g.Append("<hr>");
            else g.Append("<p>").Append(satir).Append("</p>");
        }
        if (liste) g.Append("</ul>");
        string soruBlok = soru.Length > 0 ? $"<div class='soru'><span>Soru</span>{HtmlKac(soru)}</div>" : "";
        return $@"<!doctype html><html lang='tr'><head><meta charset='utf-8'><title>{HtmlKac(baslik)}</title>
<style>
@page {{ size: A4; margin: 2cm; }}
* {{ box-sizing: border-box; }}
body {{ font-family: 'Segoe UI', system-ui, sans-serif; color: #1a1a22; line-height: 1.65; max-width: 760px; margin: 40px auto; padding: 0 24px; }}
.ust {{ border-bottom: 3px solid #D97757; padding-bottom: 16px; margin-bottom: 28px; }}
h1 {{ font-size: 26px; margin: 0 0 6px; color: #15151b; }}
.meta {{ color: #8a8a93; font-size: 13px; }}
.soru {{ background: #faf3f0; border-left: 3px solid #D97757; padding: 12px 16px; border-radius: 6px; margin: 0 0 22px; font-size: 14px; }}
.soru span {{ display: block; font-size: 11px; text-transform: uppercase; letter-spacing: .5px; color: #D97757; font-weight: 600; margin-bottom: 4px; }}
h2 {{ font-size: 19px; margin: 26px 0 10px; color: #15151b; }}
h3 {{ font-size: 15px; margin: 20px 0 8px; color: #333; }}
p {{ margin: 0 0 12px; }}
ul {{ margin: 0 0 14px; padding-left: 22px; }}
li {{ margin: 0 0 6px; }}
hr {{ border: none; border-top: 1px solid #e5e5ea; margin: 22px 0; }}
.alt {{ margin-top: 36px; padding-top: 14px; border-top: 1px solid #e5e5ea; color: #a0a0a8; font-size: 12px; }}
.alt b {{ color: #D97757; }}
</style></head><body>
<div class='ust'><h1>{HtmlKac(baslik)}</h1><div class='meta'>{zaman:d MMMM yyyy, HH:mm} · Dinamik Ada</div></div>
{soruBlok}
{g}
<div class='alt'><b>Dinamik Ada</b> ile oluşturuldu · yazdırmak için Ctrl+P, hedef olarak PDF seçin</div>
</body></html>";
    }

    private static string HtmlKac(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private void SoruKopyala_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(SoruCevap.Text); _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Yanıt kopyalandı", "", Simge: "", SaniyeOverride: 2)); } catch { }
    }

    private void SoruSifirla_Click(object sender, RoutedEventArgs e)
    {
        _claude.GecmisiSil();
        SoruCevap.Text = ""; SoruDurum.Text = "";
        SoruCevapKaydir.Visibility = Visibility.Collapsed; SoruAltSatir.Visibility = Visibility.Collapsed;
        if (_genis) Genislet();
    }

    /// Soruyu gönder, yanıtı panelde göster; panel kapalıysa duyuruyla haber ver
    private async Task SoruGonderAsync(string soru, string? gosterilenSoru = null)
    {
        if (_soruBekliyor) return;
        _soruBekliyor = true; _soruSonKullanim = DateTime.Now;
        _sonSoru = gosterilenSoru ?? soru;
        SoruIpucu.Text = gosterilenSoru ?? (soru.Length > 60 ? soru[..60] + "…" : soru);
        SoruCevap.Text = "Claude düşünüyor…";
        SoruDurum.Text = _claude.Kaynak;
        SoruCevapKaydir.Visibility = Visibility.Visible; SoruAltSatir.Visibility = Visibility.Visible;
        if (_genis && _soruGoster) Genislet();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (ok, yanit) = await _claude.SorAsync(soru);
        _soruBekliyor = false; _soruSonKullanim = DateTime.Now;
        SoruCevap.Text = ok ? yanit : "Yanıt alınamadı: " + yanit;
        SoruDurum.Text = $"{_claude.Kaynak} · {sw.Elapsed.TotalSeconds:0.0} sn";
        SoruIpucu.Text = "Devam sorusu yaz… Enter gönderir";
        Gunluk($"claude: ok={ok} sure={sw.ElapsedMilliseconds}ms soru='{(soru.Length > 40 ? soru[..40] : soru)}' yanit={yanit.Length} karakter");
        if (_genis && GenisSoru.Visibility == Visibility.Visible) Genislet();
        else _kuyruk.Ekle(new Duyuru(ok ? DuyuruTuru.Basari : DuyuruTuru.Uyari, ok ? "Claude yanıtladı" : "Claude yanıt veremedi",
            yanit.Length > 70 ? yanit[..70] + "…" : yanit, Simge: "", SaniyeOverride: 6));
    }

    private async void SeciliOzetle_Click(object sender, RoutedEventArgs e)
    {
        string m = await SeciliMetinAlAsync();
        if (m.Length == 0) return;
        _ = SoruGonderAsync("Şu metni Türkçe, kısa özetle:\n\n" + m, "Seçili metni özetle");
    }

    private async void SeciliCevir_Click(object sender, RoutedEventArgs e)
    {
        string m = await SeciliMetinAlAsync();
        if (m.Length == 0) return;
        _ = SoruGonderAsync("Şu metni çevir: Türkçe ise İngilizceye, değilse Türkçeye. Yalnız çeviriyi ver:\n\n" + m, "Seçili metni çevir");
    }

    private void PanoOzetle_Click(object sender, RoutedEventArgs e)
    {
        string m = "";
        try { if (Clipboard.ContainsText()) m = Clipboard.GetText().Trim(); } catch { }
        if (m.Length == 0) { _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Panoda metin yok", "", Simge: "", SaniyeOverride: 3)); return; }
        _ = SoruGonderAsync("Şu metni Türkçe, kısa özetle:\n\n" + (m.Length > 40000 ? m[..40000] : m), "Panodaki metni özetle");
    }

    /// Kapsüle gelmeden önce önde olan pencereye Ctrl+C gönderir, panodaki yeni metni döndürür
    private async Task<string> SeciliMetinAlAsync()
    {
        IntPtr hedef = _onPlanOnceki;
        var simdi = GetForegroundWindow();
        if (simdi != IntPtr.Zero && simdi != _hwnd) hedef = simdi;
        if (hedef == IntPtr.Zero || hedef == _hwnd)
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Önce metni seçin", "Başka pencerede metni seçip kapsüle gelin", Simge: "", SaniyeOverride: 4));
            return "";
        }
        string eski = "";
        try { if (Clipboard.ContainsText()) eski = Clipboard.GetText(); Clipboard.Clear(); } catch { }
        if (simdi != hedef) { OnPlanaAl(hedef); await Task.Delay(180); }
        keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        string yeni = "";
        for (int i = 0; i < 6 && yeni.Length == 0; i++)
        {
            await Task.Delay(120);
            try { if (Clipboard.ContainsText()) yeni = Clipboard.GetText().Trim(); } catch { }
        }
        if (yeni.Length == 0)
        {
            try { if (eski.Length > 0) Clipboard.SetText(eski); } catch { }
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Seçili metin yok", "Metni seçili bırakıp düğmeye basın", Simge: "", SaniyeOverride: 4));
            return "";
        }
        return yeni.Length > 40000 ? yeni[..40000] : yeni;
    }

    private const byte VK_C = 0x43;   // VK_CONTROL (int) klavye kancası bölümünde tanımlı

    // ---------- Oyun katmanı ----------

    private void MiniGuncelle()
    {
        MiniSaat.Text = _sesSeridi ? _sesSeridiMetin : DateTime.Now.ToString("HH:mm");
        MiniCpu.Text = _sistem.CpuYuzde.ToString();
        MiniRam.Text = _sistem.RamYuzde.ToString();
        MiniGpuKutu.Visibility = _gpu.Var && _gpu.Sicaklik >= 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_gpu.Sicaklik >= 0)
        {
            MiniGpu.Text = $"{_gpu.Sicaklik}°";
            MiniGpuYuk.Text = $"%{_gpu.Kullanim}";
            MiniGpu.Foreground = _gpu.Sicaklik >= 85 ? new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A))
                               : _gpu.Sicaklik >= 75 ? (Brush)FindResource("Vurgu") : (Brush)FindResource("MetinBirincil");
        }
        bool mikAcik = _gizlilik.MikrofonKullanan.Length > 0, kamAcik = _gizlilik.KameraKullanan.Length > 0;
        MiniGizlilik.Visibility = mikAcik || kamAcik ? Visibility.Visible : Visibility.Collapsed;
        MiniGizlilikGlif.Text = ((char)(kamAcik ? 0xE722 : 0xE720)).ToString();
        MiniGizlilik.ToolTip = kamAcik ? "Kamera kullanımda" : "Mikrofon kullanımda";
        MiniMesaj.Visibility = _okunmamis.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        MiniMesajSayi.Text = _okunmamis.Count.ToString();

        // İçerik büyüdüyse (GPU değeri sonradan geldi, rozet çıktı) şeridi yeniden genişlet
        if (MiniKatman.Visibility == Visibility.Visible && !_genis && _aktifDuyuru == null)
        {
            MiniIcerik.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = Math.Max(120, MiniIcerik.DesiredSize.Width + 4);
            if (Math.Abs(w - Ada.Width) > 2)
                Ada.BeginAnimation(WidthProperty, new DoubleAnimation(w, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    private string OnPlanSurecAdi()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return "";
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == 0) return "";
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch { return ""; }
    }

    // ---------- Şarkı sözleri ----------

    private void SozSatiriGoster(string satir)
    {
        var cik = new DoubleAnimation(0, TimeSpan.FromMilliseconds(90));
        cik.Completed += (_, _) => { SozMetin.Text = satir; SozMetin.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160))); };
        SozMetin.BeginAnimation(OpacityProperty, cik);
    }

    private async Task SozYukleAsync(MedyaDurumu d)
    {
        if (!_ayar.SozlerAcik || !d.VarMi) { SozGorunumAyarla(false); return; }
        var (_, sure) = _medya.ZamanCizelgesi();
        bool var = await _soz.YukleAsync(d.Baslik, d.Sanatci, sure);
        if (!ReferenceEquals(_durum, d) && (_durum.Baslik != d.Baslik || _durum.Sanatci != d.Sanatci)) return;   // parça değişti
        _soz.Sifirla();
        _sonSozSatiri = "";
        SozMetin.Text = var ? "♪" : "";
        SozGorunumAyarla(var);
        KompaktBaslikGuncelle();
        Gunluk($"sozler: {d.Sanatci} - {d.Baslik} -> {(var ? (_soz.Zamanli ? "zamanli" : "duz") + " (" + _soz.Kaynak + ")" : "yok")}");
    }

    /// Söz satırını aç/kapat; medya paneli açıkken yüksekliği animasyonla uyarla
    private void SozGorunumAyarla(bool goster)
    {
        bool onceki = SozMetin.Visibility == Visibility.Visible;
        SozMetin.Visibility = goster ? Visibility.Visible : Visibility.Collapsed;
        GenisMedya.Height = GenisMedyaYukseklik + (goster ? SozEkYukseklik : 0);
        if (onceki != goster && _genis && GenisMedya.Visibility == Visibility.Visible && GenisMedya.Opacity > 0.5)
            Ada.BeginAnimation(HeightProperty, new DoubleAnimation(GenisMedya.Height, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    // ---------- Spotify ----------

    private bool SpotifyCaliyor => _durum.VarMi && _durum.Aumid.Contains("Spotify", StringComparison.OrdinalIgnoreCase);

    private async Task SpotifyDurumAsync()
    {
        if (!SpotifyCaliyor || !_spotify.Hazir) { _spotifyParca = null; SpotifyDugmeleriGuncelle(); return; }
        if (_spotify.PremiumGerekli) { _spotifyParca = null; SpotifyDugmeleriGuncelle(); return; }   // API kapalı: kısayol yolu
        string baslik = _durum.Baslik, sanatci = _durum.Sanatci;
        var (parca, begenildi) = await _spotify.SimdikiAsync();
        // API çalanı bilmiyorsa ya da başka parça söylüyorsa (gecikme): adıyla ara
        if (parca == null || !string.Equals(parca.Ad, baslik, StringComparison.OrdinalIgnoreCase))
        {
            Gunluk($"spotify: currently-playing -> {(parca == null ? "yok (" + _spotify.SonHata + ")" : parca.Ad)}; aramaya düşülüyor: {baslik}");
            var (p2, b2) = await _spotify.AraAsync(baslik, sanatci);
            if (p2 != null) { parca = p2; begenildi = b2; }
        }
        if (_durum.Baslik != baslik) return;   // bu arada parça değişti
        _spotifyParca = parca; _spotifyBegenildi = begenildi;
        SpotifyDugmeleriGuncelle();
        Gunluk($"spotify: parca={(parca?.Ad ?? "yok")} begenildi={begenildi} hata={_spotify.SonHata}");
        if (parca == null)
        {
            // Spotify parça değişimini birkaç saniye geç bildirebilir: bir kez daha dene
            await Task.Delay(3000);
            if (_durum.Baslik == baslik && _spotifyParca == null && SpotifyCaliyor)
            {
                var (p3, b3) = await _spotify.SimdikiAsync();
                if (p3 == null) (p3, b3) = await _spotify.AraAsync(baslik, sanatci);
                if (_durum.Baslik == baslik) { _spotifyParca = p3; _spotifyBegenildi = b3; SpotifyDugmeleriGuncelle(); Gunluk($"spotify (tekrar): parca={(p3?.Ad ?? "yok")} hata={_spotify.SonHata}"); }
            }
        }
    }

    private void SpotifyDugmeleriGuncelle()
    {
        bool goster = SpotifyCaliyor && _spotify.Hazir;
        BegenDugme.Visibility = goster ? Visibility.Visible : Visibility.Collapsed;
        KuyrukDugme.Visibility = goster && !_spotify.PremiumGerekli ? Visibility.Visible : Visibility.Collapsed;
        if (!goster) return;
        if (_spotify.PremiumGerekli)
        {
            // Web API yok: Spotify'ın kendi kısayolu (Alt+Shift+B) ile beğen; durum okunamaz, kalp nötr
            BegenDugme.IsEnabled = true;
            BegenDugme.Content = "";
            BegenDugme.Foreground = (Brush)FindResource("MetinBirincil");
            BegenDugme.ToolTip = "Beğen / beğeniyi kaldır (Spotify kısayoluyla; Web API Premium istediği için durum okunamıyor)";
            return;
        }
        BegenDugme.IsEnabled = _spotifyParca != null;
        BegenDugme.Content = _spotifyBegenildi ? "" : "";
        BegenDugme.Foreground = _spotifyBegenildi ? (Brush)FindResource("Vurgu") : (Brush)FindResource("MetinBirincil");
        BegenDugme.ToolTip = _spotifyBegenildi ? "Beğenilenlerden çıkar" : "Beğenilenlere ekle";
    }

    private async void Begen_Click(object sender, RoutedEventArgs e)
    {
        if (_spotify.PremiumGerekli) { await SpotifyKisayolBegenAsync(); return; }
        if (_spotifyParca == null) { await SpotifyDurumAsync(); if (_spotifyParca == null) { if (_spotify.PremiumGerekli) await SpotifyKisayolBegenAsync(); return; } }
        bool hedef = !_spotifyBegenildi;
        BegenDugme.IsEnabled = false;
        bool ok = await _spotify.BegenAyarlaAsync(_spotifyParca.Id, hedef);
        if (ok) _spotifyBegenildi = hedef;
        SpotifyDugmeleriGuncelle();
        _kuyruk.Ekle(ok
            ? new Duyuru(DuyuruTuru.Basari, hedef ? "Beğenildi" : "Beğeni kaldırıldı", _spotifyParca.Ad, Simge: hedef ? "" : "", SaniyeOverride: 3, Anahtar: "begen")
            : new Duyuru(DuyuruTuru.Uyari, "Spotify'a ulaşılamadı", _spotify.SonHata, Simge: "", SaniyeOverride: 4, Anahtar: "begen"));
    }

    /// Web API kapalıyken: Spotify penceresini kısa süre öne al, Alt+Shift+B (Beğenilenlere ekle/çıkar) gönder, odağı geri ver
    private async Task SpotifyKisayolBegenAsync()
    {
        IntPtr spotify = IntPtr.Zero; bool kucuktu = false;
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("Spotify"))
                if (p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.Length > 0) { spotify = p.MainWindowHandle; break; }
        }
        catch { }
        if (spotify == IntPtr.Zero)
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Spotify penceresi bulunamadı", "Masaüstü uygulaması açık olmalı", Simge: "", SaniyeOverride: 4, Anahtar: "begen"));
            return;
        }
        IntPtr onceki = GetForegroundWindow();
        kucuktu = IsIconic(spotify);
        BegenDugme.IsEnabled = false;
        OnPlanaAl(spotify);
        await Task.Delay(kucuktu ? 600 : 250);
        const byte VK_MENU = 0x12, VK_SHIFT = 0x10, VK_B = 0x42;
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero); keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
        keybd_event(VK_B, 0, 0, UIntPtr.Zero); keybd_event(VK_B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        await Task.Delay(250);
        if (kucuktu) ShowWindow(spotify, SW_MINIMIZE);
        if (onceki != IntPtr.Zero && onceki != spotify && onceki != _hwnd) OnPlanaAl(onceki);
        BegenDugme.IsEnabled = true;
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Spotify'da beğeni değiştirildi", _durum.Baslik, Simge: "", SaniyeOverride: 3, Anahtar: "begen"));
        Gunluk($"spotify kisayol begen: {_durum.Baslik} (pencere {(kucuktu ? "kucuktu" : "acikti")})");
    }

    private async void Kuyruk_Click(object sender, RoutedEventArgs e)
    {
        var liste = await _spotify.KuyrukAsync();
        var menu = new ContextMenu { PlacementTarget = KuyrukDugme, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
        menu.Items.Add(new MenuItem { Header = liste.Count == 0 ? "Sırada parça yok" : "Sırada", IsEnabled = false, FontWeight = FontWeights.SemiBold });
        for (int i = 0; i < liste.Count; i++)
        {
            var p = liste[i];
            var oge = new MenuItem { Header = $"{i + 1}.  {p.Ad}", InputGestureText = p.Sanatci, ToolTip = i == 0 ? "Tıkla: bu parçaya geç" : null, IsEnabled = i == 0 };
            if (i == 0) oge.Click += async (_, _) => await _medya.SonrakiAsync();
            menu.Items.Add(oge);
        }
        menu.IsOpen = true;
    }

    private async Task SaniyeAsync()
    {
        _saniyeSayac++;

        if (_ayar.MikrofonKameraAcik) _gizlilik.Tik();

        _sistem.Tik();
        if (_ayar.AgAcik) { _ag.Tik(); VpnDegisimDuyur(); }
        RuhHaliTik();
        HatirlaticiKontrol();
        if (_genis && GenisBos.Visibility == Visibility.Visible) SistemHalkalariGuncelle();

        if (_hwnd != IntPtr.Zero)
        {
            _onPlanTamEkran = TamEkranServisi.OnPlanTamEkranMi(_hwnd, TamEkranServisi.MonitorTutamaci(_hwnd));
            // Tam ekran OYUN ise (tarayıcı/video oynatıcı değilse) ve oyun katmanı açıksa: gizlenme, ince şerit ol
            bool oyun = _onPlanTamEkran && _ayar.OyunKatmaniAcik && !VideoSurecleri.Contains(OnPlanSurecAdi(), StringComparer.OrdinalIgnoreCase);
            if (oyun != _mini)
            {
                _mini = oyun;
                OyunDurumDegisti(oyun);
                Gunluk($"oyun katmani: {(oyun ? "acik" : "kapali")} surec={OnPlanSurecAdi()}");
                if (oyun && _tamEkranGizli) { _tamEkranGizli = false; Ada.Visibility = Visibility.Visible; }
                if (!_genis && _aktifDuyuru == null) Daralt();
            }
            if (_mini) { _gpu.Tik(); if (_gpu.Sicaklik > _oyunGpuTepe) _oyunGpuTepe = _gpu.Sicaklik; }
            bool video = _onPlanTamEkran && !_mini;
            if (video != _videoAktif) { _videoAktif = video; IzlemeDurumDegisti(video); }
            bool gizle = _onPlanTamEkran && !_mini;
            if (_ayar.TamEkrandaGizle && gizle != _tamEkranGizli)
            {
                _tamEkranGizli = gizle;
                Ada.Visibility = gizle ? Visibility.Hidden : Visibility.Visible;
            }
        }

        if (_ayar.CanavarAcik) _etkinlik.Tik(_durum.VarMi && _durum.Oynuyor, _onPlanTamEkran, _ayar.UykuDakika);

        if (_ayar.BildirimAcik && _saniyeSayac % 2 == 0) await _bildirim.TikAsync();
        if (_ayar.WaServisAcik && _wa != null && _saniyeSayac % 2 == 1) await WaTikAsync();
        if (_ayar.SunucuAcik && _ayar.SunucuAdres.Trim().Length > 0 && (_saniyeSayac == 5 || _saniyeSayac % 60 == 0)) _ = SunucuOlcAsync();
        if (_ayar.HavaAcik && _saniyeSayac % 60 == 0) await _hava.TikAsync(_ayar.HavaSehir);
    }

    private static string SureBicimle(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    // ---------- Modül görünümleri ----------

    private void SesDegisti(float seviye, bool sessiz)
    {
        if (_ayar.SesOsdGizle) { _osdSayac = 0; _osdZaman.Stop(); _osdZaman.Start(); }   // Windows'un ses barını bastır
        BosSes.Guncelle(seviye, sessiz);
        if (_tamEkranGizli && !_mini) { _sesSeridiMetin = (sessiz ? "sessiz" : $"ses %{(int)Math.Round(seviye * 100)}"); SesSeridiGoster(); return; }   // tam ekran video: kısa şerit
        if (_genis && GenisMedya.Visibility == Visibility.Visible)
        {
            // Medya paneli açık: duyuru yerine kısa süreli rozet (ses çubuğu kaldırıldı)
            MedyaSesGlif.Text = sessiz || seviye <= 0 ? "" : seviye < 0.34 ? "" : seviye < 0.67 ? "" : "";
            MedyaSesYuzde.Text = sessiz ? "sessiz" : $"%{(int)Math.Round(seviye * 100)}";
            MedyaSesRozet.Visibility = Visibility.Visible;
            _sesRozetZaman.Stop(); _sesRozetZaman.Start();
            return;
        }
        if (!_ayar.SesAcik || _genis) return;
        string glif = sessiz || seviye <= 0 ? "" : seviye < 0.34 ? "" : seviye < 0.67 ? "" : "";
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Ses, sessiz ? "Sessiz" : "Ses", Simge: glif,
            Oran: sessiz ? 0 : seviye, SaniyeOverride: 2, Anahtar: "ses"));
    }

    private void GizlilikUygula(bool mikrofon, bool kamera)
    {
        MikrofonNokta.Visibility = mikrofon ? Visibility.Visible : Visibility.Collapsed;
        KameraNokta.Visibility = kamera ? Visibility.Visible : Visibility.Collapsed;
        KompaktGizlilik.Visibility = mikrofon || kamera ? Visibility.Visible : Visibility.Collapsed;

        var parcalar = new List<string>();
        if (mikrofon) parcalar.Add("Mikrofon: " + (_gizlilik.MikrofonKullanan is { Length: > 0 } m ? m : "kullanımda"));
        if (kamera) parcalar.Add("Kamera: " + (_gizlilik.KameraKullanan is { Length: > 0 } k ? k : "kullanımda"));
        string metin = string.Join("  ·  ", parcalar);
        GenisGizlilik.Text = metin;
        GenisBosGizlilik.Text = metin;
        GenisBosGizlilik.Visibility = metin.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Toplantı modu: mikrofon açıkken duyurular sessiz, maskot meşgul; kapanınca özet
        bool toplantiYeni = mikrofon && _ayar.ToplantiModuAcik;
        if (toplantiYeni != _toplanti)
        {
            _toplanti = toplantiYeni;
            KompaktCanavar.MesgulMu = _toplanti;
            GenisCanavar.MesgulMu = _toplanti;
            if (_toplanti)
            {
                _toplantiSayac = 0;
                _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Toplantı modu", $"{(_gizlilik.MikrofonKullanan is { Length: > 0 } m0 ? m0 + " · " : "")}bildirimler sessiz", Simge: "", SaniyeOverride: 3, Anahtar: "mik"));
            }
            else
            {
                string ozet = _toplantiSayac == 0 ? "sessiz bir toplantıydı" : $"{_toplantiSayac} bildirim bekledi";
                _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Toplantı bitti", ozet + (_okunmamis.Count > 0 ? " · okunmamış mesaj var" : ""), Simge: "", SaniyeOverride: 5));
                KompaktIcerikGuncelle();
            }
        }
        else if (mikrofon && _ayar.MikrofonKameraAcik && !_ayar.ToplantiModuAcik)
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Mikrofon kullanımda", _gizlilik.MikrofonKullanan, Simge: "", SaniyeOverride: 3, Anahtar: "mik"));
        if (kamera && _ayar.MikrofonKameraAcik)
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Kamera kullanımda", _gizlilik.KameraKullanan, Simge: "", SaniyeOverride: 3, Anahtar: "kam"));

        if (!_genis && _aktifDuyuru == null) Daralt(animasyonlu: true);
    }

    private void HavaUygula()
    {
        bool goster = _ayar.HavaAcik && _hava.Hazir;
        KompaktHava.Visibility = goster && !_ayar.CanavarAcik ? Visibility.Visible : Visibility.Collapsed;
        GenisHava.Visibility = goster ? Visibility.Visible : Visibility.Collapsed;
        if (!goster) return;
        KompaktHavaSimge.Text = _hava.Simge;
        KompaktHavaDerece.Text = $"{_hava.Sicaklik}°";
        GenisHavaSimge.Text = _hava.Simge;
        GenisHavaDerece.Text = $"{_hava.Sicaklik}°";
        GenisHavaAciklama.Text = string.IsNullOrEmpty(_hava.Yer) ? _hava.Aciklama : $"{_hava.Aciklama} · {_hava.Yer}";
        if (!_genis && _aktifDuyuru == null) Daralt(animasyonlu: true);
    }

    // ---------- Bildirimler: duyuru + okunmamış çipi + geniş mesaj paneli ----------

    private void BildirimGeldi(Bildirim b)
    {
        if (!_ayar.BildirimAcik) return;
        // Köprü bağlıyken WhatsApp mesajı zaten köprüden (numarasıyla) geliyor; Windows bildirimini yoksay
        bool waBildirim = b.Uygulama.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) || b.Aumid.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
        if (waBildirim && _wa is { Hazir: true } && b.Aumid != "wa-servis") return;
        // Yalnız son mesaj tutulur (kullanıcı isteği); öncekiler okunmuş sayılır
        _okunmamis.Clear();
        _okunmamis.Add(b);
        _ozet.Mesaj(waBildirim, waBildirim ? b.Baslik.Split(" · ").Last() : "");
        KompaktBildirimGuncelle();
        Gunluk($"bildirim geldi: {b.Uygulama} / {b.Baslik} -> okunmamis={_okunmamis.Count} genis={_genis} aktifDuyuru={_aktifDuyuru != null}");
        _ = NumarayiOtomatikOgrenAsync(b);

        string govdeTekSatir = b.Govde.Replace("\n", " · ");
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bildirim,
            Baslik: b.Baslik,
            Metin: string.IsNullOrEmpty(govdeTekSatir) ? b.Uygulama : $"{b.Uygulama} · {govdeTekSatir}",
            Logo: b.Logo,
            SaniyeOverride: 6));
    }

    // ---- Mesaja tıkla: uygulamada aç ----

    private void BildirimOge_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Bildirim b) return;
        UygulamaAc(b.Aumid);
        if (b.Id != 0) _bildirim.Kaldir(b.Id);
        _okunmamis.Clear();
        _bildirimGoruldu = false;
        Daralt();
    }

    private static void UygulamaAc(string aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return;
        try
        {
            // Paketli uygulama (WhatsApp, Discord Store sürümü): shell:AppsFolder; masaüstü uygulaması: ad ile dene
            string arg = aumid.Contains('!') || aumid.Contains('_') ? $"shell:AppsFolder\\{aumid}" : aumid;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
        }
        catch { }
    }

    // ---- Cevap: WhatsApp'ı kişi + metinle açar ----
    // Pencere odak almaz; yazı kutusuna tıklanınca geçici olarak izin verilir, kapanınca geri alınır.

    private bool _odakSerbest;

    private void OdakIzinVer(bool ver)
    {
        if (_hwnd == IntPtr.Zero || _odakSerbest == ver) return;
        int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, ver ? ex & ~WS_EX_NOACTIVATE : ex | WS_EX_NOACTIVATE);
        _odakSerbest = ver;
        if (ver) { Activate(); }
    }

    private void CevapKutu_Tik(object sender, MouseButtonEventArgs e)
    {
        OdakIzinVer(true);
        var kutu = sender as TextBox ?? CevapKutu;
        Dispatcher.BeginInvoke(() => { kutu.Focus(); Keyboard.Focus(kutu); });
    }

    private void CevapKutu_Odak(object sender, KeyboardFocusChangedEventArgs e) => CevapIpucu.Visibility = Visibility.Collapsed;

    private void CevapKutu_Tus(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; CevapGonder(); }
        else if (e.Key == Key.Escape) { e.Handled = true; CevapKapat(); }
    }

    private void CevapGonder_Click(object sender, RoutedEventArgs e) => CevapGonder();

    private async void CevapGonder()
    {
        string metin = CevapKutu.Text.Trim();
        var b = _okunmamis.FirstOrDefault();
        if (string.IsNullOrEmpty(metin) || b == null) return;

        bool whatsapp = b.Uygulama.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) || b.Aumid.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
        if (!whatsapp)
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Cevap yalnız WhatsApp için", $"{b.Uygulama} bildirimine buradan cevap verilemiyor", Simge: "", SaniyeOverride: 5));
            return;
        }

        // Numara: rehberden; yoksa kutuya girilen (rehbere kaydedilir)
        string? numara = RehberNumara(b.Baslik);
        string girilen = new string(NumaraKutu.Text.Where(char.IsDigit).ToArray());
        if (numara == null && girilen.Length >= 10)
        {
            if (girilen.Length == 10) girilen = "90" + girilen;          // 5xx... → 905xx...
            else if (girilen.StartsWith("0") && girilen.Length == 11) girilen = "9" + girilen;
            numara = girilen;
            _ayar.Rehber = (string.IsNullOrWhiteSpace(_ayar.Rehber) ? "" : _ayar.Rehber.TrimEnd() + "\n") + $"{b.Baslik.Trim()} = {numara}";
            try { _ayar.Kaydet(); } catch { }
        }

        // 1) Arka plan köprüsü hazırsa pencere açmadan gönder (numara ya da ad ile)
        if (_wa is { Hazir: true })
        {
            CevapKutu.IsEnabled = false;
            var s = await _wa.GonderAsync(numara, b.Baslik, metin, _waJid.GetValueOrDefault(b.Baslik));
            CevapKutu.IsEnabled = true;
            if (s.Ok)
            {
                if (numara == null && s.Mesaj.Length is >= 10 and <= 13)   // köprü adı çözdüyse rehbere yaz (LID değil, gerçek numara)
                {
                    _ayar.Rehber = (string.IsNullOrWhiteSpace(_ayar.Rehber) ? "" : _ayar.Rehber.TrimEnd() + "\n") + $"{b.Baslik.Trim()} = {s.Mesaj}";
                    try { _ayar.Kaydet(); } catch { }
                }
                if (b.Id != 0) _bildirim.Kaldir(b.Id);
                _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "Gönderildi", b.Baslik, Simge: "", SaniyeOverride: 4));
                CevapKapat();
                _okunmamis.Clear();
                _bildirimGoruldu = false;
                Daralt();
                return;
            }
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Arka planda gönderilemedi, WhatsApp açılıyor", s.Mesaj, Simge: "", SaniyeOverride: 5));
        }

        // 2) Köprü yoksa: WhatsApp'ı aç (odak geri verilir)
        string url = numara != null
            ? $"whatsapp://send?phone={numara}&text={Uri.EscapeDataString(metin)}"
            : $"whatsapp://send?text={Uri.EscapeDataString(metin)}";   // numara yoksa WhatsApp kişi seçtirir
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "WhatsApp açılamadı", "whatsapp:// bağlantısı tanınmıyor", Simge: "", SaniyeOverride: 5)); return; }

        if (b.Id != 0) _bildirim.Kaldir(b.Id);
        bool otomatik = numara != null && _ayar.WhatsAppOtomatikGonder;
        if (otomatik) _ = OtomatikGonderAsync(b.Baslik);
        else _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, numara != null ? "WhatsApp açıldı, Enter ile gönderin" : "WhatsApp açıldı, kişiyi seçip gönderin",
            b.Baslik, Simge: "", SaniyeOverride: 5));
        CevapKapat();
        _okunmamis.Clear();
        _bildirimGoruldu = false;
        Daralt();
    }

    /// WhatsApp penceresi öne gelince (en çok 8 sn bekler) Enter basar: metin hazır yazılmış olur, Enter gönderir.
    /// Sonra önceki pencereyi geri öne alır; WhatsApp önceden küçükse yeniden küçültür. Kullanıcının işi bölünmesin.
    private async Task OtomatikGonderAsync(string kime)
    {
        IntPtr onceki = GetForegroundWindow();
        IntPtr waPencere = WhatsAppPenceresi();
        bool waKucuktu = waPencere == IntPtr.Zero || IsIconic(waPencere) || !IsWindowVisible(waPencere);

        for (int i = 0; i < 80; i++)
        {
            await Task.Delay(100);
            if (OnPlanWhatsAppMi())
            {
                await Task.Delay(700);   // sohbet ve yazı kutusu yüklensin
                if (!OnPlanWhatsAppMi()) break;
                keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                await Task.Delay(450);   // gönderim tamamlansın

                // Odağı geri ver
                var wa = GetForegroundWindow();
                if (waKucuktu && wa != IntPtr.Zero) ShowWindow(wa, SW_MINIMIZE);
                if (onceki != IntPtr.Zero && onceki != wa) OnPlanaAl(onceki);

                _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "WhatsApp'ta gönderildi", kime, Simge: "", SaniyeOverride: 4));
                return;
            }
        }
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "WhatsApp öne gelmedi", "Metin hazır, WhatsApp'ta Enter'a basın", Simge: "", SaniyeOverride: 5));
    }

    private static IntPtr WhatsAppPenceresi()
    {
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try { if (p.ProcessName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) && p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle; }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    /// Windows'un ön plan kilidini aşmak için giriş kuyruğuna bağlanıp öne alır
    private static void OnPlanaAl(IntPtr hwnd)
    {
        try
        {
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
            uint onPlanIs = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            uint benimIs = GetCurrentThreadId();
            if (onPlanIs != benimIs) AttachThreadInput(benimIs, onPlanIs, true);
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);
            if (onPlanIs != benimIs) AttachThreadInput(benimIs, onPlanIs, false);
        }
        catch { }
    }

    private const int SW_MINIMIZE = 6, SW_RESTORE = 9;
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private static bool OnPlanWhatsAppMi()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            GetWindowThreadProcessId(h, out uint pid);
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private const byte VK_RETURN = 0x0D;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    private void CevapKapat()
    {
        CevapKutu.Text = "";
        NumaraKutu.Text = "";
        CevapIpucu.Visibility = Visibility.Visible;
        Keyboard.ClearFocus();
        OdakIzinVer(false);
    }

    private readonly RehberServisi _rehber = new();

    /// Önce elle yazılan satırlar ("Ad = 905…"), sonra .vcf rehberi (birebir, sadeleştirilmiş, tek adaylı içerme).
    private string? RehberNumara(string ad)
    {
        foreach (var satir in (_ayar.Rehber ?? "").Split('\n'))
        {
            int i = satir.IndexOf('=');
            if (i <= 0) continue;
            string k = satir[..i].Trim(), v = new string(satir[(i + 1)..].Where(char.IsDigit).ToArray());
            if (v.Length >= 10 && string.Equals(k, ad.Trim(), StringComparison.OrdinalIgnoreCase)) return v;
        }
        return _rehber.Bul(ad);
    }

    private void RehberiYukle()
    {
        string yol = string.IsNullOrWhiteSpace(_ayar.RehberDosyasi) ? RehberServisi.VarsayilanYol : _ayar.RehberDosyasi;
        int n = _rehber.Yukle(yol);
        Gunluk($"rehber vcf: {yol} -> {n} kayit");
    }

    /// Kapsüle bırakılan .vcf: uygulama klasörüne kopyalanır, rehber olarak yüklenir.
    private void RehberIceAktar(string yol)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Ayarlar.Klasor);
            if (!string.Equals(System.IO.Path.GetFullPath(yol), System.IO.Path.GetFullPath(RehberServisi.VarsayilanYol), StringComparison.OrdinalIgnoreCase))
                System.IO.File.Copy(yol, RehberServisi.VarsayilanYol, true);
            _ayar.RehberDosyasi = RehberServisi.VarsayilanYol;
            try { _ayar.Kaydet(); } catch { }
            RehberiYukle();
            _kuyruk.Ekle(_rehber.Sayi > 0
                ? new Duyuru(DuyuruTuru.Basari, $"Rehber yüklendi: {_rehber.Sayi} kayıt", "WhatsApp cevapları artık numara sormadan gider", Simge: "", SaniyeOverride: 6)
                : new Duyuru(DuyuruTuru.Uyari, "Rehber okunamadı", "Dosyada FN/TEL satırı bulunamadı", Simge: "", SaniyeOverride: 6));
        }
        catch (Exception ex)
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Rehber alınamadı", ex.Message, Simge: "", SaniyeOverride: 6));
        }
    }

    /// WhatsApp bildirimi geldiğinde bildirim veritabanındaki ham toast'tan numarayı öğrenmeye çalışır,
    /// bulursa rehbere yazar (kullanıcı numara girmek zorunda kalmaz). Bulamazsa yapıyı günlüğe not eder.
    private async Task NumarayiOtomatikOgrenAsync(Bildirim b)
    {
        bool wa = b.Uygulama.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) || b.Aumid.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
        if (!wa || RehberNumara(b.Baslik) != null) return;

        var toastlar = await Task.Run(() => BildirimVeritabani.SonToastlar("WhatsApp", 8));
        foreach (var t in toastlar)
        {
            var metinler = BildirimVeritabani.Metinler(t.Xml);
            if (metinler.Count == 0 || !string.Equals(metinler[0], b.Baslik, StringComparison.OrdinalIgnoreCase)) continue;

            string? launch = BildirimVeritabani.LaunchArgumani(t.Xml);
            // Teşhis: ham toast yapısı (metin ve rakamlar maskeli); numara gerçekten nerede, görelim
            string yapi = System.Text.RegularExpressions.Regex.Replace(t.Xml, @"\d", "#");
            yapi = System.Text.RegularExpressions.Regex.Replace(yapi, @">([^<]{3,})<", ">…<");
            Gunluk($"toast yapisi ({b.Baslik}): {yapi[..Math.Min(yapi.Length, 600)]}");

            // Yalnız kesin kalıp (JID / phone=): rastgele kimlik numaraları telefon sanılmasın
            string? numara = BildirimVeritabani.NumaraBul(launch);
            if (numara != null)
            {
                _ayar.Rehber = (string.IsNullOrWhiteSpace(_ayar.Rehber) ? "" : _ayar.Rehber.TrimEnd() + "\n") + $"{b.Baslik.Trim()} = {numara}";
                try { _ayar.Kaydet(); } catch { }
                Gunluk($"rehber ogrenildi: {b.Baslik} -> {numara[..4]}…");
                if (_genis && GenisBildirim.Visibility == Visibility.Visible) NumaraSatiri.Visibility = Visibility.Collapsed;
            }
            else Gunluk($"rehber ogrenilemedi: {b.Baslik} | launch={(launch == null ? "(yok)" : System.Text.RegularExpressions.Regex.Replace(launch, @"\d", "#"))}");
            return;
        }
        Gunluk($"rehber: veritabaninda eslesen toast yok ({b.Baslik})");
    }

    // ---------- WhatsApp Web köprüsü: durum, QR, gelen mesajlar ----------

    private async Task WaTikAsync()
    {
        if (_wa == null) return;
        bool oncekiHazir = _wa.Hazir, oncekiQr = _wa.QrVar;
        var d = await _wa.DurumAsync();
        if (d == null)
        {
            // Servis yanıt vermiyor (çökmüş ya da kapanmış): 30 sn'de bir yeniden başlatmayı dene
            if (DateTime.Now - _waSonBaslatma > TimeSpan.FromSeconds(30))
            {
                _waSonBaslatma = DateTime.Now;
                bool ok = _wa.Baslat();
                Gunluk($"wa-servis yeniden baslat: {ok}");
                if (oncekiHazir) _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "WhatsApp köprüsü durdu", ok ? "yeniden başlatılıyor" : "node ya da wa-servis bulunamadı", Simge: "", SaniyeOverride: 5));
            }
            return;
        }

        if (d.qrVar && !d.hazir)
        {
            // QR her 20 sn değişir; panel açıkken 3 sn'de bir, kapalıyken 10 sn'de bir tazele (süresi dolmuş kod gösterilmesin)
            bool panelAcik = _genis && GenisQr.Visibility == Visibility.Visible;
            if (DateTime.Now - _waSonQr > TimeSpan.FromSeconds(panelAcik ? 3 : 10))
            {
                var qr = await _wa.QrAsync();
                if (qr != null) { QrResim.Source = qr; _waSonQr = DateTime.Now; }
            }
            if (!oncekiQr && QrResim.Source != null)
                _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "WhatsApp bağlantısı için QR hazır", "Üzerine gelince kodu okutun", Simge: "", SaniyeOverride: 8));
            if (_genis && GenisQr.Visibility != Visibility.Visible && QrResim.Source != null) Genislet();
        }
        else if (d.hazir)
        {
            QrResim.Source = null;
            if (!oncekiHazir)
            {
                _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, "WhatsApp bağlandı", string.IsNullOrEmpty(d.ben) ? "arka plan gönderimi açık" : "+" + d.ben, Simge: "", SaniyeOverride: 5));
                if (_genis && GenisQr.Visibility == Visibility.Visible) Genislet();
            }
            if (d.bekleyen > 0)
                foreach (var g in await _wa.GelenAsync()) WaGelenIsle(g);
            if (d.telefondan > 0)
                foreach (var t in await _wa.TelefondanAsync()) TelefondanIsle(t);
        }
    }

    /// Köprüden gelen mesaj: rehbere numara yaz, kapsülde mesaj olarak göster
    private void WaGelenIsle(WaServisi.Gelen g)
    {
        // Geçmiş eşlemesi artığı: 90 sn'den eski mesajı gösterme (servis de süzüyor, bu ikinci kapı)
        if (DateTimeOffset.Now - DateTimeOffset.FromUnixTimeMilliseconds(g.zaman) > TimeSpan.FromSeconds(90)) return;
        string ad = string.IsNullOrWhiteSpace(g.ad) ? (string.IsNullOrEmpty(g.numara) ? "Bilinmeyen" : g.numara) : g.ad;
        // Yalnız gerçek numara öğrenilir: 10-13 hane (E.164). 15-16 haneli LID kimlikleri ve "numara = numara" satırları yazılmaz.
        if (g.numara.Length is >= 10 and <= 13 && ad != g.numara && RehberNumara(ad) == null && string.IsNullOrEmpty(g.grup))
        {
            _ayar.Rehber = (string.IsNullOrWhiteSpace(_ayar.Rehber) ? "" : _ayar.Rehber.TrimEnd() + "\n") + $"{ad.Trim()} = {g.numara}";
            try { _ayar.Kaydet(); } catch { }
        }
        string baslik = string.IsNullOrEmpty(g.grup) ? ad : $"{g.grup} · {ad}";
        if (!string.IsNullOrEmpty(g.jid)) _waJid[baslik] = g.jid;   // cevap bu hedefe gider (LID olsa bile)
        BildirimGeldi(new Bildirim("WhatsApp", baslik, g.metin, _waLogo, DateTimeOffset.FromUnixTimeMilliseconds(g.zaman).LocalDateTime, "wa-servis", 0));
    }

    private ImageSource? _waLogo;   // açılışta asenkron yüklenir
    private readonly Dictionary<string, string> _waJid = new();   // bildirim başlığı -> köprüden gelen gerçek jid

    /// Eski sürümün rehbere yazdığı LID kimliklerini ("2214275423192141 = 2214275423192141") bir kez temizle
    private void RehberTemizle()
    {
        var satirlar = (_ayar.Rehber ?? "").Split('\n');
        var kalan = satirlar.Where(s =>
        {
            int i = s.IndexOf('=');
            if (i <= 0) return s.Trim().Length > 0;
            string k = s[..i].Trim(), v = new string(s[(i + 1)..].Where(char.IsDigit).ToArray());
            return v.Length <= 13 && k != v;
        }).ToArray();
        if (kalan.Length == satirlar.Length) return;
        _ayar.Rehber = string.Join("\n", kalan).Trim();
        try { _ayar.Kaydet(); } catch { }
        Gunluk($"rehber temizlendi: {satirlar.Length - kalan.Length} LID satiri silindi");
    }

    private void KompaktBildirimGuncelle()
    {
        if (_okunmamis.Count == 0) return;
        var son = _okunmamis[0];
        KompaktBildirimLogo.Source = son.Logo;
        KompaktBildirimBaslik.Text = son.Baslik;
        KompaktBildirimMetin.Text = son.Govde.Replace("\n", " ");
        KompaktBildirimSayi.Visibility = _okunmamis.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        KompaktBildirimSayiMetin.Text = _okunmamis.Count.ToString();
    }

    /// Kompakt kapsülde ne görünecek: okunmamış mesaj varsa çip, yoksa medya ya da canavar/saat.
    private void KompaktIcerikGuncelle()
    {
        bool mesaj = _okunmamis.Count > 0 && !_toplanti;   // toplantıda çip bile görünmez, bitince gelir
        KompaktBildirim.Visibility = mesaj ? Visibility.Visible : Visibility.Collapsed;
        KompaktMedya.Visibility = !mesaj && _durum.VarMi ? Visibility.Visible : Visibility.Collapsed;
        KompaktBos.Visibility = !mesaj && !_durum.VarMi ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CanavarUygula(CanavarModu mod, string aciklama)
    {
        bool goster = _ayar.CanavarAcik;
        bool degisti = (KompaktCanavar.Visibility == Visibility.Visible) != goster;
        KompaktCanavar.Visibility = goster ? Visibility.Visible : Visibility.Collapsed;
        GenisCanavar.Visibility = goster ? Visibility.Visible : Visibility.Collapsed;
        // Canavar varken kompakt kapsülde saat ve pil yok (kullanıcı isteği); kapatılırsa saat geri gelir
        KompaktSaat.Visibility = Visibility.Visible;   // canavarın yanında saat her zaman görünür
        KompaktPil.Visibility = goster ? Visibility.Collapsed : Visibility.Visible;
        KompaktCanavar.Mod = mod;
        GenisCanavar.Mod = mod;
        // Durum metni yerine renkli daire (kullanıcı isteği): işte yeşil, oyun/müzik turuncu, uyku kırmızı, normal soluk
        var renk = mod switch
        {
            CanavarModu.Is => Color.FromRgb(0x30, 0xD1, 0x58),
            CanavarModu.Oyun or CanavarModu.Muzik => Color.FromRgb(0xD9, 0x77, 0x57),
            CanavarModu.Uyku => Color.FromRgb(0xFF, 0x45, 0x3A),
            _ => Color.FromRgb(0x3A, 0x3A, 0x42),
        };
        string? ipucu = string.IsNullOrEmpty(aciklama) ? null : aciklama;
        KompaktCanavar.DurumAyarla(renk, ipucu);   // daire maskotun sol üst köşesinde
        GenisCanavar.DurumAyarla(renk, ipucu);
        GenisCanavarDurum.Visibility = Visibility.Collapsed;
        if (degisti) HavaUygula();
    }

    // ---------- Sürükle-bırak: dosyayı uzak sunucuya kopyala ----------

    private void Ada_DragEnter(object sender, DragEventArgs e)
    {
        bool dosya = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = dosya ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        _surukleCikis.Stop();
        if (!dosya || _surukleme) return;

        _surukleme = true;
        _duyuruSuresi.Stop();
        _daraltGecikme.Stop();
        _genis = false;
        _aktifDuyuru = new Duyuru(DuyuruTuru.Bilgi, "Sunucuya kopyalamak için bırakın", HedefKisa(), Simge: "");
        DuyuruDoldur(_aktifDuyuru);
        DuyuruBoyutla(ikiSatir: true);
    }

    private void Ada_DragLeave(object sender, DragEventArgs e)
    {
        // Çocuk öğeler arasında geçişte de Leave gelir: kısa gecikmeyle gerçekten çıktı mı bak
        _surukleCikis.Stop();
        _surukleCikis.Start();
    }

    private void Ada_Drop(object sender, DragEventArgs e)
    {
        _surukleCikis.Stop();
        _surukleme = false;
        _aktifDuyuru = null;
        _genis = false;
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] yollar || yollar.Length == 0) { Daralt(); return; }
        _dropSonrasi = true; // fare üstünde kalsa da duyurular hemen gösterilsin

        // .vcf bırakıldıysa rehber olarak al, hazneye koyma
        var vcf = yollar.Where(y => y.EndsWith(".vcf", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (vcf.Length > 0)
        {
            RehberIceAktar(vcf[0]);
            yollar = yollar.Except(vcf).ToArray();
            if (yollar.Length == 0) return;
        }

        // Hazneye al; "hemen" modunda bir de sunucuya yükle
        int eklenen = 0;
        foreach (var yol in yollar) if (_hazne.Ekle(yol) != null) eklenen++;
        if (eklenen > 0) { _hazneSonEkleme = DateTime.Now; _efekt.Cal(SesEfektServisi.Efekt.Birak); }
        if (_ayar.HazneModu == "hemen")
        {
            foreach (var yol in yollar) _ = KopyalaVeBildir(yol);
        }
        else if (eklenen == 0)
        {
            string ad = yollar.Length == 1 ? System.IO.Path.GetFileName(yollar[0].TrimEnd('\\', '/')) : $"{yollar.Length} öğe";
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, $"Zaten haznede: {ad}", "Aynı dosya ikinci kez eklenmez", Simge: "", SaniyeOverride: 3));
        }
        else
        {
            string ad = eklenen == 1 && yollar.Length == 1 ? System.IO.Path.GetFileName(yollar[0].TrimEnd('\\', '/')) : $"{eklenen} öğe";
            string ek = eklenen < yollar.Length ? $" ({yollar.Length - eklenen} tanesi zaten vardı)" : "";
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, $"Hazneye eklendi: {ad}", "Üzerine gelince yükle, zip'le ya da sürükle" + ek, Simge: "", SaniyeOverride: 4));
        }
    }

    private async Task KopyalaVeBildir(string yol)
    {
        string ad = System.IO.Path.GetFileName(yol.TrimEnd('\\', '/'));
        string anahtar = "yukle:" + ad;
        _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, $"Kopyalanıyor: {ad}", HedefKisa(), Simge: "", SaniyeOverride: 600, Anahtar: anahtar, Surecte: true));

        var s = await YuklemeServisi.KopyalaAsync(yol, _ayar.SurukleHedef, _ayar.SuruklePort, _ayar.SurukleAnahtar);

        _kuyruk.Ekle(s.Basarili
            ? new Duyuru(DuyuruTuru.Basari, $"Kopyalandı: {ad}", s.Mesaj, Simge: "", SaniyeOverride: 6, Anahtar: anahtar)
            : new Duyuru(DuyuruTuru.Uyari, $"Kopyalanamadı: {ad}", s.Mesaj, Simge: "", SaniyeOverride: 8, Anahtar: anahtar));
    }

    // ---------- Kısayol çubuğu: uygulama başlatıcılar ----------

    private static readonly (string Ad, string[] Adaylar)[] VarsayilanKisayollar =
    {
        ("VS Code", new[] { @"%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe", @"C:\Program Files\Microsoft VS Code\Code.exe" }),
        ("Claude",  new[] { @"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude", @"%LOCALAPPDATA%\AnthropicClaude\claude.exe" }),
        ("Chrome",  new[] { @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" }),
        ("Spotify", new[] { @"%APPDATA%\Spotify\Spotify.exe", @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" }),
        ("Discord", new[] { @"%LOCALAPPDATA%\Discord\app-*\Discord.exe", @"%LOCALAPPDATA%\Discord\Update.exe --processStart Discord.exe" }),
        ("Terminal", new[] { @"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", @"%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe" }),
    };

    private static string VarsayilanKisayolMetni()
    {
        var satirlar = new List<string>();
        foreach (var (ad, adaylar) in VarsayilanKisayollar)
            foreach (var a in adaylar)
            {
                string yol = Environment.ExpandEnvironmentVariables(a);
                if (yol.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) { satirlar.Add($"{ad} = {yol}"); break; }

                // Joker: "...\app-*\Discord.exe" → en yeni eşleşen klasör
                string exe = yol.Split(" --")[0];
                if (exe.Contains('*'))
                {
                    string kok = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(exe)!)!;
                    string klasorDeseni = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(exe)!);
                    string dosya = System.IO.Path.GetFileName(exe);
                    if (System.IO.Directory.Exists(kok))
                    {
                        var aday = System.IO.Directory.GetDirectories(kok, klasorDeseni).OrderByDescending(d => d).Select(d => System.IO.Path.Combine(d, dosya)).FirstOrDefault(System.IO.File.Exists);
                        if (aday != null) { satirlar.Add($"{ad} = {aday}"); break; }
                    }
                    continue;
                }
                if (System.IO.File.Exists(exe)) { satirlar.Add($"{ad} = {yol}"); break; }
            }
        return string.Join("\n", satirlar);
    }

    private async void KisayollariKur()
    {
        try { await KisayollariKurAsync(); }
        catch (Exception ex) { Gunluk("kisayol hata: " + ex); }
    }

    /// Ayardaki (ya da varsayılan) kısayol listesi: (ad, hedef)
    private List<(string Ad, string Hedef)> KisayolListesi()
    {
        string metin = string.IsNullOrWhiteSpace(_ayar.Kisayollar) ? VarsayilanKisayolMetni() : _ayar.Kisayollar;
        var liste = new List<(string, string)>();
        foreach (var satir in metin.Split('\n'))
        {
            int i = satir.IndexOf('=');
            if (i <= 0) continue;
            string ad = satir[..i].Trim(), hedef = satir[(i + 1)..].Trim();
            if (ad.Length > 0 && hedef.Length > 0) liste.Add((ad, hedef));
        }
        return liste;
    }

    private void KisayolListesiKaydet(List<(string Ad, string Hedef)> liste)
    {
        _ayar.Kisayollar = liste.Count == 0 ? "-" : string.Join("\n", liste.Select(k => $"{k.Ad} = {k.Hedef}"));   // "-": bilerek boş
        try { _ayar.Kaydet(); } catch { }
        KisayollariKur();
    }

    private async Task KisayollariKurAsync()
    {
        KisayolCubugu.Children.Clear();

        // İlk düğme: hazne (ataç + adet); haznede dosya varken görünür, tıklayınca hazne paneline geçer
        int hazneSayi = _hazne.Ogeler.Count;
        if (hazneSayi > 0)
        {
            var icerik = new StackPanel { Orientation = Orientation.Horizontal };
            icerik.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M9.5,3.5 L4.6,8.4 A2.4,2.4 0 0 0 8,11.8 L12.6,7.2 A3.8,3.8 0 0 0 7.2,1.8 L2.8,6.2"), Stroke = (Brush)FindResource("Vurgu"), StrokeThickness = 1.9, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = 15, Height = 15, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center });
            icerik.Children.Add(new TextBlock { Text = hazneSayi.ToString(), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(5, 0, 0, 0), Foreground = (Brush)FindResource("MetinBirincil"), VerticalAlignment = VerticalAlignment.Center });
            var hazneDugme = new Button
            {
                Style = (Style)FindResource("KisayolDugme"),
                Width = 38, Margin = new Thickness(0, 0, 12, 0),
                ToolTip = "Hazne", Content = icerik,
            };
            hazneDugme.Click += (_, _) => { _hazneGoster = true; FareBekleBaslat(); Genislet(); };
            KisayolCubugu.Children.Add(hazneDugme);
        }
        // Claude'a sor düğmesi (kaynak yoksa gizli)
        if (_ayar.ClaudeAcik && _claude.Hazir)
        {
            var sorDugme = new Button
            {
                Style = (Style)FindResource("KisayolDugme"), Margin = new Thickness(0, 0, 12, 0), ToolTip = "Claude'a sor · " + _claude.Kaynak,
                Content = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13, Foreground = (Brush)FindResource("Vurgu") },
            };
            sorDugme.Click += (_, _) => SoruPaneliAc(odakla: true);
            var sorMenu = new ContextMenu();
            var ozetOge = new MenuItem { Header = "Günün özeti" };
            ozetOge.Click += (_, _) => OzetGoster(otomatik: false);
            sorMenu.Items.Add(ozetOge);
            var haftaOge = new MenuItem { Header = "Haftalık rapor" };
            haftaOge.Click += (_, _) => OzetGoster(otomatik: false, haftalik: true);
            sorMenu.Items.Add(haftaOge);
            sorDugme.ContextMenu = sorMenu;
            KisayolCubugu.Children.Add(sorDugme);
        }

        var liste = KisayolListesi();
        for (int n = 0; n < liste.Count; n++)
        {
            var (ad, hamHedef) = liste[n];
            string hedef = Environment.ExpandEnvironmentVariables(hamHedef);
            int sira = n;

            var simge = new Image { Width = 22, Height = 22, Stretch = Stretch.Uniform };
            var dugme = new Button { Style = (Style)FindResource("KisayolDugme"), ToolTip = ad, Content = simge, Tag = hedef };
            dugme.Click += (_, _) => KisayolCalistir((string)dugme.Tag, ad);

            // Sağ tık: düzenleme menüsü
            var menu = new ContextMenu();
            var sola = new MenuItem { Header = "Sola taşı", IsEnabled = sira > 0 };
            sola.Click += (_, _) => { var l = KisayolListesi(); (l[sira - 1], l[sira]) = (l[sira], l[sira - 1]); KisayolListesiKaydet(l); };
            var saga = new MenuItem { Header = "Sağa taşı", IsEnabled = sira < liste.Count - 1 };
            saga.Click += (_, _) => { var l = KisayolListesi(); (l[sira + 1], l[sira]) = (l[sira], l[sira + 1]); KisayolListesiKaydet(l); };
            var kaldir = new MenuItem { Header = $"Kaldır: {ad}" };
            kaldir.Click += (_, _) => { var l = KisayolListesi(); if (sira < l.Count) l.RemoveAt(sira); KisayolListesiKaydet(l); };
            menu.Items.Add(sola); menu.Items.Add(saga); menu.Items.Add(new Separator()); menu.Items.Add(kaldir);
            dugme.ContextMenu = menu;
            KisayolCubugu.Children.Add(dugme);

            // Simge: paketli uygulama logosu ya da exe/.lnk'nin kabuk simgesi
            ImageSource? kaynak = null;
            if (hedef.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
            {
                kaynak = (await UygulamaBilgisi.AlAsync(hedef["shell:AppsFolder\\".Length..])).Simge;
                simge.Width = simge.Height = 31;   // paket logolarının kendi şeffaf kenar payı var; çipe göre büyüt
            }
            else
            {
                string exe = hedef.Split(" --")[0].Trim('"');
                if (System.IO.File.Exists(exe)) kaynak = HazneDeposu.DosyaSimgesi(exe);
            }
            if (kaynak != null) simge.Source = kaynak;
            else dugme.Content = new TextBlock { Text = ad[..1].ToUpperInvariant(), FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("MetinBirincil") };
        }

        // Son düğme: ekle
        var ekle = new Button
        {
            Style = (Style)FindResource("KisayolDugme"), Margin = new Thickness(0), ToolTip = "Kısayol ekle (exe, kısayol dosyası)",
            Content = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13, Foreground = (Brush)FindResource("MetinIkincil") },
        };
        ekle.Click += (_, _) => KisayolEkleDialog();
        KisayolCubugu.Children.Add(ekle);

        // Tek satıra sığmazsa düğmeleri orantılı küçült (WrapPanel alt satırı panel yüksekliğine takılıp kırpılır)
        var dugmeler = KisayolCubugu.Children.OfType<FrameworkElement>().ToList();
        double kullanilabilir = GenisBosGenislik - 40;
        double gerek = dugmeler.Sum(d => (double.IsNaN(d.Width) ? 60 : d.Width) + d.Margin.Right);
        if (gerek > kullanilabilir)
        {
            double g = 5;
            double sabit = dugmeler.Where(d => double.IsNaN(d.Width)).Sum(d => 60.0);
            int n = dugmeler.Count, degisken = dugmeler.Count(d => !double.IsNaN(d.Width));
            double s = Math.Clamp(Math.Floor((kullanilabilir - (n - 1) * g - sabit) / Math.Max(1, degisken)), 26, 38);
            double k = s / 38;
            for (int i = 0; i < n; i++)
            {
                dugmeler[i].LayoutTransform = new ScaleTransform(k, k);
                dugmeler[i].Margin = new Thickness(0, 0, i == n - 1 ? 0 : g, 0);
            }
        }

        Gunluk($"kisayol cubugu: {liste.Count} kisayol, {dugmeler.Count} dugme, olcek {(gerek > kullanilabilir ? "kucultuldu" : "1.0")}");
    }

    private void KisayolEkleDialog()
    {
        // Kurulu uygulamalar listesi (Başlat menüsü + Store); dosya seçimi yedek olarak içinde
        var ekranlar = Forms.Screen.AllScreens;
        var ekran = ekranlar[Math.Clamp(_ayar.Ekran, 0, ekranlar.Length - 1)];
        var secici = new Kontroller.UygulamaSecici(ekran);
        OdakIzinVer(true);
        bool? sonuc = secici.ShowDialog();
        OdakIzinVer(false);
        if (sonuc != true || secici.Secim == null) return;

        var (ad, yol) = secici.Secim.Value;
        var l = KisayolListesi();
        if (l.Any(k => string.Equals(k.Hedef, yol, StringComparison.OrdinalIgnoreCase)))
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Zaten çubukta", ad, Simge: "", SaniyeOverride: 3));
            return;
        }
        l.Add((ad, yol));
        KisayolListesiKaydet(l);
        if (_genis) Genislet();
    }

    private void KisayolCalistir(string hedef, string ad)
    {
        try
        {
            if (hedef.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", hedef) { UseShellExecute = true });
            else
            {
                string exe = hedef, arg = "";
                int k = hedef.IndexOf(" --", StringComparison.Ordinal);
                if (k > 0) { exe = hedef[..k]; arg = hedef[(k + 1)..]; }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe.Trim('"'), arg) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, $"{ad} açılamadı", ex.Message, Simge: "", SaniyeOverride: 5));
        }
    }

    // ---------- Hazne işlemleri ----------

    private void HazneGostergeGuncelle()
    {
        int n = _hazne.Ogeler.Count;
        KompaktHazne.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        KompaktHazneSayi.Text = n.ToString();
        KompaktCanavar.DosyaTutuyor = n > 0;
        GenisCanavar.DosyaTutuyor = n > 0;
        KisayollariKur();   // çubuktaki ataç + adet düğmesi
        if (!_genis && _aktifDuyuru == null) Daralt(animasyonlu: true);
    }

    /// Haznedeki tek öğeyi ya da hepsini yükler. link=true: herkese açık klasöre yükler, linkleri panoya kopyalar.
    private async Task HazneYukle(HazneOgesi? tek, bool link)
    {
        _ozet.Say("yukleme");
        var hedefListe = tek != null ? new[] { tek } : _hazne.Ogeler.ToArray();
        if (hedefListe.Length == 0) return;
        string hedef = link ? _ayar.LinkHedef : _ayar.SurukleHedef;
        if (link && (string.IsNullOrWhiteSpace(_ayar.LinkOnek) || string.IsNullOrWhiteSpace(_ayar.LinkHedef)))
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "Link hedefi ayarlanmamış", "Ayarlar > Hazne > Link hedefi ve ön eki", Simge: "", SaniyeOverride: 6));
            return;
        }

        var linkler = new List<string>();
        int basarili = 0, hatali = 0;
        foreach (var o in hedefListe)
        {
            if (link && o.Klasor) { o.Durum = "klasör için önce Zip"; hatali++; continue; }  // dizin listesi kapalı, link çalışmaz
            o.Durum = link ? "linkleniyor…" : "yükleniyor…";
            var s = await YuklemeServisi.KopyalaAsync(o.Yol, hedef, _ayar.SuruklePort, _ayar.SurukleAnahtar);
            if (s.Basarili)
            {
                basarili++;
                if (link) { o.Link = _ayar.LinkOnek.TrimEnd('/') + "/" + Uri.EscapeDataString(o.Ad); linkler.Add(o.Link); o.Durum = "link kopyalandı"; }
                else o.Durum = "yüklendi";
            }
            else { hatali++; o.Durum = "hata: " + s.Mesaj; }
        }

        if (link && linkler.Count > 0)
        {
            try { Clipboard.SetText(string.Join(Environment.NewLine, linkler)); } catch { }
        }

        string ozet = hatali == 0
            ? (link ? (linkler.Count == 1 ? linkler[0] : $"{linkler.Count} link panoda") : $"{basarili} öğe → {HedefKisa()}")
            : $"{basarili} başarılı, {hatali} hatalı";
        if (hatali == 0) _efekt.Cal(SesEfektServisi.Efekt.Gonder);
        _kuyruk.Ekle(new Duyuru(hatali == 0 ? DuyuruTuru.Basari : DuyuruTuru.Uyari,
            link ? "Link hazır, panoya kopyalandı" : "Yükleme bitti", ozet,
            Simge: hatali == 0 ? "" : "", SaniyeOverride: 7));
    }

    /// Haznedekileri WhatsApp köprüsüyle kendi numarana ("Siz" sohbeti) gönderir: telefona aktarma
    private async Task HazneTelefonaAsync(HazneOgesi? tek = null)
    {
        var ogeler = tek != null ? new[] { tek } : _hazne.Ogeler.ToArray();
        if (ogeler.Length == 0) return;
        if (_wa is not { Hazir: true })
        {
            _kuyruk.Ekle(new Duyuru(DuyuruTuru.Uyari, "WhatsApp köprüsü bağlı değil", "Üzerine gelip QR'ı okutun", Simge: "", SaniyeOverride: 5));
            return;
        }
        int ok = 0, hata = 0;
        foreach (var o in ogeler)
        {
            if (o.Klasor) { o.Durum = "klasör için önce Zip"; hata++; continue; }
            o.Durum = "telefona gönderiliyor…";
            var s = await _wa.GonderDosyaAsync(null, null, o.Yol);
            if (s.Ok) { ok++; o.Durum = "telefona gönderildi"; } else { hata++; o.Durum = "hata: " + s.Mesaj; }
        }
        if (hata == 0) _efekt.Cal(SesEfektServisi.Efekt.Gonder);
        _kuyruk.Ekle(new Duyuru(hata == 0 ? DuyuruTuru.Basari : DuyuruTuru.Uyari, "Telefona gönderme",
            hata == 0 ? $"{ok} dosya WhatsApp'ta 'Siz' sohbetinde" : $"{ok} gönderildi, {hata} hatalı", Simge: "", SaniyeOverride: 6));
    }

    private void HazneZip()
    {
        var zip = _hazne.ZipYap();
        _kuyruk.Ekle(zip != null
            ? new Duyuru(DuyuruTuru.Basari, "Zip hazır", System.IO.Path.GetFileName(zip) + " masaüstünde ve haznede", Simge: "", SaniyeOverride: 6)
            : new Duyuru(DuyuruTuru.Uyari, "Zip oluşturulamadı", "", Simge: "", SaniyeOverride: 5));
    }

    private void HaznePano()
    {
        int n = _hazne.PanodanEkle();
        if (n == 0)
            _kuyruk.Ekle(_hazne.SonEklemeYinelendi
                ? new Duyuru(DuyuruTuru.Bilgi, "Zaten haznede", "Aynı içerik ikinci kez eklenmez", Simge: "", SaniyeOverride: 3)
                : new Duyuru(DuyuruTuru.Bilgi, "Panoda dosya, görsel ya da metin yok", "", Simge: "", SaniyeOverride: 4));
    }

    private string HedefKisa()
    {
        var h = _ayar.SurukleHedef;
        int i = h.IndexOf('@');
        return i >= 0 ? h[(i + 1)..] : h;
    }

    private void PomodoroUygula()
    {
        MedyaPomodoro.Guncelle();
        BosPomodoro.Guncelle();
        bool goster = false; // kronometre arayüzden kaldırıldı (kullanıcı isteği, 2026-10-03); altyapı duruyor
        bool degisti = goster != (KompaktPomodoro.Visibility == Visibility.Visible);
        KompaktPomodoro.Visibility = goster ? Visibility.Visible : Visibility.Collapsed;
        if (goster)
        {
            KompaktPomodoroSure.Text = _pomodoro.Metin;
            HalkaCiz(_pomodoro.Oran);
            PomodoroHalka.Stroke = new SolidColorBrush(_pomodoro.Calisiyor ? Color.FromRgb(0xFF, 0x9F, 0x0A) : Color.FromRgb(0x9A, 0x9A, 0xA3));
        }
        if (degisti && !_genis && _aktifDuyuru == null) Daralt(animasyonlu: true);
    }

    private void HalkaCiz(double oran) => PomodoroHalka.Data = ArkGeometri(oran, 6.75, 8);

    /// Saat yönünde, tepeden başlayan yay; 0 → null, 1 → tam daire (çok küçük boşluklu).
    private static PathGeometry? ArkGeometri(double oran, double r, double c)
    {
        if (oran <= 0.002) return null;
        double aci = Math.Min(oran, 0.9999) * 2 * Math.PI;
        var baslangic = new Point(c, c - r);
        var bitis = new Point(c + r * Math.Sin(aci), c - r * Math.Cos(aci));
        var sekil = new PathFigure { StartPoint = baslangic, IsClosed = false };
        sekil.Segments.Add(new ArcSegment(bitis, new Size(r, r), 0, aci > Math.PI, SweepDirection.Clockwise, true));
        return new PathGeometry(new[] { sekil });
    }

    /// Uzak sunucu sağlığı: 60 sn'de bir ölç, halkayı güncelle, düşüş/geri geliş duyurusu
    private async Task SunucuOlcAsync()
    {
        var d = await _sunucu.OlcAsync(_ayar.SunucuAdres, _ayar.SunucuSshKullanici, _ayar.SunucuAnahtar, _ayar.SunucuUrller);
        SunucuHalkaGuncelle();
        bool saglikli = d.Ayakta && !d.WebSorun;
        if (_sunucuOncekiSaglikli.HasValue && _sunucuOncekiSaglikli.Value != saglikli)
        {
            _kuyruk.Ekle(saglikli
                ? new Duyuru(DuyuruTuru.Basari, "Sunucu geri geldi", $"{_ayar.SunucuAdres} · {d.PingMs} ms", Simge: "", SaniyeOverride: 6)
                : new Duyuru(DuyuruTuru.Uyari, d.Ayakta ? "Sunucuda web sorunu" : "Sunucu yanıt vermiyor", d.Ayakta ? d.WebOzet : $"{_ayar.SunucuAdres} ping yok", Simge: "", SaniyeOverride: 10));
        }
        _sunucuOncekiSaglikli = saglikli;
        Gunluk($"sunucu: ayakta={d.Ayakta} ping={d.PingMs} web={d.WebOzet} yuk={d.Yuk} disk={d.DiskYuzde} ram={d.RamYuzde} hata={d.Hata}");
    }

    private void SunucuHalkaGuncelle()
    {
        var d = _sunucu.Son;
        SunucuKutu.Visibility = _ayar.SunucuAcik && _ayar.SunucuAdres.Trim().Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (d == null) { SunucuMetin.Text = "…"; SunucuHalka.Data = null; SunucuKutu.ToolTip = "ölçülüyor"; return; }

        bool saglikli = d.Ayakta && !d.WebSorun;
        var renk = !d.Ayakta ? Color.FromRgb(0xFF, 0x45, 0x3A) : d.WebSorun ? Color.FromRgb(0xD9, 0x77, 0x57) : Color.FromRgb(0x30, 0xD1, 0x58);
        SunucuHalka.Stroke = new SolidColorBrush(renk);
        SunucuHalka.Data = ArkGeometri(d.DiskYuzde >= 0 ? d.DiskYuzde / 100.0 : (d.Ayakta ? 1 : 0), 13.75, 15);
        SunucuMetin.Text = !d.Ayakta ? "!" : d.PingMs >= 0 ? d.PingMs.ToString() : "?";
        SunucuMetin.Foreground = new SolidColorBrush(saglikli ? Color.FromRgb(0xF2, 0xF2, 0xF4) : renk);
        SunucuKutu.ToolTip = string.Join("\n", new[]
        {
            $"{_ayar.SunucuAdres} · {(d.Ayakta ? d.PingMs + " ms" : "ping yok")}",
            d.WebOzet,
            d.Yuk >= 0 ? $"yük {d.Yuk:0.00} · disk %{d.DiskYuzde} · RAM %{d.RamYuzde}" : "",
            d.Uptime.Length > 0 ? "açık: " + d.Uptime : "",
            d.Hata.Length > 0 ? "ssh: " + d.Hata : "",
            $"ölçüm {_sunucu.SonZaman:HH:mm}",
        }.Where(s => s.Length > 0));
    }

    /// Boş paneldeki CPU ve RAM halkaları (30 px, yarıçap 13.75)
    private void SistemHalkalariGuncelle()
    {
        CpuHalka.Data = ArkGeometri(_sistem.CpuYuzde / 100.0, 13.75, 15);
        RamHalka.Data = ArkGeometri(_sistem.RamYuzde / 100.0, 13.75, 15);
        CpuMetin.Text = _sistem.CpuYuzde.ToString();
        RamMetin.Text = _sistem.RamYuzde.ToString();
        RamMetin.ToolTip = $"{_sistem.RamKullanilanGb:0.0} / {_sistem.RamToplamGb:0.0} GB";
        SunucuHalkaGuncelle();
        AgSatiriGuncelle();
    }

    /// Boş paneldeki ağ satırı: hızlar ve Tailscale çipi
    private void AgSatiriGuncelle()
    {
        AgSatiri.Visibility = _ayar.AgAcik ? Visibility.Visible : Visibility.Collapsed;
        if (!_ayar.AgAcik) return;
        AgIndirme.Text = AgServisi.Bicimle(_ag.IndirmeBs);
        AgYukleme.Text = AgServisi.Bicimle(_ag.YuklemeBs);
        VpnCip.Visibility = _ag.VpnKurulu ? Visibility.Visible : Visibility.Collapsed;
        if (!_ag.VpnKurulu) return;
        // yeşil bağlı · turuncu bağlanıyor/sorunlu · gri kapalı
        var renk = _ag.VpnBagli ? Color.FromRgb(0x30, 0xD1, 0x58) : _ag.VpnBaglaniyor ? Color.FromRgb(0xD9, 0x77, 0x57) : Color.FromRgb(0x3A, 0x3A, 0x42);
        VpnNokta.Fill = new SolidColorBrush(renk);
        VpnMetin.Foreground = new SolidColorBrush(_ag.VpnBagli ? Color.FromRgb(0xF2, 0xF2, 0xF4) : Color.FromRgb(0x8E, 0x8E, 0x96));
        VpnCip.ToolTip = _ag.VpnBagli ? $"Tailscale bağlı · {_ag.VpnIp}{(_ag.VpnAd.Length > 0 ? " · " + _ag.VpnAd : "")}"
                       : _ag.VpnMesaj.Length > 0 ? "Tailscale: " + _ag.VpnMesaj : "Tailscale bağlı değil";
    }

    /// Tailscale bağlanınca / kopunca bir kez duyur (ilk ölçümde sessiz)
    private void VpnDegisimDuyur()
    {
        if (!_ag.VpnKurulu) return;
        if (_vpnOnceki.HasValue && _vpnOnceki.Value != _ag.VpnBagli)
            _kuyruk.Ekle(_ag.VpnBagli
                ? new Duyuru(DuyuruTuru.Basari, "Tailscale bağlandı", _ag.VpnIp, Simge: "", SaniyeOverride: 4, Anahtar: "vpn")
                : new Duyuru(DuyuruTuru.Uyari, "Tailscale koptu", "VPN bağlantısı kapandı", Simge: "", SaniyeOverride: 4, Anahtar: "vpn"));
        _vpnOnceki = _ag.VpnBagli;
    }

    // ---------- Duyurular ----------

    private void DuyuruGeldi()
    {
        if (_aktifDuyuru != null)
        {
            // Aynı anahtarlı yeni duyuru (ses tuşu art arda): yerinde güncelle, süreyi tazele
            var ayni = _kuyruk.AnahtarlaAl(_aktifDuyuru.Anahtar);
            if (ayni != null)
            {
                _aktifDuyuru = ayni;
                DuyuruDoldur(ayni);
                DuyuruBoyutla(ikiSatir: !string.IsNullOrEmpty(ayni.Metin));
                DuyuruZamanla(ayni);
            }
            return;
        }
        if (_genis) return; // fare üstündeyken bekletilir, daralınca gösterilir
        SonrakiDuyuru();
    }

    private void SonrakiDuyuru()
    {
        var d = _kuyruk.Al();
        if (d == null) { _aktifDuyuru = null; Daralt(); return; }

        _aktifDuyuru = d;
        DuyuruDoldur(d);
        DuyuruBoyutla(ikiSatir: !string.IsNullOrEmpty(d.Metin));
        DuyuruZamanla(d);
    }

    /// Duyuru panelini içeriğine göre ölçer ve kapsülü o boyuta taşır.
    private void DuyuruBoyutla(bool ikiSatir)
    {
        // Gizli öğe 0 ölçülür; önce görünür yap
        Duyuru.Visibility = Visibility.Visible;
        Duyuru.Width = double.NaN;
        Duyuru.Height = double.NaN;
        Duyuru.UpdateLayout();
        Duyuru.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double y = ikiSatir ? KompaktIkiSatirYukseklik : KompaktYukseklik;
        // DesiredSize kenar boşluklarını (8 sol, 14 sağ) zaten içerir; genişliği sabitleme ki "*" sütun şişip
        // sağdaki yüzde yazısını kapsül dışına itmesin (ses duyurusunda "%100" kırpılıyordu)
        double g = Math.Clamp(Math.Ceiling(Duyuru.DesiredSize.Width) + 6, 180, 440);
        Duyuru.Width = double.NaN;
        Duyuru.Height = y - 2;

        Gecis(Duyuru, g, y, new BackEase { Amplitude = 0.22, EasingMode = EasingMode.EaseOut }, 320);
        Ada.CornerRadius = new CornerRadius(ikiSatir ? 22 : 18);
    }

    private void DuyuruZamanla(Duyuru d)
    {
        _duyuruSuresi.Stop();
        _duyuruSuresi.Interval = TimeSpan.FromSeconds(Math.Max(1, d.Saniye(_ayar.DuyuruSaniye)));
        _duyuruSuresi.Start();
    }

    private void DuyuruDoldur(Duyuru d)
    {
        DuyuruBaslik.Text = d.Baslik;
        DuyuruMetin.Text = d.Metin;
        DuyuruMetin.Visibility = string.IsNullOrEmpty(d.Metin) ? Visibility.Collapsed : Visibility.Visible;

        bool logoVar = d.Logo != null;
        DuyuruLogoKutu.Visibility = logoVar ? Visibility.Visible : Visibility.Collapsed;
        DuyuruLogo.Source = d.Logo;

        bool glif = !logoVar && d.Simge.Length == 1 && d.Simge[0] >= '' && d.Simge[0] <= '';
        DuyuruGlif.Text = glif ? d.Simge : "";
        DuyuruGlif.Visibility = glif ? Visibility.Visible : Visibility.Collapsed;
        DuyuruEmoji.Text = !glif && !logoVar ? d.Simge : "";
        DuyuruEmoji.Visibility = !glif && !logoVar && d.Simge.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        DuyuruGlif.Foreground = d.Tur switch
        {
            DuyuruTuru.Uyari => new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)),
            DuyuruTuru.Basari => new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)),
            _ => (Brush)FindResource("MetinBirincil"),
        };

        // Önce önceki animasyonları kaldır
        DuyuruOranKay.BeginAnimation(TranslateTransform.XProperty, null);
        DuyuruSimgeKay.BeginAnimation(TranslateTransform.YProperty, null);
        DuyuruSimgeOlcek.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        DuyuruSimgeOlcek.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        DuyuruOranKay.X = 0; DuyuruSimgeKay.Y = 0; DuyuruSimgeOlcek.ScaleX = DuyuruSimgeOlcek.ScaleY = 1;

        if (d.Surecte)
        {
            // Belirsiz ilerleme: kısa parlak parça rayda gidip gelir, simge yukarı doğru nefes alır
            DuyuruOranKutu.Visibility = Visibility.Visible;
            DuyuruOranDolgu.Width = 34;
            DuyuruOranMetin.Text = "";
            DuyuruOranKay.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(-34, 110, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            DuyuruSimgeKay.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(1.5, -2.5, TimeSpan.FromSeconds(0.55)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }
        else if (d.Oran.HasValue)
        {
            DuyuruOranKutu.Visibility = Visibility.Visible;
            DuyuruOranDolgu.Width = 110 * Math.Clamp(d.Oran.Value, 0, 1);
            DuyuruOranMetin.Text = $"%{(int)Math.Round(d.Oran.Value * 100)}";
        }
        else DuyuruOranKutu.Visibility = Visibility.Collapsed;

        if (d.Tur is DuyuruTuru.Basari or DuyuruTuru.Uyari)
        {
            // Sonuç simgesi: küçükten büyüyüp yerine oturur
            var pop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(420) };
            pop.KeyFrames.Add(new LinearDoubleKeyFrame(0.4, TimeSpan.Zero));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.25, TimeSpan.FromMilliseconds(220), new CubicEase { EasingMode = EasingMode.EaseOut }));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, TimeSpan.FromMilliseconds(420), new CubicEase { EasingMode = EasingMode.EaseInOut }));
            DuyuruSimgeOlcek.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            DuyuruSimgeOlcek.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }
    }

    // ---------- Genişle / daral ----------

    private void Ada_MouseEnter(object sender, MouseEventArgs e)
    {
        _fareBekle = false; _fareBekleZaman.Stop();
        _daraltGecikme.Stop();
        if (_dropSonrasi || _surukleme) return; // bırakma sonrası duyuru gösteriliyor, genişleme yok
        if (_mini) return;                      // oyun katmanı: oyun imleci üstünden geçerse açılma
        var onPlan = GetForegroundWindow();
        if (onPlan != IntPtr.Zero && onPlan != _hwnd && !_genis) _onPlanOnceki = onPlan;   // "seçiliyi özetle" bu pencereye Ctrl+C gönderir
        if (_aktifDuyuru != null) { _duyuruSuresi.Stop(); _aktifDuyuru = null; }
        if (!_genis) { _genis = true; Genislet(); }
    }

    // Düğmeyle açılan panel kapsülü küçültünce fare dışarıda kalabilir: hemen kapatma, kullanıcıya panele girmesi için süre tanı
    private bool _fareBekle;
    private readonly DispatcherTimer _fareBekleZaman = new() { Interval = TimeSpan.FromMilliseconds(2500) };
    private void FareBekleBaslat() { _fareBekle = true; _ = Dispatcher.BeginInvoke(async () => { await Task.Delay(1200); _fareBekle = false; }); }   // 1,2 sn içinde küçülme olmazsa normal davranış

    private void Ada_MouseLeave(object sender, MouseEventArgs e)
    {
        _dropSonrasi = false;
        if (_fareBekle) { _fareBekle = false; _fareBekleZaman.Stop(); _fareBekleZaman.Start(); return; }   // panel küçüldü, fare dışarıda kaldı: bekle
        _daraltGecikme.Stop();
        _daraltGecikme.Start();
    }

    private void Genislet()
    {
        _efekt.Cal(SesEfektServisi.Efekt.Ac);
        _genis = true;
        Gunluk($"genislet: okunmamis={_okunmamis.Count} medya={_durum.VarMi}");

        if (_okunmamis.Count > 0)
        {
            // Okunmamış mesajlar: tam içerik; yükseklik içeriğe göre ölçülür
            _bildirimGoruldu = true;
            BildirimListe.ItemsSource = _okunmamis.ToList();
            // WhatsApp mesajı ve numara bilinmiyorsa bir kerelik numara alanı
            var ilk = _okunmamis[0];
            bool wa = ilk.Uygulama.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) || ilk.Aumid.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
            NumaraSatiri.Visibility = wa && RehberNumara(ilk.Baslik) == null ? Visibility.Visible : Visibility.Collapsed;
            NumaraEtiket.Text = $"{ilk.Baslik} için numara:";
            CevapSatiri.Visibility = wa ? Visibility.Visible : Visibility.Collapsed;
            GenisBildirim.Visibility = Visibility.Visible;
            GenisBildirim.Height = double.NaN;
            GenisBildirim.UpdateLayout();
            GenisBildirim.Measure(new Size(GenisMedyaGenislik, double.PositiveInfinity));
            double yb = Math.Clamp(GenisBildirim.DesiredSize.Height + 2, 80, 420);
            GenisBildirim.Height = yb - 2;
            Gecis(GenisBildirim, GenisMedyaGenislik, yb, new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }, 320);
            Ada.CornerRadius = new CornerRadius(26);
            return;
        }

        // WhatsApp bağlı değil ve QR bekliyorsa: önce bağlantı paneli
        if (_wa is { QrVar: true, Hazir: false } && QrResim.Source != null)
        {
            Gecis(GenisQr, 380, 300, new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }, 320);
            Ada.CornerRadius = new CornerRadius(26);
            return;
        }

        // Ses karışımı paneli: düğmeyle istendiyse
        if (_karisimGoster)
        {
            KarisimYenile();
            GenisKarisim.Visibility = Visibility.Visible;
            GenisKarisim.Height = double.NaN;
            GenisKarisim.UpdateLayout();
            GenisKarisim.Measure(new Size(380, double.PositiveInfinity));
            double yk = Math.Clamp(GenisKarisim.DesiredSize.Height + 2, 100, 460);
            GenisKarisim.Height = yk - 2;
            Gecis(GenisKarisim, 380, yk, new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }, 320);
            Ada.CornerRadius = new CornerRadius(26);
            _karisimZaman.Start();
            return;
        }

        // Claude'a sor paneli: düğmeyle istendiyse, yükseklik içeriğe göre
        if (_soruGoster && _ayar.ClaudeAcik)
        {
            SoruKaynak.Text = _claude.Kaynak;
            GenisSoru.Visibility = Visibility.Visible;
            GenisSoru.Height = double.NaN;
            GenisSoru.UpdateLayout();
            GenisSoru.Measure(new Size(380, double.PositiveInfinity));
            double ys = Math.Clamp(GenisSoru.DesiredSize.Height + 2, 100, 440);
            GenisSoru.Height = ys - 2;
            Gecis(GenisSoru, 380, ys, new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }, 320);
            Ada.CornerRadius = new CornerRadius(26);
            return;
        }

        // Hazne: dosya yeni bırakıldıysa (1 dk) ya da ataç düğmesiyle istendiyse; yoksa saat/kısayol paneli
        bool hazneGoster = _hazne.Ogeler.Count > 0 && (_hazneGoster || DateTime.Now - _hazneSonEkleme < TimeSpan.FromSeconds(60));
        if (hazneGoster)
        {
            // Haznede dosya var: hazne paneli, yükseklik içeriğe göre
            double gh = HaznePanel.Width;   // ızgarada sütun sayısına göre genişler (en fazla 5 kutu)
            GenisHazne.Visibility = Visibility.Visible;
            GenisHazne.Width = gh;
            GenisHazne.Height = double.NaN;
            GenisHazne.UpdateLayout();
            GenisHazne.Measure(new Size(gh, double.PositiveInfinity));
            double yh = Math.Clamp(GenisHazne.DesiredSize.Height + 2, 120, 560);
            GenisHazne.Height = yh - 2;
            Gecis(GenisHazne, gh, yh, new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }, 320);
            Ada.CornerRadius = new CornerRadius(26);
            return;
        }

        bool medya = _durum.VarMi && !_testBos;   // _testBos: test kancası "bos 1" medya oynarken de boş paneli gösterir
        var hedef = medya ? GenisMedya : GenisBos;
        double g = medya ? GenisMedyaGenislik : GenisBosGenislik;
        double y = medya ? GenisMedyaYukseklik + (SozMetin.Visibility == Visibility.Visible ? SozEkYukseklik : 0) : GenisBosYukseklik;
        if (medya) GenisMedya.Height = y;

        Gecis(hedef, g, y, new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }, 320);
        Ada.CornerRadius = new CornerRadius(26);
        if (!medya) SistemHalkalariGuncelle();
        Tik();
        BosSes.Guncelle(_ses.Seviye, _ses.Sessiz);
    }

    private void Daralt(bool animasyonlu = true)
    {
        _efekt.Cal(SesEfektServisi.Efekt.Kapan);
        _genis = false;
        if (_odakSerbest) CevapKapat();   // odak izni geri alınsın
        if (_soruGoster && !_soruBekliyor && DateTime.Now - _soruSonKullanim > TimeSpan.FromSeconds(90)) _soruGoster = false;   // uzun süre kullanılmadıysa paneli unut
        if (_karisimGoster) { _karisimGoster = false; _karisimZaman.Stop(); }
        _hazneGoster = false;
        Gunluk($"daralt: goruldu={_bildirimGoruldu} okunmamis={_okunmamis.Count} aktifDuyuru={_aktifDuyuru != null} kuyruk={_kuyruk.Sayi}");
        if (_bildirimGoruldu) { _bildirimGoruldu = false; _okunmamis.Clear(); }   // fare çekildi: okundu
        if (_aktifDuyuru == null && _kuyruk.Sayi > 0) { SonrakiDuyuru(); return; }

        if (_mini || _testMini || _sesSeridi)
        {
            // Oyun katmanı: ince şerit (saat, CPU, GPU, RAM, okunmamış rozeti)
            MiniGuncelle();
            MiniIcerik.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Gecis(MiniKatman, Math.Max(120, MiniIcerik.DesiredSize.Width + 4), MiniYukseklik,
                new CubicEase { EasingMode = EasingMode.EaseInOut }, animasyonlu ? 220 : 1);
            Ada.CornerRadius = new CornerRadius(13);
            return;
        }

        KompaktIcerikGuncelle();
        KompaktBaslikGuncelle();   // söz varsa kompakt başlıkta o anki satır
        double g = KompaktGenislik();
        var ease = animasyonlu ? new CubicEase { EasingMode = EasingMode.EaseInOut } : new CubicEase { EasingMode = EasingMode.EaseOut };
        Gecis(Kompakt, g, KompaktYukseklik, ease, animasyonlu ? 260 : 1);
        Ada.CornerRadius = new CornerRadius(18);
    }

    private double KompaktGenislik()
    {
        var sonsuz = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double ek = 0;
        if (KompaktPomodoro.Visibility == Visibility.Visible) { KompaktPomodoro.Measure(sonsuz); ek += KompaktPomodoro.DesiredSize.Width; }
        if (KompaktHazne.Visibility == Visibility.Visible) { KompaktHazne.Measure(sonsuz); ek += KompaktHazne.DesiredSize.Width; }
        if (KompaktGizlilik.Visibility == Visibility.Visible) { KompaktGizlilik.Measure(sonsuz); ek += KompaktGizlilik.DesiredSize.Width; }

        if (_okunmamis.Count > 0)
        {
            KompaktBildirim.Measure(sonsuz);
            return Math.Clamp(KompaktBildirim.DesiredSize.Width + 2, 120, 420) + ek;
        }
        if (_durum.VarMi) return KompaktMedyaGenislik + ek;

        KompaktBos.Measure(sonsuz);
        return Math.Max(56, KompaktBos.DesiredSize.Width + 2) + ek;
    }

    /// Hedef paneli görünür yapar, diğerlerini soldurur, kapsülü yeni boyuta taşır.
    private void Gecis(UIElement hedef, double genislik, double yukseklik, IEasingFunction ease, int ms)
    {
        foreach (var p in new UIElement[] { Kompakt, Duyuru, GenisMedya, GenisBos, GenisBildirim, GenisHazne, GenisQr, MiniKatman, GenisSoru, GenisKarisim })
        {
            if (ReferenceEquals(p, hedef)) continue;
            p.IsHitTestVisible = false;
            Solma(p, 0, 120, 0, () => { if (!ReferenceEquals(p, hedef) && p.Opacity < 0.01) p.Visibility = Visibility.Collapsed; });
        }
        hedef.Visibility = Visibility.Visible;
        hedef.IsHitTestVisible = true;
        Solma(hedef, 1, ms > 1 ? 220 : 1, ms > 1 ? 90 : 0);

        var sure = TimeSpan.FromMilliseconds(ms);
        Ada.BeginAnimation(WidthProperty, new DoubleAnimation(genislik, sure) { EasingFunction = ease });
        Ada.BeginAnimation(HeightProperty, new DoubleAnimation(yukseklik, sure) { EasingFunction = ease });
    }

    private static void Solma(UIElement hedef, double opaklik, int ms, int gecikmeMs, Action? bitince = null)
    {
        var a = new DoubleAnimation(opaklik, TimeSpan.FromMilliseconds(ms))
        {
            BeginTime = TimeSpan.FromMilliseconds(gecikmeMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        if (bitince != null) a.Completed += (_, _) => bitince();
        hedef.BeginAnimation(OpacityProperty, a);
    }

    /// İçeriği kapsülün yuvarlak köşelerine göre kırpar (Border çocukları köşeyi kırpmaz).
    private void Ada_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double r = Math.Min(Ada.CornerRadius.TopLeft, e.NewSize.Height / 2);
        Icerik.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, e.NewSize.Width - 2), Math.Max(0, e.NewSize.Height - 2)), r, r);
    }

    // ---------- Denetimler ----------

    private async void OynatDuraklat_Click(object sender, RoutedEventArgs e) => await _medya.OynatDuraklatAsync();
    private async void Onceki_Click(object sender, RoutedEventArgs e) => await _medya.OncekiAsync();
    private async void Sonraki_Click(object sender, RoutedEventArgs e) => await _medya.SonrakiAsync();

    private void Ilerleme_MouseDown(object sender, MouseButtonEventArgs e)
    {
        double oran = IlerlemeTik.ActualWidth > 0 ? Math.Clamp(e.GetPosition(IlerlemeTik).X / IlerlemeTik.ActualWidth, 0, 1) : -1;
        Gunluk($"ilerleme tiklandi: oran={oran:0.00}");
        if (oran >= 0) _ = KonumaAtla(oran);
    }

    private async Task KonumaAtla(double oran)
    {
        bool olur = _medya.KonumDegistirilebilir;
        var (_, sure) = _medya.ZamanCizelgesi();
        Gunluk($"seek: olur={olur} sure={sure} oran={oran:0.00}");
        if (!olur || sure.TotalSeconds <= 0) return;
        IlerlemeDolgu.Width = IlerlemeRay.ActualWidth * oran;
        bool sonuc = await _medya.KonumAyarlaAsync(TimeSpan.FromSeconds(sure.TotalSeconds * oran));
        Gunluk($"seek sonucu: {sonuc}");
    }

    // ---------- Hata ayıklama kaydı ve test kancası (yalnız DINAMIKADA_GUNLUK=1 iken) ----------

    private static readonly bool GunlukAcik = Environment.GetEnvironmentVariable("DINAMIKADA_GUNLUK") == "1";
    private static readonly string GunlukYolu = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dinamikada-gunluk.txt");
    private static readonly string KomutYolu = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dinamikada-komut.txt");
    private DispatcherTimer? _komutZamanlayici;

    private static void Gunluk(string satir)
    {
        if (!GunlukAcik) return;
        try { System.IO.File.AppendAllText(GunlukYolu, $"{DateTime.Now:HH:mm:ss.fff} {satir}\n"); } catch { }
    }

    /// Komut dosyası: her satır bir komut. genislet | daralt | seek 0.25 | tikla X Y | pomodoro
    private void TestKancasiBaslat()
    {
        if (!GunlukAcik) return;
        _komutZamanlayici = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _komutZamanlayici.Tick += (_, _) => KomutIsle();
        _komutZamanlayici.Start();
        Gunluk("test kancasi acik");
    }

    private void KomutIsle()
    {
        if (!System.IO.File.Exists(KomutYolu)) return;
        string[] satirlar;
        try { satirlar = System.IO.File.ReadAllLines(KomutYolu); System.IO.File.Delete(KomutYolu); }
        catch { return; }

        foreach (var s in satirlar)
        {
            var p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0) continue;
            try
            {
                switch (p[0])
                {
                    case "genislet": _daraltGecikme.Stop(); _genis = true; Genislet(); break;
                    case "daralt": Daralt(); break;
                    case "ayarlar": ((App)Application.Current).AyarlariAc(); break;
                    case "bos": _testBos = p.Length > 1 && p[1] == "1"; break;
                    case "mini": _testMini = p.Length > 1 && p[1] == "1"; if (!_genis) Daralt(); break;
                    case "sor": { string soru = string.Join(' ', p.Skip(1)); SoruPaneliAc(odakla: false); if (soru.Length > 0) _ = SoruGonderAsync(soru); break; }
                    case "sor-kapat": SoruKapat_Click(this, new RoutedEventArgs()); break;
                    case "sor-kaydet": SoruKaydet_Click(this, new RoutedEventArgs()); break;
                    case "efekt": { var tur = Enum.TryParse<SesEfektServisi.Efekt>(p.Length > 1 ? p[1] : "Ac", true, out var ef) ? ef : SesEfektServisi.Efekt.Ac; _efekt.Acik = true; _efekt.Cal(tur); Gunluk($"efekt {tur}: hata='{_efekt.SonHata}'"); break; }
                    case "sor-yaz": { SoruPaneliAc(odakla: false); SoruKutu.Text = p.Length > 1 ? string.Join(' ', p.Skip(1)) : ""; SoruGonder_Click(this, new RoutedEventArgs()); break; }
                    case "karisim": KarisimAc_Click(this, new RoutedEventArgs()); break;
                    case "karisim-menu": { KarisimYenile(); var ilk = _karisimListe.FirstOrDefault(); if (ilk != null) KarisimAygit_Click(new Button { Tag = ilk }, new RoutedEventArgs()); break; }
                    case "menu-kapat": if (_sonMenu != null) _sonMenu.IsOpen = false; break;
                    case "karisim-liste": { KarisimYenile(); foreach (var o in _karisimListe) Gunluk($"karisim: {o.Surec} ({o.Ad}) pid={string.Join(",", o.Pidler)} aygit={o.AygitKisa} kalici={o.Kalici} ses={o.Yuzde} sessiz={o.Sessiz}"); foreach (var a in _karisim.Aygitlar()) Gunluk($"karisim aygit: {a.Kisa} | {a.Ad} | {a.Id}"); break; }
                    case "karisim-yonlendir": { var q = string.Join(' ', p.Skip(1)).Split('|'); KarisimYenile(); var o = _karisimListe.FirstOrDefault(x => x.Surec.Equals(q[0].Trim(), StringComparison.OrdinalIgnoreCase)); if (o == null) { Gunluk("karisim: oturum yok " + q[0]); break; } string? id = q.Length > 1 && !q[1].Trim().Equals("varsayilan", StringComparison.OrdinalIgnoreCase) ? _karisim.Aygitlar().FirstOrDefault(a => a.Ad.Contains(q[1].Trim(), StringComparison.OrdinalIgnoreCase))?.Id : null; bool ok = _karisim.Yonlendir(o, id); Gunluk($"karisim-yonlendir: {o.Surec} -> {id ?? "varsayilan"} ok={ok} hata={_karisim.SonHata} okunan={AudioPolicyConfig.Oku(o.Pidler[0]) ?? "yok"}"); break; }
                    case "spotify-ham": { string yol = string.Join(' ', p.Skip(1)); _ = _spotify.HamAsync(yol).ContinueWith(t => Gunluk($"spotify-ham {yol} -> {t.Result}")); break; }
                    case "spotify-ara": { var parca = string.Join(' ', p.Skip(1)).Split('|'); _ = _spotify.AraAsync(parca[0].Trim(), parca.Length > 1 ? parca[1].Trim() : "").ContinueWith(t => Gunluk($"spotify-ara: parca={t.Result.Parca?.Ad ?? "yok"} id={t.Result.Parca?.Id} begenildi={t.Result.Begenildi} hata={_spotify.SonHata}")); break; }
                    case "spotify-begen": { var parca = string.Join(' ', p.Skip(1)).Split('|'); _ = Task.Run(async () => { var (pp, bb) = await _spotify.AraAsync(parca[0].Trim(), parca.Length > 1 ? parca[1].Trim() : ""); if (pp == null) { Gunluk("spotify-begen: parca yok " + _spotify.SonHata); return; } bool ok1 = await _spotify.BegenAyarlaAsync(pp.Id, !bb); var (_, b2) = await _spotify.AraAsync(parca[0].Trim(), parca.Length > 1 ? parca[1].Trim() : ""); bool ok2 = await _spotify.BegenAyarlaAsync(pp.Id, bb); Gunluk($"spotify-begen: {pp.Ad} once={bb} yaz={ok1} okundu={b2} gerial={ok2} hata={_spotify.SonHata}"); }); break; }
                    case "ozet": OzetGoster(otomatik: false); break;
                    case "hafta": OzetGoster(otomatik: false, haftalik: true); break;
                    case "tepki": KompaktCanavar.Tepki(); GenisCanavar.Tepki(); break;
                    case "uykulu": { bool u = p.Length > 1 && p[1] == "1"; _yorgun = u; KompaktCanavar.Uykulu = u; GenisCanavar.Uykulu = u; break; }
                    case "sapka": { bool sap = p.Length > 1 && p[1] == "1"; var r = sap ? Color.FromRgb(0xE0, 0x3C, 0x31) : (Color?)null; KompaktCanavar.SapkaAyarla(r, "test"); GenisCanavar.SapkaAyarla(r, "test"); break; }
                    case "soz": Gunluk($"soz durumu: var={_soz.Var} zamanli={_soz.Zamanli} kaynak={_soz.Kaynak} satir='{SozMetin.Text}' gorunur={SozMetin.Visibility} yukseklik={GenisMedya.Height}"); break;
                    case "spotify": _ = SpotifyDurumAsync().ContinueWith(_ => Dispatcher.BeginInvoke(() => Gunluk($"spotify: hazir={_spotify.Hazir} parca={_spotifyParca?.Ad} begenildi={_spotifyBegenildi} hata={_spotify.SonHata}"))); break;
                    case "seek": _ = KonumaAtla(double.Parse(p[1], CultureInfo.InvariantCulture)); break;
                    case "tikla": TestTikla(double.Parse(p[1], CultureInfo.InvariantCulture), double.Parse(p[2], CultureInfo.InvariantCulture)); break;
                    case "pomodoro": _pomodoro.BaslatDuraklat(); break;
                    case "ses": _ses.SeviyeAyarla(float.Parse(p[1], CultureInfo.InvariantCulture)); break;
                    case "yukle": _ = KopyalaVeBildir(string.Join(' ', p.Skip(1))); break;
                    case "hazne": _hazne.Ekle(string.Join(' ', p.Skip(1))); break;
                    case "hazne-zip": HazneZip(); break;
                    case "hazne-pano": HaznePano(); break;
                    case "hazne-link": _ = HazneYukle(null, link: true); break;
                    case "hazne-yukle": _ = HazneYukle(null, link: false); break;
                    case "hazne-goster": _hazneGoster = true; _genis = true; Genislet(); break;
                    case "hazne-gorunum": { HaznePanel.GorunumAyarla(p.Length > 1 ? p[1] : "liste"); _ayar.HazneGorunum = HaznePanel.Gorunum; if (_genis && GenisHazne.Visibility == Visibility.Visible) Genislet(); break; }
                    case "kisayol-ekle": KisayolEkleDialog(); break;
                    case "toplanti": GizlilikUygula(p.Length > 1 && p[1] == "1", false); break;
                    case "rehber": RehberIceAktar(string.Join(' ', p.Skip(1))); break;
                    case "rehber-bul": { string ad = string.Join(' ', p.Skip(1)); var n = RehberNumara(ad); Gunluk($"rehber-bul '{ad}' -> {n ?? "yok"}"); break; }
                    case "rehber-dok": Gunluk("rehber kayitlari: " + _rehber.Dok()); break;
                    case "canavar": CanavarUygula(Enum.Parse<CanavarModu>(p[1], true), p.Length > 2 ? p[2] : ""); break;
                }
                Gunluk("komut: " + s);
            }
            catch (Exception ex) { Gunluk($"komut hata: {s} {ex.Message}"); }
        }
    }

    /// Kapsül koordinatında gerçek görsel isabet testi yapar; düğmeyse Invoke eder, değilse MouseDown yükseltir.
    private void TestTikla(double x, double y)
    {
        var sonuc = VisualTreeHelper.HitTest(Ada, new Point(x, y));
        var vurulan = sonuc?.VisualHit;
        string ad = "";
        for (var k = vurulan; k != null; k = VisualTreeHelper.GetParent(k))
            if (k is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name)) { ad = fe.Name; break; }
        Gunluk($"tikla {x},{y} -> {vurulan?.GetType().Name ?? "yok"} ({ad})");

        for (var b = vurulan; b != null; b = VisualTreeHelper.GetParent(b))
        {
            if (b is System.Windows.Controls.Button dugme)
            {
                var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(dugme);
                (peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke) as System.Windows.Automation.Provider.IInvokeProvider)?.Invoke();
                return;
            }
        }
        if (vurulan is UIElement ue)
            ue.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
    }

    public void Kapat()
    {
        _ozet.Kaydet();
        _tik.Stop(); _saniye.Stop();
        if (_klavyeKancasi != IntPtr.Zero) { UnhookWindowsHookEx(_klavyeKancasi); _klavyeKancasi = IntPtr.Zero; }
        try { _wa?.Dispose(); } catch { }
        try { _ses.Dispose(); } catch { }
        Close();
    }

    // ---------- Ctrl+V: fare kapsülün üstündeyken panoyu hazneye yapıştır ----------
    // Pencere odak almadığı için tuş olayı gelmez; düşük seviyeli klavye kancası dinler,
    // yalnız fare kapsüldeyken devreye girer ve tuşu yutar (altındaki uygulamaya gitmez).

    private delegate IntPtr KlavyeKancaProc(int nCode, IntPtr wParam, IntPtr lParam);
    private KlavyeKancaProc? _klavyeKancaDelegesi;   // GC toplamasın diye alan
    private IntPtr _klavyeKancasi;

    private const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104, VK_V = 0x56, VK_CONTROL = 0x11;

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, KlavyeKancaProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private void KlavyeKancasiBaslat()
    {
        _klavyeKancaDelegesi = KlavyeKancasi;
        _klavyeKancasi = SetWindowsHookEx(WH_KEYBOARD_LL, _klavyeKancaDelegesi, GetModuleHandle(null), 0);
    }

    private IntPtr KlavyeKancasi(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            int vk = Marshal.ReadInt32(lParam);   // KBDLLHOOKSTRUCT.vkCode
            if (vk == 0x59 && (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && (GetAsyncKeyState(0x12) & 0x8000) != 0 && _mini && _okunmamis.Count > 0)
            {
                Dispatcher.BeginInvoke(() => _ = HizliYanitGonderAsync());   // oyunda Ctrl+Alt+Y: hazır cevap
                return (IntPtr)1;
            }
            if (vk == VK_V && (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && (Ada.IsMouseOver || _genis) && Ada.Visibility == Visibility.Visible)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    int n = _hazne.PanodanEkle();
                    if (n > 0)
                    {
                        _hazneSonEkleme = DateTime.Now;
                        _hazneGoster = true;
                        if (_genis) Genislet();   // hazne panelini göster / tazele
                        else _kuyruk.Ekle(new Duyuru(DuyuruTuru.Basari, n == 1 ? "Panodan hazneye eklendi" : $"{n} öğe hazneye eklendi", "", Simge: "", SaniyeOverride: 3));
                    }
                    else if (_hazne.SonEklemeYinelendi) _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Zaten haznede", "Aynı içerik ikinci kez eklenmez", Simge: "", SaniyeOverride: 3));
                    else _kuyruk.Ekle(new Duyuru(DuyuruTuru.Bilgi, "Panoda dosya, görsel ya da metin yok", "", Simge: "", SaniyeOverride: 3));
                });
                return (IntPtr)1;   // tuşu yut
            }
        }
        return CallNextHookEx(_klavyeKancasi, nCode, wParam, lParam);
    }

    // ---------- Odak almayan, Alt+Tab'da görünmeyen pencere ----------

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private void OdakAlmaz()
    {
        int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }
}
