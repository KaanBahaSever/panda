// Language switch (English is in the HTML, Turkish lives here) and copy buttons.
// The strings below are our own static text, so setting innerHTML from them is safe.
const tr = {
  "nav.features": "Özellikler",
  "nav.install": "Kurulum",
  "nav.commands": "Komutlar",
  "nav.faq": "SSS",
  "hero.tagline": "<b>TeamSpeak 6</b> için minik, tatlı bir YouTube müzik botu — şirin bir web paneliyle.",
  "chip.ts6": "TeamSpeak 6 ile çalışır",
  "chip.oss": "Açık kaynak",
  "chip.key": "API anahtarı gerekmez",
  "chip.light": "~130 MB RAM",
  "hero.start": "Hemen başla",
  "hero.download": "İndir",
  "features.title": "Neden Panda?",
  "features.lead": "SinusBot TeamSpeak 6 sunucularına giremiyor. Panda girebiliyor — üstelik küçük, sevimli ve ücretsiz.",
  "f1.t": "TeamSpeak 6 için yapıldı",
  "f1.d": "Sunucuyla doğrudan konuşur. Arkada TeamSpeak programı, X sunucusu ya da eklenti çalışmaz. TS3 sunucularında da çalışır.",
  "f2.t": "Şarkı kütüphanesi",
  "f2.d": "İstenen her şarkı kaydedilir. Sevdiklerin anında başlar ve bir daha YouTube'a gitmez; kütüphane dolunca en az dinlenenler yer açar.",
  "f3.t": "Bekleme yok",
  "f3.d": "Sıradaki şarkılar önceden indirilir; bir şarkı biter bitmez diğeri başlar.",
  "f4.t": "Her YouTube linki",
  "f4.d": "Şarkı adı, youtube.com, youtu.be, YouTube Music, Shorts. Çalma listesi linkleri tüm listeyi açmak yerine tek şarkı ekler.",
  "f5.t": "Seveceğin bir panel",
  "f5.d": "Şimdi çalan, sürükle-bırak sıra, arama, kütüphane, geçmiş, ses, tekrar ve ileri sarma. Açık ve koyu tema, Türkçe ve İngilizce, telefonda da çalışır.",
  "f6.t": "Sohbet komutları",
  "f6.d": "<code>!çal</code>, <code>!geç</code>, <code>!sıra</code>… Türkçe ya da İngilizce. Kimlerin kullanacağını TeamSpeak UID'leri veya sunucu gruplarıyla sınırlayabilirsin.",
  "f7.t": "Bot kontrolü mü? Çözüldü.",
  "f7.d": "YouTube \"bot olmadığını doğrula\" derse cookies'i panele yapıştır, devam et.",
  "f8.t": "Tek satırla kurulum",
  "f8.d": "Tek komut Linux'ta her şeyi kurar ve yt-dlp'yi güncel tutar. Docker, Windows ve macOS sürümleri de var.",
  "lib.title": "Şarkıların kayıtlı",
  "lib.p1": "Panda istediğin her şarkıyı, senin belirlediğin boyuta kadar (varsayılan 5 GB) saklar. Kayıtlı şarkılar panelde dinlenme sayılarıyla görünür, hiç beklemeden başlar ve adıyla bulunur: <code>!çal şımarık</code> kayıtlı <i>Şımarık</i>'ı çalar.",
  "lib.p2": "Kütüphane dolunca en az dinlediğin şarkılar önce silinir; en sevdiklerin hep kalır.",
  "install.title": "Tek satırla kur",
  "install.lead": "Bir Linux sunucuda (Ubuntu, Debian, Fedora, Arch; x64 ya da ARM):",
  "copy": "Kopyala",
  "copied": "Kopyalandı!",
  "install.s1": "Kurulum TeamSpeak sunucu adresini sorar, sonunda panel adresini ve şifresini yazar.",
  "install.s2": "Paneli aç, <b>Ayarlar</b>'dan sunucu şifresini ve kanalı gir.",
  "install.s3": "Botun kanalına <code>!çal</code> ve bir şarkı yaz. Bu kadar! 🎶",
  "install.desktop": "Panda'yı <a href=\"https://github.com/KaanBahaSever/panda/releases/latest\">sürümler sayfasından</a> indir, <a href=\"https://ffmpeg.org/download.html\">ffmpeg</a>, <a href=\"https://github.com/yt-dlp/yt-dlp#installation\">yt-dlp</a> ve <a href=\"https://deno.com/\">deno</a>'yu kur, sonra <code>panda</code>'yı çalıştır.",
  "cmd.title": "Sohbet komutları",
  "cmd.lead": "Botun kanalına ya da bota özel mesaj olarak yaz. İngilizceleri de çalışır.",
  "cmd.play": "Şarkı adı ya da linkini çal veya sıraya ekle",
  "cmd.skip": "Sonraki şarkı",
  "cmd.stop": "Durdur ve sırayı temizle",
  "cmd.pause": "Duraklat (devam için <code>!devam</code>)",
  "cmd.queue": "Sırayı göster",
  "cmd.vol": "Sesi değiştir",
  "cmd.loop": "Bir şarkıyı ya da tüm sırayı tekrarla",
  "cmd.join": "Kanalıma gel",
  "faq.title": "Sorular",
  "faq.q1": "YouTube API anahtarı gerekiyor mu?",
  "faq.a1": "Hayır. Panda, YouTube'u bir tarayıcı gibi okuyan yt-dlp'yi kullanır: API anahtarı, Google projesi ya da günlük kota yok.",
  "faq.q2": "SinusBot neden TeamSpeak 6 sunucuma giremiyor?",
  "faq.a2": "TeamSpeak 6 sunucuları TeamSpeak 3 istemcilerini ancak 3.6.0 sürümünden itibaren kabul ediyor; SinusBot ise daha eski bir sürüm kullanıyor. Panda protokolü kendisi konuşur ve TeamSpeak 6'nın bağlanırken gönderdiği yeni lisans verisini anlar.",
  "faq.q3": "YouTube \"bot olmadığını doğrula\" diyor",
  "faq.a3": "Bu bazı sunucu IP'lerinde olur. Gizli pencerede YouTube'a giriş yap, cookies'i bir cookies.txt eklentisiyle dışa aktar, pencereyi kapat ve Ayarlar → YouTube cookies kısmına yapıştır.",
  "faq.q4": "Ne kadar kaynak ister?",
  "faq.a4": "Çok az: bot yaklaşık 130 MB RAM kullanır, yt-dlp ve ffmpeg sadece şarkı çalarken çalışır. TeamSpeak sunucunun yanındaki küçük bir VPS bol bol yeter.",
  "faq.q5": "Ücretsiz mi?",
  "faq.a5": "Evet, Panda Open Software License 3.0 ile açık kaynak. Fikirler, hata bildirimleri ve pull request'ler GitHub'da memnuniyetle karşılanır.",
  "foot.made": "🎧 ve bambuyla yapıldı.",
  "foot.note": "Panda'nın TeamSpeak veya YouTube ile bir bağlantısı yoktur.",
};

const en = {};
document.querySelectorAll("[data-i18n]").forEach(el => { en[el.dataset.i18n] = el.innerHTML; });
en.copied = "Copied!";

let saved = null;
try { saved = localStorage.getItem("panda-lang"); } catch { }
let lang = saved || ((navigator.language || "").toLowerCase().startsWith("tr") ? "tr" : "en");

function apply() {
  const dict = lang === "tr" ? tr : en;
  document.documentElement.lang = lang;
  document.querySelectorAll("[data-i18n]").forEach(el => {
    const text = dict[el.dataset.i18n];
    if (text !== undefined) el.innerHTML = text;
  });
  document.getElementById("lang").textContent = lang === "tr" ? "EN" : "TR";
  document.title = lang === "tr"
    ? "Panda 🐼 — TeamSpeak 6 için tatlı bir YouTube müzik botu"
    : "Panda 🐼 — a cute YouTube music bot for TeamSpeak 6";
}

document.getElementById("lang").addEventListener("click", () => {
  lang = lang === "tr" ? "en" : "tr";
  try { localStorage.setItem("panda-lang", lang); } catch { }
  apply();
});

document.querySelectorAll(".copy").forEach(btn => btn.addEventListener("click", async () => {
  const text = document.getElementById(btn.dataset.copy).textContent;
  try { await navigator.clipboard.writeText(text); } catch { return; }
  btn.textContent = (lang === "tr" ? tr : en).copied;
  btn.classList.add("done");
  setTimeout(() => { btn.textContent = (lang === "tr" ? tr : en).copy; btn.classList.remove("done"); }, 1600);
}));

apply();
