using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DinamikAda;

public partial class App : Application
{
    private Forms.NotifyIcon? _tepsi;
    private MainWindow? _pencere;
    private AyarlarPenceresi? _ayarPenceresi;
    private Ayarlar _ayar = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _ayar = Ayarlar.Yukle();
        _pencere = new MainWindow(_ayar);
        _pencere.Show();

        _tepsi = new Forms.NotifyIcon
        {
            Icon = TepsiSimgesiOlustur(),
            Text = "Dinamik Ada",
            Visible = true,
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Ayarlar…", null, (_, _) => AyarlariAc());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => Kapat());
        _tepsi.ContextMenuStrip = menu;
        _tepsi.DoubleClick += (_, _) => AyarlariAc();

        // "DinamikAda.exe --ayarlar": ayar penceresiyle aç
        if (e.Args.Any(a => a.Equals("--ayarlar", StringComparison.OrdinalIgnoreCase)))
            AyarlariAc();
    }

    public void AyarlariAc()
    {
        if (_ayarPenceresi != null) { _ayarPenceresi.Activate(); return; }
        _ayarPenceresi = new AyarlarPenceresi(_ayar, yeni =>
        {
            _ayar = yeni;
            _pencere?.AyarlariUygula(yeni);
        });
        _ayarPenceresi.Closed += (_, _) => _ayarPenceresi = null;
        _ayarPenceresi.Show();
        _ayarPenceresi.Activate();
    }

    private void Kapat()
    {
        if (_tepsi != null) { _tepsi.Visible = false; _tepsi.Dispose(); }
        _ayarPenceresi?.Close();
        _pencere?.Kapat();
        Shutdown();
    }

    /// Küçük bir siyah kapsül çizip tepsi simgesi olarak kullanır; dış dosya gerekmez.
    private static Icon TepsiSimgesiOlustur()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var yol = new GraphicsPath();
        var r = new Rectangle(2, 10, 28, 12);
        int c = 12;
        yol.AddArc(r.X, r.Y, c, c, 90, 180);
        yol.AddArc(r.Right - c, r.Y, c, c, 270, 180);
        yol.CloseFigure();
        using var firca = new SolidBrush(Color.FromArgb(0x0B, 0x0B, 0x0F));
        g.FillPath(firca, yol);
        using var kalem = new Pen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1f);
        g.DrawPath(kalem, yol);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
