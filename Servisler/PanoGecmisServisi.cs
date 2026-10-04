namespace DinamikAda.Servisler;

/// Pano (clipboard) metin geçmişi. Kopyalanan son metinleri tutar; tıklayınca tekrar panoya koyar
/// ya da hazneye/soruya aktarılır. Oturum içi (bellekte), gizlilik için kalıcı değil.
/// Parola benzeri içerikler (çok kısa + yalnız gizli görünüm) dışlanmaz; kullanıcı Temizle ile siler.
public sealed class PanoGecmisServisi
{
    public sealed record Oge(string Metin, DateTime Zaman)
    {
        /// Listede gösterilecek tek satırlık kısa biçim.
        public string Onizleme
        {
            get
            {
                var t = Metin.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
                while (t.Contains("  ")) t = t.Replace("  ", " ");
                return t.Length > 90 ? t[..90] + "…" : t;
            }
        }
        public bool Baglanti => Metin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Metin.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private readonly List<Oge> _ogeler = new();
    private readonly int _enCok;
    private string _sonEklenen = "";

    public PanoGecmisServisi(int enCok = 30) => _enCok = Math.Clamp(enCok, 5, 100);

    public IReadOnlyList<Oge> Ogeler => _ogeler;
    public event Action? Degisti;

    /// Yeni pano metni geldi. Aynı metin varsa öne alınır; ardışık tekrar yok sayılır.
    public void Ekle(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return;
        if (metin.Length > 20000) metin = metin[..20000];
        if (metin == _sonEklenen) return;
        _sonEklenen = metin;
        _ogeler.RemoveAll(o => o.Metin == metin);
        _ogeler.Insert(0, new Oge(metin, DateTime.Now));
        while (_ogeler.Count > _enCok) _ogeler.RemoveAt(_ogeler.Count - 1);
        Degisti?.Invoke();
    }

    public void Cikar(Oge o) { if (_ogeler.Remove(o)) Degisti?.Invoke(); }

    public void Temizle() { _ogeler.Clear(); _sonEklenen = ""; Degisti?.Invoke(); }
}
