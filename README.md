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
- Kapsüle sürüklenen ya da Ctrl+V ile yapıştırılan dosyalar hazneye alınır (liste / ızgara). Sunucuya yükleme (scp), paylaşım linki, zip, WhatsApp ile telefona gönderme, dışarı sürükleme (taşı / kopyala / kısayol).
- Telefondan bilgisayara: WhatsApp'ta kendinize ("Siz") attığınız dosya ve bağlantılar kendiliğinden hazneye düşer.
- İndirme izleme: İndirilenler klasörüne biten her indirme kapsülde duyurulur ve hazneye alınır; oradan sürükleyip taşıyabilirsiniz.
- Haznedeki metin dosyalarını Claude'a özetletme.

**Boş panel**
- Büyük saat, tarih, CPU / RAM / sunucu (SRV) halkaları, ağ hızı, Tailscale durumu, ses çubuğu.
- Ses karışımı: uygulama başına ses seviyesi, sessiz ve çıkış aygıtı (örneğin iki monitörün hoparlörleri arasında yönlendirme), Windows'un "Uygulama ses ve cihaz tercihleri" sayfasına gerek kalmadan.
- Tüm aygıtlarda çal: varsayılan aygıttaki sesi bağlı tüm çıkış aygıtlarından (hoparlörler, kulaklık) aynı anda çalar; ses karışımı panelindeki tek düğmeyle açılıp kapanır.
- Pano geçmişi: kopyalanan son metinler tutulur; tıklayınca geri kopyalanır, QR ile telefona okutulur ya da silinir.
- QR üretici: herhangi bir metni ya da bağlantıyı QR koduna çevirip telefonla (iOS, Android) okutma; saf C#, harici kurulum yok.
- Şifreler: güçlü şifre üretir (uzunluk, rakam, simge, karışan karakterleri eleme) ve adlandırıp saklar; liste satırında göster, kopyala, QR ve sil. Şifreler Windows hesabınıza bağlı DPAPI ile şifrelenmiş JSON olarak tutulur, düz metin değildir.
- Ekran görüntüsü: ön plandaki pencereyi (ya da tüm ekranı) yakalayıp hazneye ekler; oradan sunucuya yükleyip link ve QR ile paylaşılabilir.
- Kısayol çubuğu: kurulu uygulamalardan seçilen simgeler, sürükle-bırak ile düzenleme.
- Claude'a sor: kapsülün içinde soru, seçili metni özetle / çevir, panoyu özetle. Yanıtı masaüstüne profesyonel .md ya da yazdırılabilir .html olarak kaydetme. "not: ..." ile masaüstü not dosyasına, "hatırlat: 14:30 ..." ile zamanlı hatırlatıcı. API anahtarı yoksa makinedeki Claude Code CLI kullanılır.
- Günün özeti: akşam belirlenen saatte uygulama süreleri, mesajlar, müzik ve toplantı süresi; pazar akşamı haftalık rapor.

**Sistem**
- Uzak sunucu sağlığı: ping, HTTP, SSH ile yük / disk / RAM; düşünce ve geri gelince duyuru.
- Tam ekran oyunda gizlenmek yerine ince şerit (saat, CPU, GPU sıcaklığı, RAM, mikrofon/kamera, mesaj rozeti); tarayıcı ve video tam ekranında gizlenir.
- Oyun oturum özeti (süre, GPU tepe sıcaklığı), oyun ses profili (oyun ve Discord kulaklığa, müzik kısık), oyunda son WhatsApp mesajına Ctrl+Alt+Y ile hazır cevap.
- İzleme modu: tam ekran videoda ekran uyumaz, ses değişince kısa şerit, bitince kaldığın yer notu.
- Canlı altyazı → Türkçe: Windows Canlı Altyazı açıkken metni okuyup kapsülde Türkçeye çevirerek gösterir (hesapsız çeviri).
- Arayüz ses efektleri: hazne işlemlerinde (yükle, gönder, temizle) ve fare kapsüle girince/ayrılınca tok, sentezlenmiş sesler.
- Claude Code oturum izci: bir Claude Code oturumu çalışıp durunca (bitti ya da onay bekliyor) kapsül haber verir.
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

`DINAMIKADA_GUNLUK=1` ortam değişkeniyle başlatılınca kapsül `%TEMP%\dinamikada-gunluk.txt` dosyasına günlük yazar ve `%TEMP%\dinamikada-komut.txt` dosyasından komut okur: `genislet`, `daralt`, `bos 1`, `mini 1`, `sor <metin>`, `ozet`, `tepki`, `sapka 1` gibi. Fare ve klavyeye dokunmadan ekran görüntüsüyle doğrulama için tasarlandı.

## Mimari (kısaca)

- `MainWindow.xaml(.cs)`: kapsül, paneller arası geçiş, duyuru kuyruğu.
- `Servisler/`: medya, ses, bildirim, WhatsApp köprüsü istemcisi, hazne, sözler, Spotify, Claude, sunucu, ağ, GPU, düzenler, özet.
- `Kontroller/`: maskot, hazne paneli, ses satırı, uygulama seçici, kayan metin.
- `wa-servis/`: Baileys tabanlı yerel HTTP köprüsü (127.0.0.1:5461).

## Lisans

MIT. Ayrıntı için `LICENSE`.
