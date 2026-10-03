namespace DinamikAda.Servisler;

/// Basit geri sayım: başlat, duraklat, sıfırla, 5 dk ekle. Saniyede bir Tik() çağrılır.
public sealed class Pomodoro
{
    private DateTime _bitis;
    private TimeSpan _duraklamadaKalan;

    public TimeSpan Toplam { get; private set; } = TimeSpan.FromMinutes(25);
    public bool Calisiyor { get; private set; }
    /// Başlatılmış ama henüz sıfırlanmamış (çalışıyor veya duraklatılmış)
    public bool Aktif { get; private set; }

    public event Action? Degisti;
    public event Action? Bitti;

    public TimeSpan Kalan
    {
        get
        {
            if (!Aktif) return Toplam;
            if (!Calisiyor) return _duraklamadaKalan;
            var k = _bitis - DateTime.Now;
            return k < TimeSpan.Zero ? TimeSpan.Zero : k;
        }
    }

    public double Oran => Toplam.TotalSeconds <= 0 ? 0 : 1 - Kalan.TotalSeconds / Toplam.TotalSeconds;

    public void VarsayilanSure(int dakika)
    {
        if (dakika < 1) dakika = 1;
        if (!Aktif) { Toplam = TimeSpan.FromMinutes(dakika); Degisti?.Invoke(); }
    }

    public void BaslatDuraklat()
    {
        if (!Aktif)
        {
            Aktif = true; Calisiyor = true;
            _bitis = DateTime.Now + Toplam;
        }
        else if (Calisiyor)
        {
            _duraklamadaKalan = Kalan; Calisiyor = false;
        }
        else
        {
            _bitis = DateTime.Now + _duraklamadaKalan; Calisiyor = true;
        }
        Degisti?.Invoke();
    }

    public void Sifirla()
    {
        Aktif = false; Calisiyor = false;
        Degisti?.Invoke();
    }

    public void Ekle(int dakika)
    {
        var ek = TimeSpan.FromMinutes(dakika);
        Toplam += ek;
        if (Calisiyor) _bitis += ek;
        else if (Aktif) _duraklamadaKalan += ek;
        Degisti?.Invoke();
    }

    public void Tik()
    {
        if (!Calisiyor) return;
        if (DateTime.Now >= _bitis)
        {
            Aktif = false; Calisiyor = false;
            Bitti?.Invoke();
        }
        Degisti?.Invoke();
    }

    public string Metin
    {
        get
        {
            var k = Kalan;
            return k.TotalHours >= 1 ? $"{(int)k.TotalHours}:{k.Minutes:00}:{k.Seconds:00}" : $"{k.Minutes}:{k.Seconds:00}";
        }
    }
}
