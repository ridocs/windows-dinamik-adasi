using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DinamikAda;

/// Kullanıcı ayarları. %AppData%\DinamikAda\ayarlar.json içinde tutulur.
public sealed class Ayarlar
{
    // Konum ve görünüm
    public int Ekran { get; set; } = 0;          // Screen.AllScreens dizini
    public double UstBosluk { get; set; } = 6;   // kapsülün ekran üstünden uzaklığı (DIP)
    public double Olcek { get; set; } = 1.0;     // 0.8 .. 1.4
    public double Saydamlik { get; set; } = 1.0; // 0.5 .. 1.0
    public bool TamEkrandaGizle { get; set; } = true;
    public bool OyunKatmaniAcik { get; set; } = true;   // tam ekran OYUNDA gizlenme: ince şerit (saat, CPU, GPU, RAM); tarayıcı/video tam ekranında yine gizlen
    public bool SesOsdGizle { get; set; } = true;   // Windows'un kendi ses barını gizle
    public string HizliYanit { get; set; } = "Oyundayım, birazdan yazarım.";   // oyunda Ctrl+Alt+Y
    public bool OyunSesProfiliAcik { get; set; } = true;
    public string OyunKulaklikAd { get; set; } = "Headphones";                   // aygıt adının parçası
    public int OyunMuzikSeviye { get; set; } = 30;                               // oyunda Spotify sesi (%)
    public bool IzlemeModuAcik { get; set; } = true;
    public bool WindowsIleBaslat { get; set; } = true;

    // Modüller
    public bool SesAcik { get; set; } = true;
    public bool KulaklikAcik { get; set; } = true;
    public bool PilAcik { get; set; } = true;
    public bool MikrofonKameraAcik { get; set; } = true;
    public bool ToplantiModuAcik { get; set; } = true;     // mikrofon açıkken bildirimler sessiz, bitince özet
    public bool BildirimAcik { get; set; } = true;
    public bool HavaAcik { get; set; } = false;  // 2026-10-03: kullanıcı isteğiyle varsayılan kapalı
    public string HavaSehir { get; set; } = "";  // boşsa IP'den konum

    // Dosya haznesi
    public string HazneModu { get; set; } = "hazne";      // "hazne": bırakınca beklet, düğmeyle yükle | "hemen": bırakınca yükle
    public string HazneGorunum { get; set; } = "liste";   // "liste" | "izgara"
    public string HazneSurukleEtkisi { get; set; } = "otomatik"; // "otomatik" (Windows kuralı; Alt=kısayol, Ctrl=kopya) | "tasi" | "kopyala" | "kisayol"
    public string LinkHedef { get; set; } = "";   // örnek: kullanici@sunucu:/var/www/site/paylas/
    public string LinkOnek { get; set; } = "";    // örnek: https://alanadi/paylas/
    public int PomodoroDakika { get; set; } = 25;
    public int DuyuruSaniye { get; set; } = 5;

    // Sürükle-bırak ile uzak sunucuya kopyalama (scp)
    public string SurukleHedef { get; set; } = "";   // örnek: kullanici@sunucu:~/Desktop/
    public int SuruklePort { get; set; } = 22;
    public string SurukleAnahtar { get; set; } = "";  // boşsa ssh varsayılan anahtarları

    // Boş paneldeki kısayol çubuğu: her satır "Ad = yol" (exe yolu, protokol ya da shell:AppsFolder\AUMID). Boşsa varsayılanlar.
    public string Kisayollar { get; set; } = "";

    // WhatsApp cevabı için rehber: her satır "Ad = 905xxxxxxxxx" (bildirimdeki gönderen adıyla eşleşir)
    public string Rehber { get; set; } = "";
    public string RehberDosyasi { get; set; } = "";   // .vcf yolu; boşsa %AppData%\DinamikAda\rehber.vcf denenir
    public bool WhatsAppOtomatikGonder { get; set; } = true;   // sohbet açılınca Enter'ı kapsül basar (servis yoksa)
    public bool WaServisAcik { get; set; } = true;             // wa-servis (WhatsApp Web köprüsü) kapsülle başlasın
    public int WaServisPort { get; set; } = 5461;

    // Sunucu durumu (boş panelde SRV halkası; düşerse uyarı)
    public bool SunucuAcik { get; set; } = true;
    public string SunucuAdres { get; set; } = "";          // IP ya da alan adı; boşsa SRV halkası gizlenir
    public string SunucuSshKullanici { get; set; } = "root";
    public string SunucuAnahtar { get; set; } = "";        // özel anahtar yolu; boşsa ssh'nin varsayılan anahtarları
    public string SunucuUrller { get; set; } = "";         // her satıra bir URL

    // Ağ: boş panelde indirme/yükleme hızı ve Tailscale durumu; bağlantı değişince duyuru
    public bool AgAcik { get; set; } = true;

    // İndirilenler klasörüne düşen dosya hazneye alınır ve duyurulur
    public bool IndirmeIzleAcik { get; set; } = true;
    // WhatsApp'ta kendine ("Siz") attığın dosya ve bağlantılar hazneye düşer
    public bool TelefondanHazneyeAcik { get; set; } = true;

    // Müzik: şarkı sözleri (lrclib.net, hesapsız) ve Spotify (beğen, sıradakiler; PKCE girişi)
    public bool SozlerAcik { get; set; } = true;
    public string SpotifyClientId { get; set; } = "";
    public string SpotifyRefreshToken { get; set; } = "";
    public string SpotifyKullanici { get; set; } = "";

    // Claude'a sor: anahtar boşsa Claude Code CLI (abonelik) kullanılır
    public bool ClaudeAcik { get; set; } = true;
    public string ClaudeApiKey { get; set; } = "";
    public string ClaudeModel { get; set; } = "claude-sonnet-5-5";
    public string ClaudeNot { get; set; } = "";          // sistem notuna eklenir ("cevapları İngilizce ver" gibi)


    // Günün özeti: akşam belirlenen saatte kapsülde gösterilir (uygulama süreleri, mesajlar, müzik, toplantı)
    public string NotDosyasi { get; set; } = "";   // boşsa masaüstüDinamik Ada Notlar.md
    public bool OzetAcik { get; set; } = true;
    public string OzetSaat { get; set; } = "21:00";

    // Canavar maskot
    public bool CanavarAcik { get; set; } = true;
    public int UykuDakika { get; set; } = 3;        // bu kadar süre giriş yoksa uyur
    public int MolaDakika { get; set; } = 50;       // kesintisiz bu kadar dakika başındaysan mola hatırlatır (0: kapalı)
    public string DogumGunu { get; set; } = "";     // "gg.aa": o gün maskot parti şapkası takar

    public static string Klasor =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinamikAda");

    public static string Dosya => Path.Combine(Klasor, "ayarlar.json");

    private static readonly JsonSerializerOptions Secenek = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static Ayarlar Yukle()
    {
        try
        {
            if (File.Exists(Dosya))
                return JsonSerializer.Deserialize<Ayarlar>(File.ReadAllText(Dosya), Secenek) ?? new Ayarlar();
        }
        catch { /* bozuk dosya: varsayılanlarla devam */ }
        return new Ayarlar();
    }

    public void Kaydet()
    {
        Directory.CreateDirectory(Klasor);
        File.WriteAllText(Dosya, JsonSerializer.Serialize(this, Secenek));
    }

    public Ayarlar Kopya() => (Ayarlar)MemberwiseClone();
}
