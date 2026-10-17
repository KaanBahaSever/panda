<p align="center">
  <img src="assets/logo.svg" width="160" alt="Panda logosu">
</p>

<h1 align="center">Panda 🐼</h1>

<p align="center">
  <b>TeamSpeak 6</b> için minik, tatlı bir YouTube müzik botu — şirin bir web paneliyle.<br>
  <a href="https://kaanbahasever.github.io/panda/">Web sitesi</a> ·
  <a href="README.md">English</a>
</p>

---

Panda TeamSpeak sunucuna girer ve YouTube'dan müzik çalar. Sohbete `!çal` ve şarkı adını ya da linkini
yazman yeterli; istersen web panelini kullan. Eklenti yok, script yok, arkada çalışan bir TeamSpeak
programı yok: Panda sunucuyla doğrudan konuşur, bu yüzden hafiftir (bot ~130 MB RAM kullanır,
`yt-dlp` ve `ffmpeg` sadece şarkı çalarken çalışır).

![Panda'nın web paneli şarkı çalarken](docs/now-playing.png)

## Özellikler

- 🎵 **Sadece YouTube, ama hakkıyla** — adıyla ara ya da link yapıştır: `youtube.com`, `youtu.be`,
  **YouTube Music**, Shorts. Çalma listesi ve albüm linkleri tüm listeyi açmak yerine tek şarkı ekler
  (linkteki şarkıyı ya da listenin ilk şarkısını).
- 📚 **Şarkı kütüphanesi** — istenen her şarkı diske kaydedilir. Tekrar çalınan şarkılar anında başlar
  ve YouTube'a hiç gitmez; kütüphane dolunca en az dinlenen şarkılar yer açar.
- ⚡ **Şarkılar arası bekleme yok** — sıradaki şarkılar önceden indirilir.
- 📻 **Radyo** — sıra bitince Panda son şarkıya benzer şarkılar çalmaya devam eder (YouTube Mix).
  Varsayılan olarak kapalı; `!radyo` ya da radyo düğmesiyle açılır.
- 🔊 **Eşit ses seviyesi** — kaydedilen şarkılar bir kez ölçülür ve hepsi aynı yükseklikte çalar.
- 📃 **Çalma listeleri ve favoriler** — sırayı ya da tek tek şarkıları listelere kaydet, `!liste <ad>`
  ile çal, `!fav` ile çalan şarkıyı favorilere ekle.
- 🤖 **Tek panelden birden fazla bot** — her botun kendi sunucusu, kanalı, sırası ve ayarları olur;
  şarkı kütüphanesi ortaktır.
- 🐼 **TeamSpeak 6 için yapıldı** (TS3 sunucularında da çalışır).
- 🌐 **Web paneli** — şimdi çalan, sürükle-bırak sıra, arama, kütüphane, çalma listeleri, saatli geçmiş,
  istatistikler, ses, tekrar, ileri/geri sarma, açık/koyu tema, Türkçe/İngilizce/Fransızca/Almanca.
- 👥 **Panel kullanıcıları** — yöneticiler her şeyi yapar; "sadece şarkı ekler" kullanıcıları izler ve
  şarkı ekler.
- 🩺 **Sistem ekranı** — işlemci, bellek ve disk, her botun bağlantısı, aranabilir canlı kayıtlar ve
  güncellemeler (Linux kurulumunda tek tık).
- 💬 Türkçe, İngilizce, Fransızca ve Almanca **sohbet komutları**. `!takip` ile bot seni kanaldan kanala takip eder.
- 🍪 **YouTube bot kontrolü mü?** Cookies'i panele yapıştır, bitti.
- 🔁 Kendi kendine yeniden bağlanır, kimliğini korur (yetkiler kalıcı olur), çalan şarkıyı açıklamasında gösterir.
- 🔐 Yüksek kimlik güvenlik seviyesi isteyen sunucularda da çalışır: Panda kendi seviyesini bir kez yükseltir (birkaç saniye ile birkaç dakika arası).
- 📦 Tek dosya, tek komutla kurulum. Linux (x64/ARM), Windows, macOS ve Docker'da çalışır.
- 🔑 **YouTube API anahtarı gerekmez.**

## Şarkı kütüphanesi

İstenen her şarkı kütüphane klasörüne (`/var/lib/panda/library`) kaydedilir; sınırı sen belirlersin
(varsayılan 5 GB). Sınır dolunca Panda en az dinlenen şarkıları siler, böylece en sevdiklerin kalır.
Bir şarkı kütüphanedeyken:

- hemen başlar (YouTube'a hiç istek gitmez),
- `!çal` onu adıyla da bulur — `!çal şımarık` kayıtlı *Şımarık*'ı çalar,
- panelin **Kütüphane** bölümünde görünür; oradan çalabilir, sıraya ekleyebilir ya da silebilirsin.

Kaydetme **Ayarlar → Kütüphane** kısmından kapatılabilir; o zaman Panda sadece sıradaki iki şarkıyı
önceden indirir ve çaldıktan sonra siler.

![Koyu temada kütüphane](docs/library.png)

## İstatistikler ve Sistem ekranı

**İstatistikler** kartı neyin çalındığını gösterir:
- çalma sayısı ve dinlenen süre,
- günlere ve saatlere göre hareketlilik,
- en çok çalınan şarkılar ve en çok isteyen kişiler.

Yöneticilerin bir de **Sistem** ekranı var. Burada şunlar görünür:
- işlemci, bellek ve disk,
- her botun bağlantısı,
- canlı kayıtlar,
- güncellemeler.

![İstatistikler](docs/statistics.png)

![Sistem ekranı](docs/system.png)

## Kurulum (Linux)

```bash
curl -fsSL https://raw.githubusercontent.com/KaanBahaSever/panda/main/scripts/install.sh | sudo bash
```

Kurulum TeamSpeak sunucu adresini ve panel portunu sorar; `ffmpeg`, `yt-dlp` ve `deno`'yu kurar,
bir systemd servisi oluşturur ve sonunda panel adresiyle giriş bilgisini yazar. Güncellemek için
paneldeki **Sistem → Güncellemeler** kısmını kullan ya da aynı komutu tekrar çalıştır. Güncelleme
şarkıları, geçmişi, listeleri ve ayarları silmez; botlar kuyruğa kaldıkları yerden devam eder.

```bash
journalctl -u panda -f                    # loglar
sudo systemctl restart panda              # yeniden başlat
curl -fsSL https://raw.githubusercontent.com/KaanBahaSever/panda/main/scripts/install.sh | sudo bash -s -- --uninstall
```

`yt-dlp` her gün kendini günceller (YouTube sık değişiyor, eski sürümler çalışmaz hale geliyor); panelin
**Sistem** ekranından hemen de güncellenebilir.

### Docker

```bash
git clone https://github.com/KaanBahaSever/panda && cd panda
docker compose up -d
docker compose logs panda                 # ilk açılışta panel giriş bilgisini gösterir
```

Güncellemek için: `git pull && docker compose up -d --build`.

### Windows / macOS

[Releases](https://github.com/KaanBahaSever/panda/releases) sayfasından sistemine uygun arşivi indir,
[ffmpeg](https://ffmpeg.org/download.html), [yt-dlp](https://github.com/yt-dlp/yt-dlp#installation) ve
[deno](https://deno.com/)'yu `PATH`'e kur, sonra `panda`'yı (`panda.exe`) çalıştır. Ayarlar, başlattığın
yerdeki `data` klasöründe tutulur.

## İlk adımlar

1. Paneli aç (`http://sunucun:8080`) ve kurulumun yazdığı şifreyle `admin` olarak giriş yap.
2. **Ayarlar → Bağlantı**: sunucu adresi (`ts.ornek.com` ya da `1.2.3.4:9987`), sunucu şifresi, kanal.
   Kaydedince bot yeniden bağlanır. Sohbet dilini **Ayarlar → Bot** kısmından Türkçe yap.
3. Panel şifresini **Ayarlar**'dan değiştir; istersen arkadaşların için kullanıcı ekle.
4. TeamSpeak'te botun kanalına `!çal tarkan şımarık` yaz. 🎶

![Ayarlar](docs/settings.png)

## Sohbet komutları

Botun kanalına ya da bota özel mesaj olarak yaz. İngilizce karşılıkları da çalışır.

| Komut | İngilizce | Fransızca | Almanca | Ne yapar |
|---|---|---|---|---|
| `!çal <ad veya link>` | `!play` / `!p` | `!jouer` | `!spielen` | Hemen çalar ya da sıraya ekler |
| `!şimdiçal <ad veya link>` | `!playnow` | `!jouermaintenant` | `!jetztspielen` | Sırayı atlayıp hemen çalar |
| `!geç` | `!skip` / `!next` | `!passer` / `!suivant` | `!überspringen` | Sonraki şarkı |
| `!dur` | `!stop` | `!arrêt` | `!stopp` | Durdurur ve sırayı temizler |
| `!duraklat` / `!devam` | `!pause` / `!resume` | `!pause` / `!reprendre` | `!pause` / `!fortsetzen` | Duraklat / devam |
| `!sıra` | `!queue` / `!q` | `!file` | `!warteschlange` | Sırayı gösterir |
| `!şimdi` | `!np` | `!encours` | `!läuft` | Ne çalıyor |
| `!ses <0-100>` | `!vol` | `!volume` | `!lautstärke` | Ses seviyesi |
| `!tekrar <kapalı\|şarkı\|hepsi>` | `!loop <off\|one\|all>` | `!boucle <non\|un\|tout>` | `!wiederholen <aus\|eins\|alle>` | Tekrar |
| `!karıştır` | `!shuffle` | `!mélanger` | `!mischen` | Sırayı karıştırır |
| `!sil <#>` / `!temizle` | `!remove` / `!clear` | `!retirer` / `!vider` | `!entfernen` / `!leeren` | Şarkı siler / sırayı temizler |
| `!gel` | `!join` | `!viens` | `!komm` | Kanalıma gel |
| `!takip` | `!follow` | `!suivre` | `!folgen` | Beni kanaldan kanala takip et (tekrar yazınca bırakır) |
| `!radyo <aç\|kapat>` | `!radio <on\|off>` | `!radio <oui\|non>` | `!radio <an\|aus>` | Sıra bitince benzer şarkılar çalmaya devam et |
| `!liste <ad>` | `!playlist` / `!pl` | `!playlist` | `!playlist` | Kayıtlı bir listeyi sıraya ekle |
| `!listeler` | `!playlists` | `!playlists` | `!playlists` | Kayıtlı listeleri göster |
| `!fav` | `!fav` | `!fav` | `!fav` | Çalan şarkıyı Favoriler'e kaydet |
| `!ara <ad>` | `!search` | `!chercher` | `!suchen` | 5 sonuç gösterir; `!1` … `!5` ile seçilir |
| `!sar <1:30 \| +10 \| -10>` | `!seek` | `!aller` | `!spulen` | Şarkıda ileri geri gider |
| `!önceki` | `!back` | `!précédent` | `!zurück` | Önceki şarkıyı tekrar çalar |
| `!git <#>` | `!skipto` | `!allerà` | `!springen` | Sıradaki bir şarkıya atlar |
| `!taşı <#> <#>` | `!move` | `!déplacer` | `!verschieben` | Sırada şarkının yerini değiştirir |
| `!oyla` | `!voteskip` | `!voter` | `!abstimmen` | Geçmek için oy verir (**Oylamayla geç** açıksa) |
| `!yardım` | `!help` | `!aide` | `!hilfe` | Komut listesi |

Varsayılan olarak herkes botu kullanabilir. Sınırlamak için **Ayarlar → Bot** kısmına TeamSpeak
UID'leri ya da sunucu grubu ID'leri ekle.

Kanal sessiz kalsın istersen **Ayarlar → Bot** kısmında **Her şarkıyı sohbette duyur** (her şarkı
başlarken bir mesaj) ve **Sohbet komutlarını onayla** (“Sıraya eklendi …”, “Geçildi.” gibi kısa cevaplar)
seçeneklerini kapat. `!şimdi` gibi sorular ve hatalar yine cevaplanır.

Yine **Ayarlar → Bot** kısmında: **Kişi başı şarkı** bir kişinin sırada aynı anda kaç şarkısı
bekleyebileceğini sınırlar (panel yöneticileri sınırlanmaz). **Oylamayla geç** açıksa başkasının yazdığı
`!geç` bir oy sayılır; botun kanalındakilerin yarısı oy verince şarkı geçilir. Şarkıyı isteyen kişi her zaman geçebilir.

YouTube bir şarkıyı anlık olarak reddederse (HTTP 403, bot kontrolü, yarıda kopan akış) Panda sessizce
üç kereye kadar yeniden dener ve şarkı kaldığı yerden sürer.

## "Sign in to confirm you're not a bot" hatası

YouTube bazen sunucu IP'lerini engeller. Bir YouTube hesabının cookies'iyle çözülür:

1. **Gizli/özel** bir pencere aç ve YouTube'a giriş yap.
2. Cookies'i bir `cookies.txt` eklentisiyle dışa aktar (örneğin *Get cookies.txt LOCALLY*).
3. Gizli pencereyi hemen kapat (böylece cookies geçerli kalır).
4. Dosyayı **Ayarlar → YouTube cookies** kısmına yapıştır.

Bunun için yan bir hesap kullanmak iyi fikir.

## YouTube API anahtarı gerekiyor mu?

Hayır. Panda arama ve çalmayı [yt-dlp](https://github.com/yt-dlp/yt-dlp) ile yapar; yt-dlp YouTube'u bir
tarayıcı gibi okur. Yani API anahtarı, Google projesi ya da günlük kota yok. YouTube'un isteyebileceği
tek şey yukarıdaki bot kontrolü, onu da cookies çözer.

## Ayarlar dosyası

Her şey panelden değiştirilebilir; dosya `/var/lib/panda/config.json` (ya da `data/config.json`).
Botlar `bots` listesinde, panel kullanıcıları `panel.users` içinde, ses eşitleme `audio` altında durur.
0.3'ten önceki dosyalar (tek bot, tek panel şifresi) açılışta dönüştürülür; bot TeamSpeak kimliğini
korur, eski şifre `admin` kullanıcısının şifresi olur.

Bir panel şifresini sıfırlamak (ya da bütün şifreler unutulduysa yeni bir yönetici açmak) için:
`sudo -u panda /opt/panda/panda --data /var/lib/panda set-password <yeni> [kullanıcı]`.

Panel düz HTTP konuşur. İnternetten erişilebiliyorsa önüne HTTPS'li bir ters vekil sunucu
(Caddy, nginx) ya da güvenlik duvarı koy.

## Derleme

```bash
dotnet build src/Panda                    # .NET 8 SDK gerekir
bash scripts/build-release.sh linux-x64   # dist/ içine tek dosyalık sürüm
```

## Nasıl çalışıyor?

Panda, [TSLib](https://github.com/Splamy/TS3AudioBot) (bir TeamSpeak istemci kütüphanesi) üzerine
kurulmuş bir C# (.NET 8) uygulaması. TeamSpeak 6 sunucuları bağlantı sırasında TSLib'in tanımadığı yeni
bir lisans bloğu gönderiyor; Panda bunu okuyan küçük bir yama içeriyor (bkz. [`lib/`](lib/README.md)).
Ses `yt-dlp` ile gelir, `ffmpeg` ile çözülür ve [Concentus](https://github.com/lostromb/concentus) ile
Opus'a çevrilir.

## Lisans

Panda, üzerine kurulduğu TSLib gibi [Open Software License 3.0](LICENSE) ile lisanslanmıştır.
Panda'nın TeamSpeak veya YouTube ile bir bağlantısı yoktur.

Panda, bir yapay zekâ asistanı olan [Claude](https://claude.ai)'un yardımıyla geliştirildi ve gerçek bir
TeamSpeak 6 sunucusunda test edildi.
