using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DinamikAda.Servisler;

namespace DinamikAda.Kontroller;

/// Küçük maskot (Claude Code terminal yaratığı üslubunda): moda göre aksesuar takar ve farklı hareket eder.
public partial class Canavar : UserControl
{
    private readonly DispatcherTimer _kirpma = new();
    private readonly Random _rastgele = new();
    private CanavarModu _mod = CanavarModu.Normal;

    public Canavar()
    {
        InitializeComponent();
        _kirpma.Tick += (_, _) => Kirp();
        Loaded += (_, _) => { ModUygula(_mod); KirpmaPlanla(); };
        Unloaded += (_, _) => _kirpma.Stop();
    }

    public CanavarModu Mod
    {
        get => _mod;
        set { if (value != _mod) { _mod = value; if (IsLoaded) ModUygula(value); } }
    }

    /// Sol üstteki durum dairesi; null verilirse gizlenir.
    public void DurumAyarla(System.Windows.Media.Color? renk, string? ipucu)
    {
        if (renk == null) { DurumNokta.Visibility = Visibility.Collapsed; return; }
        DurumNokta.Fill = new System.Windows.Media.SolidColorBrush(renk.Value);
        DurumNokta.ToolTip = ipucu;
        DurumNokta.Visibility = Visibility.Visible;
    }

    private bool _mesgul;
    /// Toplantıda: kulaklık + mikrofon takar
    public bool MesgulMu
    {
        get => _mesgul;
        set { _mesgul = value; Mesgul.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
    }

    private bool _dosyaTutuyor;
    /// Haznede dosya varken elinde zarf görünür.
    public bool DosyaTutuyor
    {
        get => _dosyaTutuyor;
        set { _dosyaTutuyor = value; Zarf.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
    }

    // ---- Ruh hâli ----

    public static readonly DependencyProperty SapkaFircasiProperty =
        DependencyProperty.Register(nameof(SapkaFircasi), typeof(Brush), typeof(Canavar), new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xE0, 0x3C, 0x31))));
    public Brush SapkaFircasi { get => (Brush)GetValue(SapkaFircasiProperty); set => SetValue(SapkaFircasiProperty, value); }

    /// Özel gün şapkası: null gizler; renk verilirse takar (ipucu: günün adı)
    public void SapkaAyarla(Color? renk, string? ipucu = null)
    {
        if (renk == null) { Sapka.Visibility = Visibility.Collapsed; return; }
        SapkaFircasi = new SolidColorBrush(renk.Value);
        Sapka.Visibility = Visibility.Visible;
        if (ipucu != null) ToolTip = ipucu;
    }

    private bool _uykulu;
    private double GozAcik => _uykulu ? 0.55 : 1.0;
    /// Uykulu / yorgun: gözler yarı kapalı (akşam geç saat, uzun süre molasız çalışma)
    public bool Uykulu
    {
        get => _uykulu;
        set
        {
            if (_uykulu == value) return;
            _uykulu = value;
            GozKirp.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(GozAcik, TimeSpan.FromMilliseconds(400)) { FillBehavior = FillBehavior.Stop });
            GozKirp.ScaleY = GozAcik;
        }
    }

    /// Tıklama tepkisi: zıplar, iki yana sallanır, gözler iri açılır
    public void Tepki()
    {
        var zip = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(520), FillBehavior = FillBehavior.Stop };
        zip.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.Zero));
        zip.KeyFrames.Add(new EasingDoubleKeyFrame(-7, TimeSpan.FromMilliseconds(160), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        zip.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.FromMilliseconds(330), new BounceEase { Bounces = 2, Bounciness = 2.5, EasingMode = EasingMode.EaseOut }));
        zip.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.FromMilliseconds(520)));
        var mevcutY = Zipla.Y;
        zip.Completed += (_, _) => { if (IsLoaded) ModUygula(_mod); };
        Zipla.BeginAnimation(TranslateTransform.YProperty, zip, HandoffBehavior.SnapshotAndReplace);

        var salla = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(520), FillBehavior = FillBehavior.Stop };
        salla.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.Zero));
        salla.KeyFrames.Add(new LinearDoubleKeyFrame(-12, TimeSpan.FromMilliseconds(120)));
        salla.KeyFrames.Add(new LinearDoubleKeyFrame(12, TimeSpan.FromMilliseconds(260)));
        salla.KeyFrames.Add(new LinearDoubleKeyFrame(-6, TimeSpan.FromMilliseconds(380)));
        salla.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(520)));
        Egil.BeginAnimation(RotateTransform.AngleProperty, salla, HandoffBehavior.SnapshotAndReplace);

        var goz = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700), FillBehavior = FillBehavior.Stop };
        goz.KeyFrames.Add(new LinearDoubleKeyFrame(GozAcik, TimeSpan.Zero));
        goz.KeyFrames.Add(new LinearDoubleKeyFrame(1.35, TimeSpan.FromMilliseconds(120)));
        goz.KeyFrames.Add(new LinearDoubleKeyFrame(1.35, TimeSpan.FromMilliseconds(500)));
        goz.KeyFrames.Add(new LinearDoubleKeyFrame(GozAcik, TimeSpan.FromMilliseconds(700)));
        GozKirp.BeginAnimation(ScaleTransform.ScaleYProperty, goz, HandoffBehavior.SnapshotAndReplace);
    }

    private void ModUygula(CanavarModu mod)
    {
        // Tüm animasyonları kaldır, başlangıç durumuna dön
        Nefes.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        Nefes.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Egil.BeginAnimation(RotateTransform.AngleProperty, null);
        Zipla.BeginAnimation(TranslateTransform.XProperty, null);
        Zipla.BeginAnimation(TranslateTransform.YProperty, null);
        Bakis.BeginAnimation(TranslateTransform.XProperty, null);
        ZzzKay.BeginAnimation(TranslateTransform.YProperty, null);
        Zzz.BeginAnimation(OpacityProperty, null);
        foreach (var k in new[] { KodSatir1, KodSatir2, KodSatir3 }) { k.BeginAnimation(OpacityProperty, null); k.Opacity = 0; }
        Nefes.ScaleX = Nefes.ScaleY = 1; Egil.Angle = 0; Zipla.X = Zipla.Y = 0; Bakis.X = 0;

        bool uyku = mod == CanavarModu.Uyku;
        Gozler.Visibility = uyku ? Visibility.Collapsed : Visibility.Visible;
        GozKapali.Visibility = uyku ? Visibility.Visible : Visibility.Collapsed;
        Agiz.Visibility = uyku ? Visibility.Collapsed : Visibility.Visible;
        AgizUyku.Visibility = uyku ? Visibility.Visible : Visibility.Collapsed;
        Zzz.Visibility = uyku ? Visibility.Visible : Visibility.Collapsed;
        Gozluk.Visibility = mod == CanavarModu.Is ? Visibility.Visible : Visibility.Collapsed;
        Laptop.Visibility = mod == CanavarModu.Is ? Visibility.Visible : Visibility.Collapsed;
        Kulaklik.Visibility = mod == CanavarModu.Muzik ? Visibility.Visible : Visibility.Collapsed;
        Gamepad.Visibility = mod == CanavarModu.Oyun ? Visibility.Visible : Visibility.Collapsed;

        switch (mod)
        {
            case CanavarModu.Oyun:
                Salla(1.0, 1.04, 0.3);
                Anim(Zipla, TranslateTransform.YProperty, 0, -3.5, 0.28, new QuadraticEase { EasingMode = EasingMode.EaseOut });
                Anim(Egil, RotateTransform.AngleProperty, -4, 4, 0.56);
                break;

            case CanavarModu.Is:
                // Klavye başında: hızlı küçük sallanma, gözler ekrana (aşağı), kod satırları sırayla yazılır
                Anim(Egil, RotateTransform.AngleProperty, -3.5, 3.5, 0.16);
                Anim(Zipla, TranslateTransform.YProperty, 0, -1.2, 0.16);
                Bakis.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(-1.5, 1.5, TimeSpan.FromSeconds(0.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                KodYaz();
                break;

            case CanavarModu.Muzik:
                Anim(Zipla, TranslateTransform.YProperty, 0, -2, 0.45, new SineEase());
                Anim(Egil, RotateTransform.AngleProperty, -9, 9, 0.9, new SineEase { EasingMode = EasingMode.EaseInOut });
                break;

            case CanavarModu.Uyku:
                Salla(1.0, 1.05, 2.8);
                Anim(Egil, RotateTransform.AngleProperty, 0, 6, 2.8, new SineEase { EasingMode = EasingMode.EaseInOut });
                var zOp = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(2.6) };
                zOp.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.Zero));
                zOp.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromSeconds(0.6)));
                zOp.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromSeconds(1.8)));
                zOp.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromSeconds(2.6)));
                Zzz.BeginAnimation(OpacityProperty, zOp);
                ZzzKay.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(2, -7, TimeSpan.FromSeconds(2.6)) { RepeatBehavior = RepeatBehavior.Forever });
                break;

            default:
                Salla(1.0, 1.045, 1.5);
                Anim(Bakis, TranslateTransform.XProperty, -1.5, 1.5, 2.4, new SineEase { EasingMode = EasingMode.EaseInOut });
                break;
        }
    }

    /// Laptop ekranında üç kod satırı sırayla belirir, kısa durur, silinir; sonsuz döngü.
    private void KodYaz()
    {
        var satirlar = new[] { KodSatir1, KodSatir2, KodSatir3 };
        var toplam = TimeSpan.FromSeconds(2.4);
        for (int i = 0; i < satirlar.Length; i++)
        {
            var a = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = toplam };
            double t = 0.25 + i * 0.4;
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, TimeSpan.Zero));
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, TimeSpan.FromSeconds(t)));
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, TimeSpan.FromSeconds(1.9)));
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, TimeSpan.FromSeconds(2.0)));
            satirlar[i].BeginAnimation(OpacityProperty, a);
        }
    }

    private void Salla(double from, double to, double saniye)
    {
        Nefes.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(from, to, TimeSpan.FromSeconds(saniye)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        Nefes.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(from, 1 + (to - 1) * 0.5, TimeSpan.FromSeconds(saniye)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
    }

    private static void Anim(Transform hedef, DependencyProperty prop, double from, double to, double saniye, IEasingFunction? ease = null)
    {
        hedef.BeginAnimation(prop, new DoubleAnimation(from, to, TimeSpan.FromSeconds(saniye))
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease,
        });
    }

    private void KirpmaPlanla()
    {
        _kirpma.Interval = TimeSpan.FromMilliseconds(2200 + _rastgele.Next(2800));
        _kirpma.Start();
    }

    private void Kirp()
    {
        _kirpma.Stop();
        if (_mod != CanavarModu.Uyku)
        {
            var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(170), FillBehavior = FillBehavior.Stop };
            a.KeyFrames.Add(new LinearDoubleKeyFrame(GozAcik, TimeSpan.Zero));
            a.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, TimeSpan.FromMilliseconds(70)));
            a.KeyFrames.Add(new LinearDoubleKeyFrame(GozAcik, TimeSpan.FromMilliseconds(170)));
            GozKirp.ScaleY = GozAcik;
            GozKirp.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }
        KirpmaPlanla();
    }
}
