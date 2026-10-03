using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DinamikAda.Kontroller;

/// Sığmayan metni yavaşça sağa-sola kaydırır; sığıyorsa düz TextBlock gibi durur.
public sealed class KayanMetin : Decorator
{
    private readonly TextBlock _metin = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TranslateTransform _kaydir = new();
    private double _sonTasma = double.NaN;

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(KayanMetin),
            new PropertyMetadata("", (d, _) => ((KayanMetin)d).MetinDegisti()));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(KayanMetin),
            new PropertyMetadata(Brushes.White, (d, e) => ((KayanMetin)d)._metin.Foreground = (Brush)e.NewValue));
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(KayanMetin),
            new PropertyMetadata(12.0, (d, e) => ((KayanMetin)d)._metin.FontSize = (double)e.NewValue));
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(KayanMetin),
            new PropertyMetadata(FontWeights.Normal, (d, e) => ((KayanMetin)d)._metin.FontWeight = (FontWeight)e.NewValue));
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public KayanMetin()
    {
        ClipToBounds = true;
        _metin.RenderTransform = _kaydir;
        _metin.HorizontalAlignment = HorizontalAlignment.Left;
        Child = _metin;
        SizeChanged += (_, _) => Yenile();
    }

    private void MetinDegisti()
    {
        _metin.Text = Text;
        _sonTasma = double.NaN;
        Yenile();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        // Çocuğa sonsuz genişlik ver ki gerçek metin genişliği ölçülsün; kendimiz verilen alanı doldururuz
        _metin.Measure(new Size(double.PositiveInfinity, constraint.Height));
        double h = _metin.DesiredSize.Height;
        double w = double.IsInfinity(constraint.Width) ? _metin.DesiredSize.Width : constraint.Width;
        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        _metin.Arrange(new Rect(0, 0, Math.Max(arrangeSize.Width, _metin.DesiredSize.Width), arrangeSize.Height));
        Yenile();
        return arrangeSize;
    }

    private void Yenile()
    {
        double tasma = _metin.DesiredSize.Width - ActualWidth;
        if (Math.Abs(tasma - _sonTasma) < 0.5) return;
        _sonTasma = tasma;

        _kaydir.BeginAnimation(TranslateTransform.XProperty, null);
        _kaydir.X = 0;
        if (tasma <= 1) return;

        // 28 px/sn hız, iki uçta 1.4 sn bekleme
        double saniye = Math.Max(1.5, tasma / 28.0);
        var anim = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        var t0 = TimeSpan.Zero;
        var t1 = TimeSpan.FromSeconds(1.4);
        var t2 = t1 + TimeSpan.FromSeconds(saniye);
        var t3 = t2 + TimeSpan.FromSeconds(1.4);
        var t4 = t3 + TimeSpan.FromSeconds(saniye);
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, t0));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, t1));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-tasma, t2));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-tasma, t3));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, t4));
        _kaydir.BeginAnimation(TranslateTransform.XProperty, anim);
    }
}
