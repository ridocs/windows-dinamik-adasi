using System.Windows.Media;

namespace DinamikAda.Servisler;

public enum DuyuruTuru { Bilgi, Basari, Uyari, Ses, Bildirim }

/// Kapsülde kısa süre gösterilen tek bir mesaj.
public sealed record Duyuru(
    DuyuruTuru Tur,
    string Baslik,
    string Metin = "",
    string Simge = "",           // Segoe MDL2 glifi veya emoji
    ImageSource? Logo = null,    // uygulama logosu (bildirimler)
    double? Oran = null,         // 0..1: ses seviyesi gibi çubuk
    int SaniyeOverride = 0,
    string Anahtar = "",         // aynı anahtarlı duyuru kuyruktakini değiştirir (ses tuşu art arda)
    bool Surecte = false)        // sürüyor: belirsiz ilerleme animasyonu (kopyalama)
{
    public int Saniye(int varsayilan) => SaniyeOverride > 0 ? SaniyeOverride : varsayilan;
}

/// Duyuruları sıraya koyar; aynı anahtarlı yeni duyuru eskisinin yerine geçer.
public sealed class DuyuruKuyrugu
{
    private readonly LinkedList<Duyuru> _kuyruk = new();
    public event Action? Degisti;

    /// Süzgeç: false dönerse duyuru atılır (toplantı modu). Çağıran sayaç tutabilir.
    public Func<Duyuru, bool>? Suzgec { get; set; }

    public int Sayi { get { lock (_kuyruk) return _kuyruk.Count; } }

    public void Ekle(Duyuru d)
    {
        if (Suzgec != null && !Suzgec(d)) return;
        lock (_kuyruk)
        {
            if (!string.IsNullOrEmpty(d.Anahtar))
            {
                var eski = _kuyruk.FirstOrDefault(x => x.Anahtar == d.Anahtar);
                if (eski != null) _kuyruk.Remove(eski);
            }
            _kuyruk.AddLast(d);
        }
        Degisti?.Invoke();
    }

    public Duyuru? Al()
    {
        lock (_kuyruk)
        {
            if (_kuyruk.Count == 0) return null;
            var d = _kuyruk.First!.Value;
            _kuyruk.RemoveFirst();
            return d;
        }
    }

    /// Gösterilmekte olan duyuruyla aynı anahtarlı yeni bir duyuru geldi mi (ses tuşu basılı tutuluyor)?
    public Duyuru? AnahtarlaAl(string anahtar)
    {
        if (string.IsNullOrEmpty(anahtar)) return null;
        lock (_kuyruk)
        {
            var d = _kuyruk.FirstOrDefault(x => x.Anahtar == anahtar);
            if (d != null) _kuyruk.Remove(d);
            return d;
        }
    }
}
