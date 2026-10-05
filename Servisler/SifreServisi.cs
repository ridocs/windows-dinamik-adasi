using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DinamikAda.Servisler;

/// Şifre (parola) üretir ve saklar. Saklama JSON dosyasıdır; şifre alanları Windows DPAPI ile
/// (yalnız bu kullanıcı hesabında çözülebilir) şifrelenip base64 olarak yazılır, düz metin değildir.
/// Dosya: %AppData%\DinamikAda\sifreler.json
public sealed class SifreServisi
{
    public sealed class Kayit
    {
        public string Ad { get; set; } = "";
        public string Sifre { get; set; } = "";
        public DateTime Zaman { get; set; }
        public string ZamanMetni => Zaman.ToString("yyyy-MM-dd HH:mm");
        public bool Acik { get; set; }   // listede açık mı gösteriliyor (sadece arayüz durumu)
        public string Gorunen => Acik ? Sifre : new string((char)0x2022, Math.Clamp(Sifre.Length, 1, 16));
    }

    private sealed class DiskKayit
    {
        public string Ad { get; set; } = "";
        public string Sifreli { get; set; } = "";
        public long Zaman { get; set; }
    }

    private static string Dosya =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinamikAda", "sifreler.json");

    private static readonly byte[] Entropi = Encoding.UTF8.GetBytes("DinamikAda-sifre-v1");

    private readonly List<Kayit> _liste = new();
    public IReadOnlyList<Kayit> Kayitlar => _liste;
    public event Action? Degisti;

    public SifreServisi() => Yukle();

    /// Güçlü rastgele şifre üret. benzersiz=true: karışan karakterleri (0O1lI) eler.
    public static string Uret(int uzunluk, bool rakam = true, bool simge = true, bool benzersiz = false)
    {
        string buyuk = benzersiz ? "ABCDEFGHJKLMNPQRSTUVWXYZ" : "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string kucuk = benzersiz ? "abcdefghijkmnpqrstuvwxyz" : "abcdefghijklmnopqrstuvwxyz";
        string rk = benzersiz ? "23456789" : "0123456789";
        const string sm = "!@#$%^&*-_=+?.";

        var gruplar = new List<string> { buyuk, kucuk };
        if (rakam) gruplar.Add(rk);
        if (simge) gruplar.Add(sm);
        string havuz = string.Concat(gruplar);

        uzunluk = Math.Clamp(uzunluk, 4, 128);
        var ch = new char[uzunluk];
        // Her seçili gruptan en az bir karakter olsun
        int i = 0;
        foreach (var grup in gruplar)
            if (i < uzunluk) ch[i++] = grup[RandomNumberGenerator.GetInt32(grup.Length)];
        for (; i < uzunluk; i++) ch[i] = havuz[RandomNumberGenerator.GetInt32(havuz.Length)];
        // Karıştır (Fisher-Yates)
        for (int j = ch.Length - 1; j > 0; j--)
        {
            int k = RandomNumberGenerator.GetInt32(j + 1);
            (ch[j], ch[k]) = (ch[k], ch[j]);
        }
        return new string(ch);
    }

    public void Ekle(string ad, string sifre)
    {
        if (string.IsNullOrWhiteSpace(sifre)) return;
        ad = string.IsNullOrWhiteSpace(ad) ? "(adsız)" : ad.Trim();
        _liste.Insert(0, new Kayit { Ad = ad, Sifre = sifre, Zaman = DateTime.Now });
        Kaydet();
        Degisti?.Invoke();
    }

    public void Cikar(Kayit k) { if (_liste.Remove(k)) { Kaydet(); Degisti?.Invoke(); } }

    private void Kaydet()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Dosya)!);
            var disk = _liste.Select(k => new DiskKayit
            {
                Ad = k.Ad,
                Sifreli = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(k.Sifre), Entropi, DataProtectionScope.CurrentUser)),
                Zaman = new DateTimeOffset(k.Zaman).ToUnixTimeSeconds(),
            }).ToList();
            File.WriteAllText(Dosya, JsonSerializer.Serialize(disk, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void Yukle()
    {
        try
        {
            if (!File.Exists(Dosya)) return;
            var disk = JsonSerializer.Deserialize<List<DiskKayit>>(File.ReadAllText(Dosya));
            if (disk == null) return;
            _liste.Clear();
            foreach (var d in disk)
            {
                string s = "";
                try { s = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(d.Sifreli), Entropi, DataProtectionScope.CurrentUser)); }
                catch { }
                _liste.Add(new Kayit { Ad = d.Ad, Sifre = s, Zaman = DateTimeOffset.FromUnixTimeSeconds(d.Zaman).LocalDateTime });
            }
        }
        catch { }
    }
}
