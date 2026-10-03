using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DinamikAda.Servisler;

/// Günün özeti: ön plandaki uygulama süreleri, mesajlar, müzik, toplantı, pomodoro, yükleme.
/// Saniyede bir Tik(); dakikada bir %AppData%\DinamikAda\gunluk\YYYY-MM-DD.json. Boşta (3 dk giriş yok) süre sayılmaz.
public sealed class OzetServisi
{
    public sealed class Izleme { public string Baslik { get; set; } = ""; public int Dakika { get; set; } public string Saat { get; set; } = ""; }

    public sealed class Gun
    {
        public string Tarih { get; set; } = "";
        public Dictionary<string, int> Uygulama { get; set; } = new();   // süreç adı -> saniye
        public Dictionary<string, int> Gonderen { get; set; } = new();   // WhatsApp gönderen -> adet
        public int Aktif { get; set; }        // saniye
        public int Medya { get; set; }
        public int Toplanti { get; set; }
        public int Pomodoro { get; set; }
        public int Yukleme { get; set; }
        public int Oyun { get; set; }         // saniye
        public int GpuTepe { get; set; } = -1;
        public List<Izleme> Izlemeler { get; set; } = new();
        public int MesajWa { get; set; }
        public int MesajDiger { get; set; }
        public int IlkDakika { get; set; } = -1;   // günün ilk etkinliği (dakika)
        public int SonDakika { get; set; } = -1;
    }

    public Gun Bugun { get; private set; } = new() { Tarih = DateTime.Today.ToString("yyyy-MM-dd") };
    public int SurekliAktifSaniye { get; private set; }   // son 3 dk'lık boşluktan beri kesintisiz çalışma
    public bool Bosta { get; private set; }

    private bool _kirli;
    private DateTime _sonKayit = DateTime.MinValue;
    private static string Klasor => Path.Combine(Ayarlar.Klasor, "gunluk");

    public void Yukle()
    {
        try
        {
            string yol = Path.Combine(Klasor, Bugun.Tarih + ".json");
            if (File.Exists(yol)) Bugun = JsonSerializer.Deserialize<Gun>(File.ReadAllText(yol)) ?? Bugun;
        }
        catch { }
    }

    public void Tik(string surec, bool medyaOynuyor, bool toplanti)
    {
        string tarih = DateTime.Today.ToString("yyyy-MM-dd");
        if (tarih != Bugun.Tarih) { Kaydet(); Bugun = new Gun { Tarih = tarih }; SurekliAktifSaniye = 0; }

        double bostaSn = BostaSaniye();
        Bosta = bostaSn >= 180;
        if (Bosta) SurekliAktifSaniye = 0;
        else
        {
            SurekliAktifSaniye++;
            Bugun.Aktif++;
            if (surec.Length > 0) Bugun.Uygulama[surec] = Bugun.Uygulama.GetValueOrDefault(surec) + 1;
            int dk = (int)DateTime.Now.TimeOfDay.TotalMinutes;
            if (Bugun.IlkDakika < 0) Bugun.IlkDakika = dk;
            Bugun.SonDakika = dk;
        }
        if (medyaOynuyor) Bugun.Medya++;
        if (toplanti) Bugun.Toplanti++;
        _kirli = true;
        if ((DateTime.Now - _sonKayit).TotalSeconds >= 60) Kaydet();
    }

    public void OyunEkle(int saniye, int gpuTepe, string ad) { Bugun.Oyun += saniye; if (gpuTepe > Bugun.GpuTepe) Bugun.GpuTepe = gpuTepe; _kirli = true; }
    public void IzlemeEkle(string baslik, int dakika) { var v = Bugun.Izlemeler.FirstOrDefault(x => x.Baslik == baslik); if (v != null) v.Dakika += dakika; else Bugun.Izlemeler.Add(new Izleme { Baslik = baslik, Dakika = dakika, Saat = DateTime.Now.ToString("HH:mm") }); _kirli = true; }

    public void Say(string tur)
    {
        if (tur == "pomodoro") Bugun.Pomodoro++;
        else if (tur == "yukleme") Bugun.Yukleme++;
        _kirli = true;
    }

    public void Mesaj(bool whatsapp, string gonderen)
    {
        if (whatsapp)
        {
            Bugun.MesajWa++;
            string g = gonderen.Trim();
            if (g.Length > 0) Bugun.Gonderen[g] = Bugun.Gonderen.GetValueOrDefault(g) + 1;
        }
        else Bugun.MesajDiger++;
        _kirli = true;
    }

    public void Kaydet()
    {
        if (!_kirli) return;
        try
        {
            Directory.CreateDirectory(Klasor);
            File.WriteAllText(Path.Combine(Klasor, Bugun.Tarih + ".json"), JsonSerializer.Serialize(Bugun, new JsonSerializerOptions { WriteIndented = true }));
            _kirli = false; _sonKayit = DateTime.Now;
        }
        catch { }
    }

    /// Kapsülde gösterilecek düz metin özet
    public string Ozet()
    {
        var g = Bugun;
        var sb = new StringBuilder();
        var kultur = new System.Globalization.CultureInfo("tr-TR");
        sb.Append(DateTime.Today.ToString("d MMMM dddd", kultur)).Append(" · başında ").Append(Sure(g.Aktif));
        if (g.IlkDakika >= 0) sb.Append($" ({g.IlkDakika / 60:00}:{g.IlkDakika % 60:00} - {g.SonDakika / 60:00}:{g.SonDakika % 60:00})");
        sb.AppendLine();

        var enCok = g.Uygulama.Where(k => k.Value >= 60).OrderByDescending(k => k.Value).Take(5).ToList();
        if (enCok.Count > 0)
            sb.AppendLine("En çok: " + string.Join(" · ", enCok.Select(k => $"{UygulamaAdi(k.Key)} {Sure(k.Value)}")));

        var mesaj = new List<string>();
        if (g.MesajWa > 0)
        {
            var top = g.Gonderen.OrderByDescending(k => k.Value).Take(3).Select(k => $"{Kisalt(k.Key)} {k.Value}");
            mesaj.Add($"{g.MesajWa} WhatsApp" + (g.Gonderen.Count > 0 ? $" (en çok: {string.Join(", ", top)})" : ""));
        }
        if (g.MesajDiger > 0) mesaj.Add($"{g.MesajDiger} başka bildirim");
        sb.AppendLine("Mesaj: " + (mesaj.Count > 0 ? string.Join(" · ", mesaj) : "yok"));

        var diger = new List<string>();
        if (g.Medya >= 60) diger.Add("müzik/video " + Sure(g.Medya));
        if (g.Toplanti >= 60) diger.Add("toplantı " + Sure(g.Toplanti));
        if (g.Pomodoro > 0) diger.Add($"pomodoro {g.Pomodoro}");
        if (g.Yukleme > 0) diger.Add($"yükleme {g.Yukleme}");
        if (g.Oyun >= 60) diger.Add("oyun " + Sure(g.Oyun) + (g.GpuTepe >= 0 ? $" (GPU tepe {g.GpuTepe}°)" : ""));
        if (diger.Count > 0) sb.AppendLine(string.Join(" · ", diger));
        if (g.Izlemeler.Count > 0) sb.AppendLine("İzlenen: " + string.Join(" · ", g.Izlemeler.OrderByDescending(x => x.Dakika).Take(3).Select(x => $"{x.Baslik} ({x.Dakika} dk)")));
        return sb.ToString().TrimEnd();
    }

    public static string Sure(int sn)
    {
        int dk = sn / 60;
        return dk >= 60 ? $"{dk / 60} sa {dk % 60} dk" : $"{dk} dk";
    }

    private static string Kisalt(string s) => s.Length > 18 ? s[..18] + "…" : s;

    private static readonly Dictionary<string, string> Adlar = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Code"] = "VS Code", ["chrome"] = "Chrome", ["msedge"] = "Edge", ["firefox"] = "Firefox", ["Spotify"] = "Spotify", ["Discord"] = "Discord",
        ["WhatsApp"] = "WhatsApp", ["devenv"] = "Visual Studio", ["explorer"] = "Dosya Gezgini", ["WindowsTerminal"] = "Terminal", ["Claude"] = "Claude",
        ["Telegram"] = "Telegram", ["steam"] = "Steam", ["VALORANT-Win64-Shipping"] = "Valorant", ["League of Legends"] = "LoL", ["Photoshop"] = "Photoshop",
        ["EXCEL"] = "Excel", ["WINWORD"] = "Word", ["POWERPNT"] = "PowerPoint", ["OUTLOOK"] = "Outlook", ["Teams"] = "Teams", ["ms-teams"] = "Teams", ["Zoom"] = "Zoom",
        ["Notion"] = "Notion", ["Obsidian"] = "Obsidian", ["Figma"] = "Figma", ["vlc"] = "VLC", ["Cursor"] = "Cursor", ["idea64"] = "IntelliJ", ["rider64"] = "Rider",
    };
    public static string UygulamaAdi(string surec) => Adlar.TryGetValue(surec, out var ad) ? ad : surec;

    // ---- boşta süresi ----
    [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    public static double BostaSaniye()
    {
        var li = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref li)) return 0;
        return (Environment.TickCount64 - li.dwTime) / 1000.0;
    }

    /// Son 7 günün özet dosyalarını topla: toplam süreler, en çok uygulamalar, mesaj, oyun, izleme
    public string HaftalikOzet()
    {
        Kaydet();
        var gunler = new List<Gun>();
        for (int i = 0; i < 7; i++)
        {
            var t = DateTime.Today.AddDays(-i);
            string yol = Path.Combine(Klasor, t.ToString("yyyy-MM-dd") + ".json");
            if (!File.Exists(yol)) continue;
            try { var g = JsonSerializer.Deserialize<Gun>(File.ReadAllText(yol)); if (g != null) gunler.Add(g); } catch { }
        }
        if (gunler.Count == 0) return "Son 7 günde kayıtlı etkinlik yok.";

        int aktif = gunler.Sum(g => g.Aktif), oyun = gunler.Sum(g => g.Oyun), medya = gunler.Sum(g => g.Medya);
        int wa = gunler.Sum(g => g.MesajWa), pomo = gunler.Sum(g => g.Pomodoro);
        var uyg = new Dictionary<string, int>();
        foreach (var g in gunler) foreach (var kv in g.Uygulama) uyg[kv.Key] = uyg.GetValueOrDefault(kv.Key) + kv.Value;
        var izl = new Dictionary<string, int>();
        foreach (var g in gunler) foreach (var v in g.Izlemeler) izl[v.Baslik] = izl.GetValueOrDefault(v.Baslik) + v.Dakika;

        var kultur = new System.Globalization.CultureInfo("tr-TR");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Son 7 gün ({gunler.Count} gün kayıtlı) · toplam başında {Sure(aktif)}, günlük ort. {Sure(aktif / Math.Max(1, gunler.Count))}");
        var enCok = uyg.Where(k => k.Value >= 300).OrderByDescending(k => k.Value).Take(6).ToList();
        if (enCok.Count > 0) sb.AppendLine("En çok: " + string.Join(" · ", enCok.Select(k => $"{UygulamaAdi(k.Key)} {Sure(k.Value)}")));
        var mesaj = new List<string>();
        if (wa > 0) mesaj.Add($"{wa} WhatsApp mesajı");
        if (pomo > 0) mesaj.Add($"{pomo} pomodoro");
        if (oyun >= 300) mesaj.Add("oyun " + Sure(oyun));
        if (medya >= 300) mesaj.Add("müzik/video " + Sure(medya));
        if (mesaj.Count > 0) sb.AppendLine(string.Join(" · ", mesaj));
        if (izl.Count > 0) sb.AppendLine("İzlenenler: " + string.Join(" · ", izl.OrderByDescending(k => k.Value).Take(4).Select(k => $"{k.Key} ({k.Value} dk)")));
        return sb.ToString().TrimEnd();
    }
}
