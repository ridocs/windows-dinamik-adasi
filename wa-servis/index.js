// Dinamik Ada WhatsApp köprüsü (Baileys: tarayıcısız, WhatsApp çoklu cihaz protokolü).
// 127.0.0.1:PORT üzerinden kapsülle konuşur:
//   GET  /durum            -> { hazir, qrVar, ben, bekleyen, durum }
//   GET  /qr               -> PNG (bağlı değilken)
//   POST /gonder           -> { numara?, ad?, metin }                -> { ok, kime }
//   POST /gonder-dosya     -> { numara?, ad?, yol, metin? }          -> { ok, kime }
//   GET  /gelen            -> son gelen mesajlar (okununca kuyruktan düşer)
//   GET  /kisi?ad=         -> { numara }
// Oturum ./oturum-baileys içinde kalır (bir kez QR).

const http = require("http");
const path = require("path");
const fs = require("fs");
const os = require("os");
const QRCode = require("qrcode");
const pino = require("pino");
const baileys = require("@whiskeysockets/baileys");
const makeWASocket = baileys.default || baileys.makeWASocket;
const { useMultiFileAuthState, DisconnectReason, fetchLatestBaileysVersion, Browsers, downloadMediaMessage } = baileys;

const PORT = Number(process.env.WA_PORT || 5461);
const OTURUM = path.join(__dirname, "oturum-baileys");

let sock = null;
let hazir = false;
let sonQr = null;
let ben = "";
let durum = "kapali";
let baslatiliyor = false;
const gelenKuyruk = [];
const MAX_KUYRUK = 50;
const kisiler = new Map();   // numara -> { ad, sade }
const lidEsle = new Map();   // lid kullanıcı kimliği -> numara

function log(...a) { console.log(new Date().toISOString().slice(11, 19), ...a); }
process.on("unhandledRejection", e => log("yakalanmayan (promise):", e && e.message ? e.message : e));
process.on("uncaughtException", e => log("yakalanmayan (istisna):", e && e.message ? e.message : e));

function sade(s) {
  return String(s || "").normalize("NFD").replace(/[̀-ͯ]/g, "")
    .toLowerCase().replace(/[^\p{L}\p{N}\s]/gu, "").replace(/\s+/g, " ").trim();
}
function numaraMi(jid) { return typeof jid === "string" && jid.endsWith("@s.whatsapp.net"); }
function jidNumara(jid) { return String(jid || "").split("@")[0].split(":")[0]; }
function kisiKaydet(numara, ad) {
  if (!numara || !ad || numara.length < 8) return;
  const eski = kisiler.get(numara);
  if (!eski || (ad.length > 0 && eski.ad !== ad && !eski.rehber)) { kisiler.set(numara, { ad, sade: sade(ad), rehber: eski?.rehber || false }); kaliciKaydet(); }
}

// Mesajdan telefon numarasını çıkar (LID ise alternatif alanlardan). Eşlenemeyen LID numara DEĞİLDİR: boş döner.
function gonderenNumara(m) {
  const k = m.key || {};
  const adaylar = [k.participantAlt, k.remoteJidAlt, k.senderPn, k.participant, k.remoteJid];
  for (const a of adaylar) if (numaraMi(a)) return jidNumara(a);
  for (const a of [k.participant, k.remoteJid]) {
    if (typeof a === "string" && a.endsWith("@lid")) {
      const n = lidEsle.get(jidNumara(a)); if (n) return n;
      try { const pn = sock?.signalRepository?.lidMapping?.getPNForLID?.(a); if (typeof pn === "string" && numaraMi(pn)) { lidEsle.set(jidNumara(a), jidNumara(pn)); return jidNumara(pn); } } catch {}
    }
  }
  return "";
}
// Cevap için gerçek hedef: gruptaysa katılımcı, değilse sohbet (LID de olabilir; Baileys LID'e gönderebilir)
function gonderenJid(m) {
  const k = m.key || {};
  return k.participant || k.remoteJid || "";
}

function mesajMetni(m) {
  const x = m.message || {};
  return x.conversation || x.extendedTextMessage?.text || x.imageMessage?.caption || x.videoMessage?.caption
    || (x.imageMessage ? "[fotoğraf]" : x.videoMessage ? "[video]" : x.audioMessage ? "[sesli mesaj]" : x.documentMessage ? `[dosya] ${x.documentMessage.fileName || ""}` : x.stickerMessage ? "[çıkartma]" : x.locationMessage ? "[konum]" : x.contactMessage ? "[kişi kartı]" : "");
}

async function baslat(neden) {
  if (baslatiliyor) return;
  baslatiliyor = true;
  log("başlatılıyor:", neden);
  try {
    const { state, saveCreds } = await useMultiFileAuthState(OTURUM);
    const { version } = await fetchLatestBaileysVersion();
    sock = makeWASocket({
      version,
      auth: state,
      logger: pino({ level: "silent" }),
      browser: Browsers.windows("Dinamik Ada"),
      syncFullHistory: false,
      markOnlineOnConnect: false,
      generateHighQualityLinkPreview: false,
    });
    sock.ev.on("creds.update", saveCreds);
    sock.ev.on("connection.update", u => {
      const { connection, lastDisconnect, qr, isNewLogin, receivedPendingNotifications } = u;
      if (isNewLogin) log("yeni eşleşme algılandı (telefon okuttu)");
      if (receivedPendingNotifications) log("bekleyen bildirimler alındı");
      if (qr) { sonQr = qr; hazir = false; durum = "qr"; log("QR hazır, telefondan okutun"); }
      if (connection === "connecting") durum = "baglaniyor";
      if (connection === "open") {
        hazir = true; sonQr = null; durum = "bagli";
        try { ben = jidNumara(sock.user?.id || ""); } catch {}
        log("hazır:", ben);
        // Arşiv / sessize alma bilgisi uygulama durumunda; yeniden bağlanınca zorla eşitle (chats.update olarak düşer)
        setTimeout(async () => {
          try {
            if (typeof sock.resyncAppState === "function") {
              await sock.resyncAppState(["critical_block", "critical_unblock_low", "regular_high", "regular_low", "regular"], false);
              log(`uygulama durumu eşitlendi: ${arsiv.size} arşivli sohbet`);
            }
          } catch (e) { log("uygulama durumu eşitlenemedi:", e.message); }
        }, 4000);
      }
      if (connection === "close") {
        hazir = false; durum = "kapali";
        const kod = lastDisconnect?.error?.output?.statusCode;
        log("bağlantı kapandı, kod:", kod);
        baslatiliyor = false;
        if (kod === DisconnectReason.loggedOut) {
          log("oturum kapatılmış; oturum klasörü siliniyor, yeni QR gelecek");
          try { fs.rmSync(OTURUM, { recursive: true, force: true }); } catch {}
          setTimeout(() => baslat("çıkış sonrası"), 2000);
        } else setTimeout(() => baslat("kopma sonrası"), 3000);
      }
    });
    // Arşivlenmiş sohbetler: mesajları kapsüle düşürme (chats.* olaylarından archived bayrağı izlenir)
    sock.ev.on("chats.upsert", liste => { for (const c of liste || []) sohbetAl(c); });
    sock.ev.on("chats.update", liste => { for (const c of liste || []) sohbetAl(c); });
    sock.ev.on("contacts.upsert", liste => { for (const k of liste) kisiAl(k); });
    sock.ev.on("contacts.update", liste => { for (const k of liste) kisiAl(k); });
    sock.ev.on("messaging-history.set", ({ contacts, chats }) => { for (const k of contacts || []) kisiAl(k); for (const c of chats || []) sohbetAl(c); });
    sock.ev.on("lid-mapping.update", m => { try { for (const [lid, pn] of Object.entries(m || {})) lidEsle.set(jidNumara(lid), jidNumara(pn)); } catch {} });
    sock.ev.on("messages.upsert", ({ messages, type }) => {
      if (type !== "notify") return;
      for (const m of messages) {
        try {
          if (!m.message) continue;
          if (m.key.fromMe) { kendineIsle(m); continue; }
          const jid = m.key.remoteJid || "";
          if (jid === "status@broadcast" || jid.endsWith("@newsletter")) continue;
          if (arsiv.has(jid)) continue;   // arşivlenmiş sohbet: sessiz
          const ts = Number(m.messageTimestamp || 0);
          if (ts && Date.now() / 1000 - ts > 120) continue;   // geçmiş eşlemesi artığı
          const grup = jid.endsWith("@g.us");
          const numara = gonderenNumara(m);
          const ad = m.pushName || kisiler.get(numara)?.ad || numara || "Bilinmeyen";
          kisiKaydet(numara, m.pushName || "");
          gelenKuyruk.push({ id: m.key.id || String(Date.now()), numara, jid: gonderenJid(m), ad, grup: grup ? (grupAdi(jid)) : "", metin: mesajMetni(m), zaman: (ts || Math.floor(Date.now() / 1000)) * 1000 });
          while (gelenKuyruk.length > MAX_KUYRUK) gelenKuyruk.shift();
        } catch (e) { log("gelen mesaj işlenemedi:", e.message); }
      }
    });
    baslatiliyor = false;
  } catch (e) {
    log("başlatma hatası:", e.message);
    baslatiliyor = false;
    setTimeout(() => baslat("yeniden deneme"), 8000);
  }
}

// Arşivlenmiş sohbetler ve kişiler diske yazılır: yeniden başlatınca WhatsApp geçmişi tekrar göndermez
const KALICI = path.join(__dirname, "kalici.json");
const arsiv = new Set();   // arşivlenmiş sohbet jid'leri
try {
  const k = JSON.parse(fs.readFileSync(KALICI, "utf8"));
  for (const j of k.arsiv || []) arsiv.add(j);
  for (const [n, v] of Object.entries(k.kisiler || {})) kisiler.set(n, v);
  for (const [l, n] of Object.entries(k.lid || {})) lidEsle.set(l, n);
  log(`kalıcı veri: ${arsiv.size} arşiv, ${kisiler.size} kişi`);
} catch {}
let kaliciZaman = null;
function kaliciKaydet() {
  clearTimeout(kaliciZaman);
  kaliciZaman = setTimeout(() => {
    try { fs.writeFileSync(KALICI, JSON.stringify({ arsiv: [...arsiv], kisiler: Object.fromEntries(kisiler), lid: Object.fromEntries(lidEsle) })); } catch (e) { log("kalıcı yazılamadı:", e.message); }
  }, 2000);
}
function sohbetAl(c) {
  try {
    const id = c && c.id; if (!id) return;
    const a = c.archived !== undefined ? c.archived : c.archive;
    if (a === true) { if (!arsiv.has(id)) { log("arşivde:", id); arsiv.add(id); kaliciKaydet(); } }
    else if (a === false && arsiv.has(id)) { arsiv.delete(id); kaliciKaydet(); }
  } catch {}
}

// ---- Telefondan bilgisayara: kendine ("Siz") attığın dosya ve bağlantılar ----
const gonderilen = new Set();          // köprünün kendi gönderdiği mesaj kimlikleri (döngü olmasın)
const telefondanKuyruk = [];
const GELEN_KLASOR = path.join(process.env.APPDATA || os.homedir(), "DinamikAda", "gelen");
function kendiSohbetiMi(jid) {
  if (!jid) return false;
  if (jid === `${ben}@s.whatsapp.net`) return true;
  try { const me = sock?.user; if (me?.lid && jid === `${jidNumara(me.lid)}@lid`) return true; } catch {}
  return false;
}
function uzantiBul(mime, varsayilan) {
  const m = String(mime || "").split(";")[0].trim();
  const tablo = { "image/jpeg": ".jpg", "image/png": ".png", "image/webp": ".webp", "image/gif": ".gif", "video/mp4": ".mp4", "audio/ogg": ".ogg", "audio/mpeg": ".mp3", "audio/mp4": ".m4a", "application/pdf": ".pdf" };
  return tablo[m] || varsayilan;
}
function guvenliAd(ad) { return String(ad || "").replace(/[\\/:*?"<>|]+/g, "_").trim().slice(0, 120) || "dosya"; }
async function kendineIsle(m) {
  try {
    const id = m.key?.id || "";
    if (!id || gonderilen.has(id)) return;                        // köprü gönderdi, tekrar alma
    if (!kendiSohbetiMi(m.key.remoteJid)) return;                 // başka sohbete attığın şey değil, yalnız "Siz"
    const ts = Number(m.messageTimestamp || 0);
    if (ts && Date.now() / 1000 - ts > 120) return;               // geçmiş eşlemesi artığı
    const x = m.message || {};
    const metin = x.conversation || x.extendedTextMessage?.text || x.imageMessage?.caption || x.videoMessage?.caption || x.documentMessage?.caption || "";
    const medya = x.imageMessage ? "image" : x.videoMessage ? "video" : x.documentMessage ? "document" : x.audioMessage ? "audio" : null;
    if (medya) {
      fs.mkdirSync(GELEN_KLASOR, { recursive: true });
      const buf = await downloadMediaMessage(m, "buffer", {}, { logger: pino({ level: "silent" }), reuploadRequest: sock.updateMediaMessage });
      const mm = x.imageMessage || x.videoMessage || x.documentMessage || x.audioMessage;
      const zaman = new Date((ts || Date.now() / 1000) * 1000);
      const damga = zaman.toISOString().slice(0, 19).replace(/[-:T]/g, "").replace(/^(\d{8})(\d{6})$/, "$1-$2");
      let ad = medya === "document" && mm.fileName ? guvenliAd(mm.fileName) : `WhatsApp-${damga}${uzantiBul(mm.mimetype, medya === "image" ? ".jpg" : medya === "video" ? ".mp4" : medya === "audio" ? ".ogg" : ".bin")}`;
      let yol = path.join(GELEN_KLASOR, ad);
      for (let i = 2; fs.existsSync(yol); i++) { const e = path.extname(ad); yol = path.join(GELEN_KLASOR, `${path.basename(ad, e)} (${i})${e}`); }
      fs.writeFileSync(yol, buf);
      telefondanKuyruk.push({ tur: "dosya", yol, ad: path.basename(yol), url: "", metin, zaman: zaman.getTime() });
      log("telefondan dosya:", path.basename(yol));
    }
    const linkler = String(metin).match(/https?:\/\/[^\s<>"')\]]+/g) || [];
    for (const url of linkler) { telefondanKuyruk.push({ tur: "link", yol: "", ad: "", url, metin, zaman: (ts || Math.floor(Date.now() / 1000)) * 1000 }); log("telefondan link:", url); }
    while (telefondanKuyruk.length > 50) telefondanKuyruk.shift();
  } catch (e) { log("kendine mesaj işlenemedi:", e.message); }
}

const grupAdlari = new Map();
function grupAdi(jid) {
  if (grupAdlari.has(jid)) return grupAdlari.get(jid);
  grupAdlari.set(jid, "grup");
  sock?.groupMetadata(jid).then(g => grupAdlari.set(jid, g.subject || "grup")).catch(() => {});
  return "grup";
}

function kisiAl(k) {
  try {
    const id = k.id || "";
    const ad = k.name || k.notify || k.verifiedName || "";
    if (numaraMi(id)) { const n = jidNumara(id); if (ad) { kisiler.set(n, { ad, sade: sade(ad), rehber: !!k.name }); kaliciKaydet(); } }
    else if (id.endsWith("@lid") && k.lid === undefined && ad) {
      const n = lidEsle.get(jidNumara(id)); if (n) { kisiler.set(n, { ad, sade: sade(ad), rehber: !!k.name }); kaliciKaydet(); }
    }
  } catch {}
}

function kisiBul(ad) {
  const hedef = sade(ad);
  if (!hedef) return null;
  for (const [n, k] of kisiler) if (k.sade === hedef) return n;
  const adaylar = [...kisiler].filter(([, k]) => k.sade.length >= 3 && (k.sade.includes(hedef) || hedef.includes(k.sade))).map(([n]) => n);
  return adaylar.length === 1 ? adaylar[0] : null;
}

function json(res, kod, obj) { res.writeHead(kod, { "Content-Type": "application/json; charset=utf-8" }); res.end(JSON.stringify(obj)); }
async function govdeOku(req) { let g = ""; for await (const p of req) g += p; return JSON.parse(g || "{}"); }
function hedefJid(numara, ad, jid) {
  if (typeof jid === "string" && /@(s\.whatsapp\.net|lid|g\.us)$/.test(jid)) return jid;   // gelen mesajın gerçek hedefi
  let h = numara ? String(numara).replace(/\D/g, "") : null;
  if (!h && ad) h = kisiBul(ad);
  return h ? `${h}@s.whatsapp.net` : null;
}

const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, `http://127.0.0.1:${PORT}`);
    if (req.method === "GET" && url.pathname === "/durum") return json(res, 200, { hazir, qrVar: !!sonQr, ben, bekleyen: gelenKuyruk.length, durum, kisi: kisiler.size, arsiv: arsiv.size, telefondan: telefondanKuyruk.length });
    if (req.method === "GET" && url.pathname === "/qr") {
      if (!sonQr) return json(res, 404, { hata: "QR yok" });
      const png = await QRCode.toBuffer(sonQr, { width: 320, margin: 1 });
      res.writeHead(200, { "Content-Type": "image/png" }); return res.end(png);
    }
    if (req.method === "GET" && url.pathname === "/gelen") return json(res, 200, gelenKuyruk.splice(0, gelenKuyruk.length));
    if (req.method === "GET" && url.pathname === "/telefondan") return json(res, 200, telefondanKuyruk.splice(0, telefondanKuyruk.length));
    if (req.method === "GET" && url.pathname === "/kisi") {
      const numara = kisiBul(url.searchParams.get("ad") || "");
      return json(res, numara ? 200 : 404, { numara });
    }
    if (req.method === "POST" && url.pathname === "/gonder") {
      if (!hazir) return json(res, 503, { hata: "WhatsApp bağlı değil" });
      const { numara, ad, metin, jid: istenenJid } = await govdeOku(req);
      if (!metin) return json(res, 400, { hata: "metin boş" });
      const jid = hedefJid(numara, ad, istenenJid);
      if (!jid) return json(res, 404, { hata: "kişi bulunamadı" });
      const g1 = await sock.sendMessage(jid, { text: metin }); if (g1 && g1.key && g1.key.id) gonderilen.add(g1.key.id);
      log("gönderildi ->", jidNumara(jid));
      return json(res, 200, { ok: true, kime: jidNumara(jid) });
    }
    if (req.method === "POST" && url.pathname === "/gonder-dosya") {
      if (!hazir) return json(res, 503, { hata: "WhatsApp bağlı değil" });
      const { numara, ad, yol, metin } = await govdeOku(req);
      if (!yol || !fs.existsSync(yol)) return json(res, 400, { hata: "dosya yok" });
      const jid = hedefJid(numara || (ad ? null : ben), ad);
      if (!jid) return json(res, 404, { hata: "kişi bulunamadı" });
      const adDosya = path.basename(yol);
      const uz = path.extname(yol).toLowerCase();
      const resim = [".png", ".jpg", ".jpeg", ".webp"].includes(uz);
      const icerik = resim
        ? { image: fs.readFileSync(yol), caption: metin || "" }
        : { document: fs.readFileSync(yol), fileName: adDosya, mimetype: "application/octet-stream", caption: metin || "" };
      const g2 = await sock.sendMessage(jid, icerik); if (g2 && g2.key && g2.key.id) gonderilen.add(g2.key.id);
      log("dosya gönderildi ->", jidNumara(jid), adDosya);
      return json(res, 200, { ok: true, kime: jidNumara(jid) });
    }
    json(res, 404, { hata: "yol yok" });
  } catch (e) { log("istek hatası:", e.message); json(res, 500, { hata: e.message }); }
});

server.listen(PORT, "127.0.0.1", () => log(`dinliyor http://127.0.0.1:${PORT} (baileys)`));
baslat("açılış");
