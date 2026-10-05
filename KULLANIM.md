# Dinamik Ada — Kullanım Kılavuzu

Windows 11 için, ekranın üst ortasında duran, macOS Dynamic Island benzeri bir kapsül. Fare uzaktayken küçük bir hap gibi durur; üstüne gelince çalan müziğe, gelen mesaja, haznedeki dosyalara ya da saat ve sistem bilgisine göre genişler.

---

## 1. Temel kullanım

- **Kapsül nerede:** Ekranın üst ortasında. Ayarlardan ekranı, üstten boşluğu, ölçeği ve saydamlığı değiştirebilirsiniz.
- **Fareyi üstüne getirin:** Kapsül genişler. Ne gösterdiği o anki duruma bağlıdır: müzik çalıyorsa medya, okunmamış mesaj varsa mesaj, hazneye dosya bıraktıysanız hazne, yoksa saat ve sistem paneli.
- **Fareyi çekin:** Kapsül küçülür. Boştayken küçük halde maskot ve saat görünür.
- **Tepsi simgesi (sağ altta):** Sağ tık → **Ayarlar** (tüm seçenekler) veya **Çıkış**.
- **Windows ile başlat:** Ayarlardan açıksa her açılışta kendiliğinden gelir.

---

## 2. Medya (müzik ve video)

Spotify, tarayıcı, Media Player gibi ses çalan her uygulamayı tanır.

- **Fareyi kapsüle getirin:** Kapak, şarkı adı, sanatçı, ilerleme çubuğu ve kontroller çıkar.
- **Oynat / duraklat / ileri / geri:** Kapağın sağ alt köşesindeki hap şeklindeki düğmeler.
- **İlerleme çubuğuna tıklayın:** Şarkının o anına atlar.
- **Beğen (kalp, sağ üstte):** Spotify çalarken parçayı beğenir. (Not: Spotify Web API Premium gerektirir; Premium yoksa Spotify masaüstü uygulamasının kısayoluyla beğenir, pencere bir an öne gelir.)
- **Ses aygıtı düğmesi (kalbin yanında):** Çalan uygulamanın sesini hangi hoparlörden çıkacağını ve seviyesini seçmek için ses karışımını açar.
- **Şarkı sözleri:** lrclib.net'ten otomatik gelir. Geniş panelde ilerleme çubuğunun altında, kompakt görünümde ise şarkı adının yerinde satır satır akar. Hesap gerekmez. Ayarların Müzik bölümünden kapatılabilir.

---

## 3. Ses karışımı (uygulama başına ses ve monitör yönlendirme)

İki monitörünüz varsa her uygulamanın sesini ayrı hoparlöre yönlendirebilir, seviyesini ayrı ayarlayabilirsiniz.

- **Açmak için:** Boş panelde ses çubuğunun sağındaki küçük düğme, ya da medya panelinde kalbin yanındaki düğme.
- **Her satır bir uygulama:** Simge, ad, aygıt düğmesi, sessiz düğmesi ve ses kaydırıcısı.
- **Aygıt adına tıklayın:** Açılan menüden hoparlör seçersiniz; "Sistem varsayılanını izle" ile özel atama kaldırılır.
- **Kaydırıcı:** Yalnız o uygulamanın sesini değiştirir.
- Windows'un "Uygulama ses ve cihaz tercihleri" sayfasına gitmeden yapılır.
- **Tüm aygıtlarda çal:** Panelin üstündeki "Tüm aygıtlarda çal" düğmesine basınca varsayılan aygıttaki ses, bağlı diğer tüm çıkış aygıtlarından (örneğin A24, A27 V2 ve HS 420) aynı anda çıkar. Bir odada hepsi birden çalar; farklı odalardaki hoparlörleri aynı şarkıyla doldurmak için idealdir. Küçük bir gecikme olabilir. Bir kez açtığınızda kapsül yeniden başladığında da açık kalır; aynı düğmeyle kapatırsınız.

---

## 3.1 Boş panel araç düğmeleri (ses çubuğunun sağında)

Boş panelde ses çubuğunun sağında dört küçük düğme vardır: ekran görüntüsü, pano geçmişi, geliştirici araçları ve ses karışımı.

- **Ekran görüntüsü (kamera):** Ön plandaki pencereyi yakalar (pencere küçükse tüm ekranı). Kapsül görüntüye girmez. PNG `Resimler\DinamikAda` altına kaydedilir ve hazneye eklenir. Haznedeki görüntüyü "Link" düğmesiyle sunucuya yükleyip bağlantısını alır, o bağlantıyı QR ile telefona okutabilirsiniz.
- **Pano geçmişi:** Kopyaladığınız son metinler burada listelenir (oturum içi, en çok 30). Bir satıra tıklayınca panoya geri kopyalar. Her satırda kopyala, QR ve sil düğmeleri vardır. QR ile bir linki ya da metni telefonunuzda anında açarsınız. Ayarlardan kapatılabilir.
- **Şifreler (anahtar simgesi):** Uzunluğu seçin (8-40), Rakam/Simge/Karışanları ele düğmeleriyle içeriği ayarlayın ve "Üret"e basın. Üretilen şifreyi kopyalar ya da QR ile telefona gönderirsiniz. Alt alana bir ad (örneğin GitHub) yazıp "Kaydet" ile saklarsınız. Saklanan şifreler listede ad ve maskeli biçimde durur; satırdaki göz düğmesi gösterir/gizler, diğerleri kopyalar, QR yapar ve siler. Şifreler Windows hesabınıza bağlı şifreli JSON (`%AppData%\DinamikAda\sifreler.json`) olarak saklanır, dosyada düz metin görünmez.
- **QR:** Pano ve araçlar panellerindeki QR düğmeleri "Telefonla oku" panelini açar; büyük QR kodunu telefon kamerasıyla okutursunuz.

---

## 4. Mesajlar ve WhatsApp

- **Windows bildirimleri:** Kapsüle düşer. Fare değmemişken son mesaj kısmen görünür; üstüne gelince tam içerik; tıklayınca uygulamada açılır.
- **WhatsApp köprüsü:** Mesajlar pencere açılmadan arka planda gelir ve gider. İlk seferde kapsülde çıkan QR kodunu telefondan okutun (WhatsApp > Bağlı cihazlar > Cihaz bağla). Bir kez yeter.
- **Cevap yazma:** Gelen WhatsApp mesajının üzerine gelin, cevap kutusuna yazıp gönderin. Pencere açılmaz, arka planda gider.
- **Rehber:** Kişi adlarını numaraya çevirmek için telefondan dışa aktardığınız `.vcf` dosyasını kapsülün üstüne bırakın. Elle de girebilirsiniz (Ayarlar > WhatsApp).
- **Arşivlenmiş sohbetler:** Kapsüle düşmez, sessizdir.
- **Toplantı modu:** Mikrofon açılınca bildirimler susar, mikrofon kapanınca özet verilir.

---

## 5. Dosya haznesi

Kapsül, dosyalarınızı geçici tuttuğunuz bir raf gibidir.

- **Dosya eklemek:** Dosyayı kapsülün üstüne sürükleyip bırakın, ya da bir şey kopyalayıp kapsül üstündeyken **Ctrl+V** yapın.
- **Hazneyi açmak:** Kompakt kapsülde ataç simgesine tıklayın.
- **Görünüm:** Sağ üstteki düğmeyle liste ↔ ızgara geçişi. Listede dosyanın üzerine gelince sağda beş düğme çıkar; ızgarada ve yan sütunda toplu işlem düğmeleri vardır.
- **Düğmeler (tek dosya için hover, ya da toplu):**
  - **Claude:** Metin dosyalarını Claude'a özetletir.
  - **Telefona:** WhatsApp köprüsüyle kendi numaranıza gönderir ("Siz" sohbetinde görünür).
  - **Zip:** Hepsini masaüstünde tek zip'e toplar.
  - **Link:** Herkese açık klasöre yükler, bağlantıları panoya kopyalar.
  - **Yükle:** Ayarladığınız sunucuya gönderir (scp).
  - **Kaldır / Temizle:** Hazneden çıkarır (dosyalar diskte kalır).
  - **Panodan ekle:** Panodaki dosya, görsel ya da metni hazneye koyar.
- **Dışarı sürükleme:** Haznedeki dosyayı başka pencereye sürükleyin. Ctrl basılıyken kopyalar, Alt basılıyken kısayol oluşturur (Ayarlardan davranışı sabitleyebilirsiniz).
- **Telefondan bilgisayara:** WhatsApp'ta kendinize ("Siz") attığınız dosya ve bağlantılar kendiliğinden hazneye düşer.
- **İndirme izleme:** İndirilenler klasörüne biten her dosya kapsülde duyurulur ve hazneye alınır; oradan sürükleyip taşıyabilirsiniz. (Ayarlar > Modüller)

---

## 6. Boş panel (saat, sistem, kısayollar)

Müzik çalmıyor ve mesaj yoksa, fareyi getirince bu panel açılır.

- **Üstte:** Maskot, büyük saat ve tarih.
- **Sağda halkalar:** CPU, RAM ve (ayarladıysanız) sunucu (SRV) doluluğu.
- **Ağ satırı:** İndirme/yükleme hızı ve Tailscale VPN durumu (yeşil bağlı, turuncu bağlanıyor, gri kapalı).
- **Ses çubuğu:** O anki sistem sesi. Yanındaki düğme ses karışımını açar.
- **Kısayol çubuğu:** Seçtiğiniz uygulamaların simgeleri. Tıklayınca açılır. Sağ tıkla sıralama/kaldırma, "+" ile kurulu uygulamalardan ekleme.

---

## 7. Claude'a sor

Kapsülün içinde Claude ile konuşabilirsiniz. API anahtarınız yoksa makinenizdeki Claude Code kullanılır (Ayarlar > Claude).

- **Açmak:** Kısayol çubuğundaki turuncu sohbet simgesi.
- **Soru:** Kutuya yazıp Enter'a basın.
- **Hızlı eylemler:** Başka pencerede seçili metni özetle/çevir, panoyu özetle.
- **Yanıtı kaydet:** Alttaki **Kaydet** düğmesi; masaüstüne profesyonel `.md` ve A4 yazdırmaya uygun `.html` üretir (Ctrl+P ile PDF yapılır).
- **Not almak:** Kutuya `not: içerik` yazın; masaüstündeki "Dinamik Ada Notlar.md" dosyasına eklenir.
- **Hatırlatıcı kurmak:** Kutuya `hatırlat: 14:30 toplantı` ya da `hatırlat: 15 dk sonra çay` ya da `hatırlat: yarın 09:00 ...` yazın. Zamanı gelince kapsül uyarır.
- **Günün özeti / haftalık rapor:** Sohbet simgesine sağ tıklayın. Günü ya da son 7 günü özetler (uygulama süreleri, mesajlar, müzik, oyun). Günün özeti akşam ayarladığınız saatte, haftalık rapor pazar akşamı kendiliğinden gelir.

---

## 8. Oyun ve film

- **Oyun şeridi:** Tam ekran oyun açıkken kapsül gizlenmek yerine ince bir şerit olur: saat, CPU, GPU sıcaklığı, RAM, mikrofon/kamera durumu ve okunmamış mesaj sayısı.
- **Oyunda hızlı cevap:** Oyundayken gelen son WhatsApp mesajına **Ctrl+Alt+Y** ile hazır cevabınızı gönderir (metni Ayarlar > Oyun ve izleme'den belirleyin).
- **Oyun ses profili:** Oyuna girince oyun ve Discord sesini kulaklığa yönlendirir, müziği kısar; çıkınca eski haline döner. (Ayarlardan açın, kulaklık adını ve müzik düzeyini belirleyin.)
- **Oyun oturum özeti:** Oyundan çıkınca süre ve GPU tepe sıcaklığı duyurulur, günün özetine işlenir.
- **İzleme modu:** Tam ekran video oynatırken ekran uyumaz, bildirimler susar, ses değişince kısa şerit çıkar, bitince "kaldığın yer" notu gelir. (Ayarlar > Oyun ve izleme)

---

## 9. Canlı altyazı → Türkçe

İngilizce bir videoyu izlerken konuşmayı Türkçe altyazıyla takip edebilirsiniz.

- **Windows Canlı Altyazı'yı açın:** `Win + Ctrl + L`. İlk açılışta "Evet, devam et" deyin.
- İngilizce ses/video oynatın.
- Kapsül altyazı metnini okur, Türkçeye çevirir ve kendi panelinde Türkçe (büyük) ile İngilizce (küçük) gösterir.
- Ayarların Modüller bölümünden açılıp kapatılır. (Not: ücretsiz çeviri servisinin günlük bir sınırı vardır; çok uzun kullanımda çeviri bir süre durabilir.)

---

## 10. Sistem ve maskot

- **Sunucu izleme:** Ayarlara sunucu adresi girerseniz ping, web ve SSH ile yük/disk/RAM ölçülür; sunucu düşünce ve geri gelince kapsül haber verir. (Ayarlar > Sunucu)
- **Pil, pomodoro:** Pil olayları bildirilir; pomodoro sayacı Ayarlardan.
- **Claude Code oturum izci:** Bir Claude Code oturumu çalışıp durunca (işi bitti ya da onayınızı bekliyor) kapsül "Claude Code bekliyor" diye haber verir. Başka pencerede olsanız da kaçırmazsınız. (Ayarlar > Modüller)
- **Maskot (piksel yaratık):** Ön plandaki uygulamaya göre kod yazar, oyun oynar, müzik dinler; uzun süre dokunmazsanız uyur. Üstüne tıklayınca zıplar. Uzun aralıksız çalışınca mola hatırlatır, gece uykulu bakar, yılbaşı/bayram/doğum gününde şapka takar (doğum gününüzü Ayarlar > Canavar'a yazın).

---

## 11. Ses efektleri

Hazne işlemlerinde (yükle, gönder, temizle, dosya bırak) ve fare kapsüle girip çıkınca tok, yumuşak sesler çıkar. Ayarların Modüller bölümünden açıp kapatabilir, düzeyini ayarlayabilirsiniz.

---

## 12. Ayarlar

Tepsi simgesine sağ tıklayıp **Ayarlar**. Sol menüden bölüm seçin:

- **Genel:** Ekran, üstten boşluk, ölçek, saydamlık, tam ekranda gizlenme, Windows ile başlat, duyuru süresi, günün özeti saati.
- **Modüller:** Hangi özellikler açık (ses, pil, bildirim, toplantı modu, ağ, Windows ses barını gizle, indirme izleme, telefondan hazneye, ses efektleri, canlı altyazı, Claude Code izci), hava için şehir, pomodoro.
- **Müzik:** Şarkı sözleri, Spotify hesabı (Client ID ile bağlanma).
- **Claude:** API anahtarı (boşsa Claude Code), model, kalıcı not.
- **Hazne:** Bırakınca davranış, görünüm, dışarı sürükleme, paylaşım linki, sunucuya kopyalama bilgileri.
- **Kısayollar:** Boş paneldeki uygulama çubuğu.
- **WhatsApp:** Arka plan köprüsü, rehber (.vcf).
- **Sunucu:** İzlenecek sunucu adresi, SSH kullanıcısı/anahtarı, URL'ler.
- **Canavar:** Maskot, uyku süresi, mola hatırlatma, doğum günü.
- **Oyun ve izleme:** Hazır cevap, oyun ses profili, izleme modu.

---

## 13. Kurulum (geliştiriciler için)

```
git clone https://github.com/ridocs/windows-dinamik-adasi.git
cd windows-dinamik-adasi
dotnet build -c Release
.\bin\Release\net8.0-windows10.0.19041.0\DinamikAda.exe
```

WhatsApp köprüsü için `wa-servis` klasöründe `npm install`. Kapsül köprüyü kendisi başlatır; QR'ı bir kez okutun.

Ayarlar `%AppData%\DinamikAda\ayarlar.json` dosyasında tutulur.
