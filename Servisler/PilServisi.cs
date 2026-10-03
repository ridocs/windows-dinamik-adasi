using Forms = System.Windows.Forms;

namespace DinamikAda.Servisler;

/// Pil durumunu okur, geçişleri (şarja takıldı, çıktı, %20 altı, doldu) duyuru olarak üretir.
public sealed class PilServisi
{
    private bool? _oncekiSarjda;
    private int _oncekiYuzde = -1;
    private bool _dusukUyarildi;
    private bool _dolduUyarildi;

    public bool PilVar { get; private set; }
    public int Yuzde { get; private set; } = -1;
    public bool Sarjda { get; private set; }

    public event Action<Duyuru>? Olay;

    public void Tik()
    {
        var g = Forms.SystemInformation.PowerStatus;
        PilVar = !g.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery);
        if (!PilVar) { Yuzde = -1; return; }

        float oran = g.BatteryLifePercent;
        Yuzde = (oran >= 0 && oran <= 1) ? (int)Math.Round(oran * 100) : -1;
        Sarjda = g.PowerLineStatus == Forms.PowerLineStatus.Online;

        if (_oncekiSarjda.HasValue && _oncekiSarjda.Value != Sarjda)
        {
            Olay?.Invoke(Sarjda
                ? new Duyuru(DuyuruTuru.Basari, "Şarj oluyor", Yuzde >= 0 ? $"%{Yuzde}" : "", Simge: "")
                : new Duyuru(DuyuruTuru.Bilgi, "Pilde", Yuzde >= 0 ? $"%{Yuzde}" : "", Simge: ""));
            _dolduUyarildi = false;
        }
        _oncekiSarjda = Sarjda;

        if (Yuzde >= 0)
        {
            if (!Sarjda && Yuzde <= 20 && !_dusukUyarildi)
            {
                _dusukUyarildi = true;
                Olay?.Invoke(new Duyuru(DuyuruTuru.Uyari, "Pil azaldı", $"%{Yuzde}, şarja takın", Simge: ""));
            }
            if (Yuzde > 25) _dusukUyarildi = false;

            if (Sarjda && Yuzde >= 100 && !_dolduUyarildi)
            {
                _dolduUyarildi = true;
                Olay?.Invoke(new Duyuru(DuyuruTuru.Basari, "Pil doldu", "Fişi çekebilirsiniz", Simge: ""));
            }
        }
        _oncekiYuzde = Yuzde;
    }

    /// Kısa metin: "⚡84%" veya "84%". Pil yoksa boş.
    public string Metin => !PilVar || Yuzde < 0 ? "" : (Sarjda ? "⚡" : "") + $"{Yuzde}%";
}
