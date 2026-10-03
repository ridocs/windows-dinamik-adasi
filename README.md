# Windows Dinamik Adası

Windows 11 için, ekranın üst ortasında duran, macOS Dynamic Island benzeri bir kapsül. Fare uzaktayken küçük bir hap; üstüne gelince çalan müziğe, gelen mesaja, haznedeki dosyalara ya da saat ve sistem bilgisine göre genişler. C# ve WPF ile yazıldı, tek bir exe olarak çalışır.

Kişisel kullanım için geliştirildi, topluluğa açıldı. Türkçe arayüz, Türkçe kaynak kodu.

## Neler yapar

**Medya**
- Çalan parça: kapak, başlık, sanatçı, ilerleme çubuğu (tıklayarak atlama), oynat / duraklat / ileri / geri. Spotify, tarayıcı, Media Player ve sistem medya oturumu veren her uygulama.
- Şarkı sözleri: lrclib.net üzerinden zamanlı sözler; geniş panelde kendi satırında, kompakt görünümde şarkı adının yerinde akar. Hesap gerekmez.
- Spotify beğen ve sıradakiler (Spotify Web API; uygulama sahibinde Premium ister, yoksa beğen için masaüstü kısayolu kullanılır).
- Ses tuşu animasyonu, kulaklık tak / çıkar duyurusu, ses çıkış aygıtı değişimi.

**Mesajlar**
- Windows bildirimleri kapsüle düşer; tek okunmamış mesaj çipi, üstüne gelince içerik, tıklayınca uygulamada açma.
- WhatsApp köprüsü (`wa-servis`, Node + Baileys): mesajlar pencere açılmadan gelir, kapsülden cevap yazılır ve arka planda gider. Rehber `.vcf` dosyasından okunur. Arşivlenmiş sohbetler sessizdir.
- Toplantı modu: mikrofon açılınca bildirimler bastırılır, bitince özet.

**Dosya haznesi**
- Kapsüle sürüklenen ya da Ctrl+V ile yapıştırılan dosyalar hazneye alınır (liste / ızgara). Sunucuya yükleme (scp), paylaşım linki, zip, e-posta eki, WhatsApp ile telefona gönderme, dışarı sürükleme (taşı / kopyala / kısayol).
- Haznedeki metin dosyalarını Claude'a özetletme.

**Boş panel**
- Büyük saat, tarih, CPU / RAM / sunucu (SRV) halkaları, ağ hızı, Tailscale durumu, ses çubuğu.
- Kısayol çubuğu: kurulu uygulamalardan seçilen simgeler, sürükle-bırak ile düzenleme.
- Çalışma düzenleri: açık pencerelerin yerleşimini kaydet, tek tıkla geri kur (kapalı uygulamalar açılır).
- Claude'a sor: kapsülün içinde soru, seçili metni özetle / çevir, panoyu özetle. API anahtarı yoksa makinedeki Claude Code CLI kullanılır.
- Günün özeti: akşam belirlenen saatte uygulama süreleri, mesajlar, müzik ve toplantı süresi.

**Sistem**
- Uzak sunucu sağlığı: ping, HTTP, SSH ile yük / disk / RAM; düşünce ve geri gelince duyuru.
- Tam ekran oyunda gizlenmek yerine ince şerit (saat, CPU, GPU sıcaklığı, RAM, mesaj rozeti); tarayıcı ve video tam ekranında gizlenir.
- Pil olayları, pomodoro, Windows ile başlat, ekran ve ölçek seçimi.

**Maskot**
- Piksel maskot: ön plandaki uygulamaya göre kod yazar, oyun oynar, müzik dinler, boşta uyur. Tıklayınca zıplar, uzun çalışmada mola hatırlatır, gece uykulu bakar, özel günlerde şapka takar.

## Kurulum

Gereksinimler: Windows 10 19041+ (Windows 11 önerilir), .NET 8 SDK. WhatsApp köprüsü için Node.js 20+.

```powershell
git clone https://github.com/ridocs/windows-dinamik-adasi.git
cd windows-dinamik-adasi
dotnet build -c Release
.\bin\Release\net8.0-windows10.0.19041.0\DinamikAda.exe
```

WhatsApp köprüsü (isteğe bağlı):

```powershell
cd wa-servis
npm install
```

Kapsül açılınca köprüyü kendisi başlatır; ilk seferde kapsülde çıkan QR kodunu telefondan okutun (WhatsApp > Bağlı cihazlar). Oturum `wa-servis/oturum-baileys` klasöründe kalır; bu klasörü paylaşmayın.

Ayarlar tepsi simgesinden açılır ve `%AppData%\DinamikAda\ayarlar.json` dosyasında tutulur. Sunucu, paylaşım linki, Spotify ve Claude alanları varsayılan olarak boştur; kendi değerlerinizi girin.

## Test kancası

`DINAMIKADA_GUNLUK=1` ortam değişkeniyle başlatılınca kapsül `%TEMP%\dinamikada-gunluk.txt` dosyasına günlük yazar ve `%TEMP%\dinamikada-komut.txt` dosyasından komut okur: `genislet`, `daralt`, `bos 1`, `mini 1`, `sor <metin>`, `ozet`, `duzen-yakala <ad>`, `tepki`, `sapka 1` gibi. Fare ve klavyeye dokunmadan ekran görüntüsüyle doğrulama için tasarlandı.

## Mimari (kısaca)

- `MainWindow.xaml(.cs)`: kapsül, paneller arası geçiş, duyuru kuyruğu.
- `Servisler/`: medya, ses, bildirim, WhatsApp köprüsü istemcisi, hazne, sözler, Spotify, Claude, sunucu, ağ, GPU, düzenler, özet.
- `Kontroller/`: maskot, hazne paneli, ses satırı, uygulama seçici, kayan metin.
- `wa-servis/`: Baileys tabanlı yerel HTTP köprüsü (127.0.0.1:5461).

## Lisans

MIT. Ayrıntı için `LICENSE`.
