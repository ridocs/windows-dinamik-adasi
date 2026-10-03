using System.Net.Http;
using System.Text.Json;

namespace DinamikAda.Servisler;

/// Open-Meteo'dan anlık hava. Konum: ayarlardaki şehir, boşsa IP'den (ip-api.com).
public sealed class HavaServisi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private DateTime _sonYenileme = DateTime.MinValue;
    private string _sonSehirAyari = "\0";

    public bool Hazir { get; private set; }
    public int Sicaklik { get; private set; }
    public string Simge { get; private set; } = "";
    public string Aciklama { get; private set; } = "";
    public string Yer { get; private set; } = "";

    public event Action? Degisti;

    /// Yarım saatte bir ya da şehir ayarı değişince yeniler.
    public async Task TikAsync(string sehirAyari)
    {
        bool sehirDegisti = sehirAyari != _sonSehirAyari;
        if (!sehirDegisti && DateTime.Now - _sonYenileme < TimeSpan.FromMinutes(30)) return;
        _sonSehirAyari = sehirAyari;
        _sonYenileme = DateTime.Now;

        try
        {
            var (lat, lon, ad) = string.IsNullOrWhiteSpace(sehirAyari)
                ? await IpKonumAsync()
                : await SehirKonumAsync(sehirAyari.Trim());

            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      "&current=temperature_2m,weather_code,is_day&timezone=auto";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var c = doc.RootElement.GetProperty("current");
            Sicaklik = (int)Math.Round(c.GetProperty("temperature_2m").GetDouble());
            int kod = c.GetProperty("weather_code").GetInt32();
            bool gunduz = c.GetProperty("is_day").GetInt32() == 1;
            (Simge, Aciklama) = KodCevir(kod, gunduz);
            Yer = ad;
            Hazir = true;
            Degisti?.Invoke();
        }
        catch
        {
            // Ağ yoksa son bilinen değer kalır; 5 dk sonra tekrar dene
            _sonYenileme = DateTime.Now - TimeSpan.FromMinutes(25);
        }
    }

    private static async Task<(double, double, string)> IpKonumAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync("http://ip-api.com/json/?fields=lat,lon,city&lang=tr"));
        var r = doc.RootElement;
        return (r.GetProperty("lat").GetDouble(), r.GetProperty("lon").GetDouble(), r.GetProperty("city").GetString() ?? "");
    }

    private static async Task<(double, double, string)> SehirKonumAsync(string sehir)
    {
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(sehir)}&count=1&language=tr&format=json";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        var ilk = doc.RootElement.GetProperty("results")[0];
        return (ilk.GetProperty("latitude").GetDouble(), ilk.GetProperty("longitude").GetDouble(), ilk.GetProperty("name").GetString() ?? sehir);
    }

    /// WMO hava kodu → emoji ve Türkçe açıklama
    private static (string, string) KodCevir(int kod, bool gunduz) => kod switch
    {
        0 => (gunduz ? "☀️" : "🌙", "Açık"),
        1 => (gunduz ? "🌤️" : "🌙", "Az bulutlu"),
        2 => ("⛅", "Parçalı bulutlu"),
        3 => ("☁️", "Kapalı"),
        45 or 48 => ("🌫️", "Sisli"),
        51 or 53 or 55 or 56 or 57 => ("🌦️", "Çisenti"),
        61 or 63 or 65 or 66 or 67 => ("🌧️", "Yağmurlu"),
        71 or 73 or 75 or 77 => ("🌨️", "Karlı"),
        80 or 81 or 82 => ("🌧️", "Sağanak"),
        85 or 86 => ("❄️", "Kar sağanağı"),
        95 => ("⛈️", "Fırtına"),
        96 or 99 => ("⛈️", "Dolu fırtınası"),
        _ => ("🌡️", ""),
    };
}
