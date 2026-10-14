// Panda 🐼 web panel — plain JS, no build step.
'use strict';

/* ---------------- i18n ---------------- */
const I18N = {
  en: {
    loginHint: 'Your cozy TeamSpeak DJ', password: 'Password', login: 'Log in', language: 'Language: English (switch to Türkçe)',
    theme: 'Toggle dark mode', settings: 'Settings', logout: 'Log out', offline: 'Lost connection to Panda — reconnecting…',
    nowPlaying: 'Now playing', live: 'LIVE', napping: 'Panda is napping 💤 — add a song!', seek: 'Seek',
    shuffle: 'Shuffle queue', stop: 'Stop', play: 'Play', pause: 'Pause', skip: 'Skip', mute: 'Mute', unmute: 'Unmute', volume: 'Volume',
    loopOff: 'Loop: off', loopAll: 'Loop: whole queue', loopOne: 'Loop: this song',
    radioOn: 'Radio: on — similar songs follow when the queue ends', radioOff: 'Radio: off', radioToast: '📻 Radio on: similar songs follow when the queue ends',
    playlists: 'Playlists', plEmpty: 'No saved playlists yet.', plSaveQueue: 'Save the queue as a playlist', plAdd: 'Add to playlist', plNew: 'New playlist…',
    plName: 'Name of the playlist:', plPlay: 'Add to the queue', plShuffle: 'Add shuffled', plRename: 'Rename', plDelete: 'Delete playlist',
    confirmPlDelete: x => `Delete the playlist "${x}"?`, plQueued: (n, x) => `${n} songs from "${x}" added`, plSaved: x => `Saved to "${x}"`,
    plRenamed: 'Playlist renamed', plDeleted: 'Playlist deleted', 'err.bad-playlist-name': 'Playlist names are 1–40 characters with at least one letter or digit, and no slashes.',
    'err.playlist-exists': 'There is already a playlist with that name.',
    addSong: 'Add a song', addPh: 'Song name or YouTube link', add: 'Add', playNow: 'Play now', added: 'Added', results: 'Results',
    noResults: 'No results 🐾', close: 'Close', queue: 'Queue', clear: 'Clear', queueEmpty: 'The queue is empty. Search for something above!',
    history: 'Recently played', historyEmpty: 'Nothing played yet.', today: 'Today', yesterday: 'Yesterday', addAgain: 'Add again', remove: 'Remove', moveUp: 'Move up', moveDown: 'Move down',
    requestedBy: 'requested by', songs: n => n === 1 ? '1 song' : `${n} songs`,
    connected: 'Connected', connecting: 'Connecting…', disconnected: 'Disconnected',
    raisingId: n => `Raising identity security to level ${n}…`, listeners: n => n === 1 ? '1 listener' : `${n} listeners`,
    sConn: 'Connection', sConnHint: 'Changing these makes the bot reconnect.', sAddress: 'Server address', sServerPw: 'Server password',
    sChannel: 'Channel', sChannelHint: 'Path like "Music/Lounge" or an id like "/5". Empty = default channel.', sChannelPw: 'Channel password',
    sNick: 'Nickname', removePw: 'Remove password', unchanged: 'unchanged', sBot: 'Bot', sLang: 'Chat language', sPrefix: 'Command prefix',
    sAnnounce: 'Announce songs in chat', sDesc: 'Show song in bot description', sMaxQueue: 'Max queue length', sMaxDur: 'Max song length (min)',
    sMaxDurHint: '0 = unlimited.', sUsers: 'Allowed users', sUsersHint: 'One TeamSpeak unique ID per line. Empty = everyone.',
    sGroups: 'Allowed server groups', sGroupsHint: 'Comma-separated group ids. Empty = no group restriction.', save: 'Save', reconnect: 'Reconnect bot',
    sCookies: 'YouTube cookies', sCookiesHelp: 'If YouTube says "confirm you\'re not a bot": open a private/incognito window, log in to YouTube, export cookies with a "cookies.txt" browser extension, paste them here, then close that window.',
    chooseFile: 'Choose file', saveCookies: 'Save cookies', removeCookies: 'Remove cookies', cookiesOn: 'Cookies set ✓', cookiesOff: 'No cookies',
    sPanelPw: 'Panel password', pwCurrent: 'Current password', pwNew: 'New password (min 6)', pwChange: 'Change password',
    cancel: 'Cancel', ok: 'OK', saved: 'Settings saved', reconnecting: 'Saved — the bot is reconnecting…', reconnectSent: 'Reconnecting the bot…',
    cookiesSaved: 'Cookies saved 🍪', cookiesRemoved: 'Cookies removed', cookiesEmpty: 'Paste or choose a cookies.txt first', pwChanged: 'Password changed',
    confirmStop: 'Stop playback and clear the queue?', confirmClear: 'Clear the whole queue?', confirmCookies: 'Remove the saved YouTube cookies?',
    nothing: 'Nothing to do right now', invalid: 'Please check the highlighted fields', netError: 'Network error — is Panda running?',
    'err.not-found': 'No song found for that search.', 'err.empty': 'Type a song name or paste a link.',
    'err.playlist-empty': 'That playlist is empty.', 'err.not-youtube': 'Only YouTube links work.',
    'err.too-long': 'That song is too long.', 'err.bot-check': 'YouTube wants a bot check — add cookies in Settings.',
    'err.unavailable': 'That video is unavailable.', 'err.age': 'That video is age-restricted — add cookies in Settings.',
    'err.queue-full': 'The queue is full.', 'err.wrong-password': 'Wrong password.', 'err.too-many-attempts': 'Too many attempts — wait a few minutes.',
    'err.password-too-short': 'The new password needs at least 6 characters.', 'err.cookies-invalid': 'That doesn\'t look like a YouTube cookies.txt.',
    'err.too-large': 'That file is too large.', 'err.missing': 'Missing tool on the server: ',
    'err.timeout': 'YouTube didn\'t answer in time — try again.',
    library: 'Library', savedBadge: 'saved', fromLibrary: 'From your library', fromYouTube: 'From YouTube', libFilter: 'Filter saved songs',
    usage: (a, b) => `${a} of ${b}`, libOff: 'Saving is off — turn it on in Settings.', libEmpty: 'No saved songs yet. Songs are saved as they are requested.',
    libNoMatch: 'No saved songs match.', libCapped: 'Showing the 500 most played songs — use the filter to find others.', showMore: 'Show more',
    plays: n => n === 1 ? '1 play' : `${n} plays`, requests: n => n === 1 ? '1 song' : `${n} songs`, delete: 'Delete', deleted: 'Deleted', confirmLibDelete: x => `Delete "${x}" from the library?`,
    sLib: 'Library', sLibOn: 'Save every requested song', sLibOnHint: 'When off, only the next song is downloaded ahead of time.',
    sLibMax: 'Size limit (GB)', sLibMaxHint: 'When full, the least played songs are deleted first. 0 = no limit.', sLibMaxBad: 'Use 0 (no limit) or at least 0.1 GB.',
    sNorm: 'Play songs equally loud', sNormHint: 'Each saved song is measured once and played at the same loudness. A song\'s very first play, before it is saved, plays as it is.',
    bots: 'Bots', addBot: 'Add a bot', botAdded: 'Bot added — now set its channel', idle: 'Idle',
    sPrefixHint: 'Bots in the same channel need different prefixes, or they all answer.', sShared: 'Shared by all bots.',
    sRemoveBot: 'Remove this bot', sRemoveBotHint: 'Disconnects it and deletes its TeamSpeak identity and play history.', removeBot: 'Remove bot',
    confirmBotRemove: x => `Remove "${x}"? Its identity and play history are deleted.`, botRemoved: 'Bot removed',
    userName: 'User name', role: 'Role', roleAdmin: 'Admin', roleDj: 'Can only add songs', sUsers2: 'Panel users',
    sUsersHint2: 'Admins can do everything. "Can only add songs" users can watch and add songs, nothing else.',
    addUser: 'Add user', userAdded: 'User added', userRemoved: 'User removed', roleChanged: 'Role changed', you: '(you)',
    confirmUserRemove: x => `Remove the user "${x}"?`, resetPw: 'Set a new password', resetPwFor: x => `New password for "${x}" (min 6):`, pwReset: 'Password set',
    forbidden: 'You are not allowed to do that.', 'err.bad-name': 'User names are 2–32 letters, digits, ".", "_" or "-".',
    'err.user-exists': 'That user name is taken.', 'err.bad-role': 'Unknown role.', 'err.last-admin': 'There must be at least one admin.',
    'err.self': 'You can\'t remove yourself.',
    system: 'System', health: 'Status', logs: 'Logs', logLevel: 'Level', lvlAll: 'All', lvlWarn: 'Warnings and errors', lvlError: 'Errors only',
    logSearch: 'Search the log', logFollow: 'Follow', download: 'Download', logNote: 'The latest 2000 lines since Panda started.', logEmpty: 'No log lines match.',
    hPanda: 'Panda', hVersion: 'Version', hUptime: 'Running for', hRuntime: 'Runtime', hOs: 'System', hTs: 'TeamSpeak', hCpu: 'CPU', hRam: 'Memory',
    hDisk: 'Disk', hThis: 'Panda', hMachine: 'Whole server', hFree: 'free', hLib: 'Library',
    hUsed: (a, b) => `${a} of ${b} used`, hLibUsed: a => `${a} on disk`, hLibOff: 'Saving is off', hLimit: 'Limit', hNoLimit: 'none', missingTool: 'not found',
    updates: 'Updates', updCheck: 'Check', updChecked: x => `Last checked: ${x}`, updNotes: 'What\'s new',
    updUnreachable: 'GitHub could not be reached; try again later.', updRelease: 'Release page',
    updInstalled: 'Installed', updNewest: 'Newest', updOk: 'Up to date', updNew: 'New version',
    updPandaWhat: 'The bot itself. Your songs, history, playlists and settings stay; the queue carries on after the restart.',
    updYtWhat: 'The tool that fetches songs from YouTube. YouTube changes often; the server also updates it daily.',
    updNow: 'Update Panda', updStarted: 'Updating… Panda restarts in a minute or two.', updDone: x => `Panda was updated to v${x}. Reloading…`,
    updSlow: 'Panda hasn\'t come back yet; see the logs on the server.',
    updFailed: 'The update didn\'t go through (GitHub may have had a hiccup). Panda keeps running as it was; try again in a minute.',
    'updHow.docker': 'Docker: in the Panda folder run "git pull && docker compose up -d --build".',
    'updHow.systemd-old': 'Run the installer on the server once more (the command in the README); after that you can update from here.',
    'updHow.manual': 'Download the new release from GitHub and replace the program.',
    ytUpdate: 'Update yt-dlp', ytBusy: 'Updating… this can take a minute.', ytDone: (a, b) => `Updated: ${a || '?'} → ${b || '?'}`,
    ytCurrent: x => `Already the newest version (${x || '?'}).`, ytPending: 'The server is still updating it; press Check in a minute.',
    'err.ytdlp-update-failed': 'yt-dlp could not be updated; see the logs.',
    'err.cannot-update': 'This install can\'t update itself.',
    cores: n => n === 1 ? '1 core' : `${n} cores`, uptime: (d, h, m) => d ? `${d}d ${h}h` : h ? `${h}h ${m}m` : `${m}m`,
    stats: 'Statistics', statsRange: 'Period', last7: 'Last 7 days', last30: 'Last 30 days', last365: 'Last year', allTime: 'All time',
    thisBot: 'This bot', allBots: 'All bots', statsEmpty: 'Nothing was played in this period.', sPlays: 'plays', sListened: 'listened',
    sSongs: 'different songs', sPeople: 'people', topSongs: 'Most played', topPeople: 'Who asked the most', activity: 'Activity', byHour: 'By hour of day',
  },
  tr: {
    loginHint: 'Sevimli TeamSpeak DJ\'in', password: 'Şifre', login: 'Giriş yap', language: 'Dil: Türkçe (English\'e geç)',
    theme: 'Karanlık modu aç/kapat', settings: 'Ayarlar', logout: 'Çıkış yap', offline: 'Panda ile bağlantı koptu — yeniden bağlanılıyor…',
    nowPlaying: 'Şimdi çalıyor', live: 'CANLI', napping: 'Panda uyukluyor 💤 — bir şarkı ekle!', seek: 'Sar',
    shuffle: 'Sırayı karıştır', stop: 'Durdur', play: 'Oynat', pause: 'Duraklat', skip: 'Geç', mute: 'Sessize al', unmute: 'Sesi aç', volume: 'Ses',
    loopOff: 'Tekrar: kapalı', loopAll: 'Tekrar: tüm sıra', loopOne: 'Tekrar: bu şarkı',
    radioOn: 'Radyo: açık — sıra bitince benzer şarkılar gelir', radioOff: 'Radyo: kapalı', radioToast: '📻 Radyo açık: sıra bitince benzer şarkılar gelir',
    playlists: 'Çalma listeleri', plEmpty: 'Henüz kayıtlı liste yok.', plSaveQueue: 'Sırayı liste olarak kaydet', plAdd: 'Listeye ekle', plNew: 'Yeni liste…',
    plName: 'Listenin adı:', plPlay: 'Sıraya ekle', plShuffle: 'Karışık ekle', plRename: 'Adını değiştir', plDelete: 'Listeyi sil',
    confirmPlDelete: x => `"${x}" listesi silinsin mi?`, plQueued: (n, x) => `"${x}" listesinden ${n} şarkı eklendi`, plSaved: x => `"${x}" listesine kaydedildi`,
    plRenamed: 'Listenin adı değişti', plDeleted: 'Liste silindi', 'err.bad-playlist-name': 'Liste adı 1–40 karakter olmalı, en az bir harf ya da rakam içermeli ve eğik çizgi içermemeli.',
    'err.playlist-exists': 'Bu adda bir liste zaten var.',
    addSong: 'Şarkı ekle', addPh: 'Şarkı adı ya da YouTube linki', add: 'Ekle', playNow: 'Hemen çal', added: 'Eklendi', results: 'Sonuçlar',
    noResults: 'Sonuç yok 🐾', close: 'Kapat', queue: 'Sıra', clear: 'Temizle', queueEmpty: 'Sıra boş. Yukarıdan bir şey ara!',
    history: 'Son çalınanlar', historyEmpty: 'Henüz bir şey çalınmadı.', today: 'Bugün', yesterday: 'Dün', addAgain: 'Tekrar ekle', remove: 'Kaldır', moveUp: 'Yukarı taşı', moveDown: 'Aşağı taşı',
    requestedBy: 'İsteyen:', songs: n => `${n} şarkı`,
    connected: 'Bağlı', connecting: 'Bağlanıyor…', disconnected: 'Bağlı değil',
    raisingId: n => `Kimlik güvenliği yükseltiliyor (seviye ${n})…`, listeners: n => `${n} dinleyici`,
    sConn: 'Bağlantı', sConnHint: 'Bunları değiştirmek botun yeniden bağlanmasına yol açar.', sAddress: 'Sunucu adresi', sServerPw: 'Sunucu şifresi',
    sChannel: 'Kanal', sChannelHint: '"Müzik/Lounge" gibi bir yol ya da "/5" gibi bir id. Boş = varsayılan kanal.', sChannelPw: 'Kanal şifresi',
    sNick: 'Takma ad', removePw: 'Şifreyi kaldır', unchanged: 'değişmedi', sBot: 'Bot', sLang: 'Sohbet dili', sPrefix: 'Komut öneki',
    sAnnounce: 'Şarkıları sohbette duyur', sDesc: 'Şarkıyı bot açıklamasında göster', sMaxQueue: 'En fazla sıra uzunluğu', sMaxDur: 'En uzun şarkı (dk)',
    sMaxDurHint: '0 = sınırsız.', sUsers: 'İzinli kullanıcılar', sUsersHint: 'Her satıra bir TeamSpeak benzersiz kimliği. Boş = herkes.',
    sGroups: 'İzinli sunucu grupları', sGroupsHint: 'Virgülle ayrılmış grup id\'leri. Boş = grup kısıtlaması yok.', save: 'Kaydet', reconnect: 'Botu yeniden bağla',
    sCookies: 'YouTube çerezleri', sCookiesHelp: 'YouTube "bot olmadığınızı doğrulayın" derse: gizli/özel bir pencere açın, YouTube\'a giriş yapın, bir "cookies.txt" tarayıcı eklentisiyle çerezleri dışa aktarın, buraya yapıştırın ve o pencereyi kapatın.',
    chooseFile: 'Dosya seç', saveCookies: 'Çerezleri kaydet', removeCookies: 'Çerezleri kaldır', cookiesOn: 'Çerezler var ✓', cookiesOff: 'Çerez yok',
    sPanelPw: 'Panel şifresi', pwCurrent: 'Mevcut şifre', pwNew: 'Yeni şifre (en az 6)', pwChange: 'Şifreyi değiştir',
    cancel: 'Vazgeç', ok: 'Tamam', saved: 'Ayarlar kaydedildi', reconnecting: 'Kaydedildi — bot yeniden bağlanıyor…', reconnectSent: 'Bot yeniden bağlanıyor…',
    cookiesSaved: 'Çerezler kaydedildi 🍪', cookiesRemoved: 'Çerezler kaldırıldı', cookiesEmpty: 'Önce bir cookies.txt yapıştırın ya da seçin', pwChanged: 'Şifre değiştirildi',
    confirmStop: 'Çalma durdurulsun ve sıra temizlensin mi?', confirmClear: 'Tüm sıra temizlensin mi?', confirmCookies: 'Kayıtlı YouTube çerezleri kaldırılsın mı?',
    nothing: 'Şu an yapılacak bir şey yok', invalid: 'Lütfen işaretli alanları kontrol edin', netError: 'Ağ hatası — Panda çalışıyor mu?',
    'err.not-found': 'Bu arama için şarkı bulunamadı.', 'err.empty': 'Bir şarkı adı yazın ya da link yapıştırın.',
    'err.playlist-empty': 'Bu çalma listesi boş.', 'err.not-youtube': 'Sadece YouTube linkleri çalışır.',
    'err.too-long': 'Bu şarkı çok uzun.', 'err.bot-check': 'YouTube bot doğrulaması istiyor — Ayarlar\'dan çerez ekleyin.',
    'err.unavailable': 'Bu video kullanılamıyor.', 'err.age': 'Bu video yaş sınırlı — Ayarlar\'dan çerez ekleyin.',
    'err.queue-full': 'Sıra dolu.', 'err.wrong-password': 'Şifre yanlış.', 'err.too-many-attempts': 'Çok fazla deneme — birkaç dakika bekleyin.',
    'err.password-too-short': 'Yeni şifre en az 6 karakter olmalı.', 'err.cookies-invalid': 'Bu bir YouTube cookies.txt dosyasına benzemiyor.',
    'err.too-large': 'Dosya çok büyük.', 'err.missing': 'Sunucuda eksik araç: ',
    'err.timeout': 'YouTube zamanında cevap vermedi — tekrar dene.',
    library: 'Kütüphane', savedBadge: 'kayıtlı', fromLibrary: 'Kütüphanenden', fromYouTube: 'YouTube\'dan', libFilter: 'Kayıtlı şarkılarda ara',
    usage: (a, b) => `${a} / ${b}`, libOff: 'Kaydetme kapalı — Ayarlar\'dan açabilirsin.', libEmpty: 'Henüz kayıtlı şarkı yok. Şarkılar istendikçe kaydedilir.',
    libNoMatch: 'Eşleşen kayıtlı şarkı yok.', libCapped: 'En çok dinlenen 500 şarkı gösteriliyor — diğerleri için aramayı kullan.', showMore: 'Daha fazla göster',
    plays: n => `${n} kez çalındı`, requests: n => `${n} şarkı`, delete: 'Sil', deleted: 'Silindi', confirmLibDelete: x => `"${x}" kütüphaneden silinsin mi?`,
    sLib: 'Kütüphane', sLibOn: 'İstenen her şarkıyı kaydet', sLibOnHint: 'Kapalıyken sadece sıradaki şarkı önceden indirilir.',
    sLibMax: 'Boyut sınırı (GB)', sLibMaxHint: 'Dolduğunda en az dinlenen şarkılar önce silinir. 0 = sınırsız.', sLibMaxBad: '0 (sınırsız) ya da en az 0,1 GB girin.',
    sNorm: 'Şarkıları aynı yükseklikte çal', sNormHint: 'Kaydedilen her şarkının ses seviyesi bir kez ölçülür ve hepsi aynı yükseklikte çalar. Bir şarkı kaydedilmeden önceki ilk çalınışında olduğu gibi çalar.',
    bots: 'Botlar', addBot: 'Bot ekle', botAdded: 'Bot eklendi — şimdi kanalını ayarla', idle: 'Boşta',
    sPrefixHint: 'Aynı kanaldaki botların önekleri farklı olmalı, yoksa hepsi cevap verir.', sShared: 'Tüm botlar için geçerli.',
    sRemoveBot: 'Bu botu kaldır', sRemoveBotHint: 'Bağlantısını keser, TeamSpeak kimliğini ve çalma geçmişini siler.', removeBot: 'Botu kaldır',
    confirmBotRemove: x => `"${x}" kaldırılsın mı? Kimliği ve çalma geçmişi silinir.`, botRemoved: 'Bot kaldırıldı',
    userName: 'Kullanıcı adı', role: 'Rol', roleAdmin: 'Yönetici', roleDj: 'Sadece şarkı ekler', sUsers2: 'Panel kullanıcıları',
    sUsersHint2: 'Yöneticiler her şeyi yapabilir. "Sadece şarkı ekler" kullanıcıları izler ve şarkı ekler, başka bir şey yapamaz.',
    addUser: 'Kullanıcı ekle', userAdded: 'Kullanıcı eklendi', userRemoved: 'Kullanıcı silindi', roleChanged: 'Rol değişti', you: '(sen)',
    confirmUserRemove: x => `"${x}" kullanıcısı silinsin mi?`, resetPw: 'Yeni şifre koy', resetPwFor: x => `"${x}" için yeni şifre (en az 6):`, pwReset: 'Şifre ayarlandı',
    forbidden: 'Bunu yapma iznin yok.', 'err.bad-name': 'Kullanıcı adı 2–32 harf, rakam, ".", "_" ya da "-" olmalı.',
    'err.user-exists': 'Bu kullanıcı adı alınmış.', 'err.bad-role': 'Bilinmeyen rol.', 'err.last-admin': 'En az bir yönetici kalmalı.',
    'err.self': 'Kendini silemezsin.',
    system: 'Sistem', health: 'Durum', logs: 'Kayıtlar', logLevel: 'Seviye', lvlAll: 'Hepsi', lvlWarn: 'Uyarı ve hatalar', lvlError: 'Sadece hatalar',
    logSearch: 'Kayıtlarda ara', logFollow: 'Canlı takip', download: 'İndir', logNote: 'Panda açıldığından beri son 2000 satır.', logEmpty: 'Eşleşen kayıt yok.',
    hPanda: 'Panda', hVersion: 'Sürüm', hUptime: 'Çalışma süresi', hRuntime: 'Çalışma ortamı', hOs: 'Sistem', hTs: 'TeamSpeak', hCpu: 'İşlemci', hRam: 'Bellek',
    hDisk: 'Disk', hThis: 'Panda', hMachine: 'Tüm sunucu', hFree: 'boş', hLib: 'Kütüphane',
    hUsed: (a, b) => `${a} / ${b} dolu`, hLibUsed: a => `diskte ${a}`, hLibOff: 'Kaydetme kapalı', hLimit: 'Sınır', hNoLimit: 'yok', missingTool: 'bulunamadı',
    updates: 'Güncellemeler', updCheck: 'Denetle', updChecked: x => `Son denetim: ${x}`, updNotes: 'Neler yeni',
    updUnreachable: 'GitHub\'a ulaşılamadı; sonra tekrar dene.', updRelease: 'Sürüm sayfası',
    updInstalled: 'Kurulu', updNewest: 'En son', updOk: 'Güncel', updNew: 'Yeni sürüm var',
    updPandaWhat: 'Botun kendisi. Şarkılar, geçmiş, listeler ve ayarlar silinmez; kuyruk yeniden başlayınca kaldığı yerden sürer.',
    updYtWhat: 'Şarkıları YouTube\'dan çeken araç. YouTube sık değiştiği için sunucu bunu her gün kendisi de günceller.',
    updNow: 'Panda\'yı güncelle', updStarted: 'Güncelleniyor… Panda bir iki dakika içinde yeniden başlayacak.', updDone: x => `Panda v${x} sürümüne güncellendi. Sayfa yenileniyor…`,
    updSlow: 'Panda henüz geri gelmedi; sunucudaki kayıtlara bak.',
    updFailed: 'Güncelleme yapılamadı (GitHub anlık bir hata vermiş olabilir). Panda eskisi gibi çalışıyor; bir dakika sonra tekrar dene.',
    'updHow.docker': 'Docker: Panda klasöründe "git pull && docker compose up -d --build" çalıştır.',
    'updHow.systemd-old': 'Sunucuda kurulum komutunu bir kez daha çalıştır (README\'deki komut); sonra buradan güncelleyebilirsin.',
    'updHow.manual': 'Yeni sürümü GitHub\'dan indirip programı değiştir.',
    ytUpdate: 'yt-dlp\'yi güncelle', ytBusy: 'Güncelleniyor… bir dakika sürebilir.', ytDone: (a, b) => `Güncellendi: ${a || '?'} → ${b || '?'}`,
    ytCurrent: x => `Zaten en son sürüm (${x || '?'}).`, ytPending: 'Sunucu hâlâ güncelliyor; bir dakika sonra Denetle\'ye bas.',
    'err.ytdlp-update-failed': 'yt-dlp güncellenemedi; kayıtlara bak.',
    'err.cannot-update': 'Bu kurulum kendini güncelleyemiyor.',
    cores: n => `${n} çekirdek`, uptime: (d, h, m) => d ? `${d} gün ${h} sa` : h ? `${h} sa ${m} dk` : `${m} dk`,
    stats: 'İstatistikler', statsRange: 'Aralık', last7: 'Son 7 gün', last30: 'Son 30 gün', last365: 'Son 1 yıl', allTime: 'Tüm zamanlar',
    thisBot: 'Bu bot', allBots: 'Tüm botlar', statsEmpty: 'Bu aralıkta bir şey çalınmadı.', sPlays: 'çalma', sListened: 'dinlendi',
    sSongs: 'farklı şarkı', sPeople: 'kişi', topSongs: 'En çok çalınanlar', topPeople: 'En çok isteyenler', activity: 'Hareketlilik', byHour: 'Saatlere göre',
  },
};

/* ---------------- helpers ---------------- */
const $ = id => document.getElementById(id);
const store = { get: k => { try { return localStorage.getItem(k); } catch { return null; } },
                set: (k, v) => { try { localStorage.setItem(k, v); } catch {} } };
let lang = store.get('panda.lang') || ((navigator.language || '').toLowerCase().startsWith('tr') ? 'tr' : 'en');
const t = (k, ...a) => { const v = I18N[lang][k] ?? I18N.en[k] ?? k; return typeof v === 'function' ? v(...a) : v; };

/** Only http(s) URLs are allowed into href/src. */
function safeUrl(u) {
  try { const x = new URL(u, location.href); return /^https?:$/.test(x.protocol) ? x.href : null; } catch { return null; }
}
function fmt(sec) {
  sec = Math.max(0, Math.floor(sec || 0));
  const h = Math.floor(sec / 3600), m = Math.floor(sec / 60) % 60, s = String(sec % 60).padStart(2, '0');
  return h ? `${h}:${String(m).padStart(2, '0')}:${s}` : `${m}:${s}`;
}
/** Tiny element builder: el('div', {class: 'x', text: 'y'}, child...). Text always goes through textContent. */
function el(tag, props = {}, ...kids) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (v == null || v === false) continue;
    if (k === 'text') e.textContent = v;
    else if (k === 'class') e.className = v;
    else if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
    else e.setAttribute(k, v === true ? '' : v);
  }
  for (const c of kids) if (c) e.append(c);
  return e;
}
function icon(name, cls = 'ic') {
  const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  s.setAttribute('class', cls); s.setAttribute('aria-hidden', 'true');
  const u = document.createElementNS('http://www.w3.org/2000/svg', 'use');
  u.setAttribute('href', '#i-' + name); s.append(u);
  return s;
}
function setIcon(btn, name) { btn.querySelector('use').setAttribute('href', '#i-' + name); }
function setLabel(btn, text) { btn.setAttribute('aria-label', text); btn.title = text; }
function thumb(track, cls = 'thumb') {
  const img = el('img', { class: cls, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer', width: 96, height: 54 });
  const src = safeUrl(track.thumbnail);
  img.src = src || 'img/avatar.png';
  img.onerror = () => { img.onerror = null; img.src = 'img/avatar.png'; };
  return img;
}
function debounce(fn, ms) { let h; return (...a) => { clearTimeout(h); h = setTimeout(() => fn(...a), ms); }; }

/* ---------------- API ---------------- */
let botId = store.get('panda.bot') || '';
/** URL of an endpoint of the bot shown in the panel. */
const B = path => `/api/bots/${encodeURIComponent(botId)}${path}`;

class ApiError extends Error { constructor(code, status) { super(code); this.code = code; this.status = status; } }

async function api(method, path, body) {
  const opt = { method, headers: {}, credentials: 'same-origin' };
  if (typeof body === 'string') { opt.body = body; opt.headers['Content-Type'] = 'text/plain'; }
  else if (body !== undefined) { opt.body = JSON.stringify(body); opt.headers['Content-Type'] = 'application/json'; }
  let res;
  try { res = await fetch(path, opt); } catch { throw new ApiError('network', 0); }
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (res.status === 401 && path !== '/api/login') { showLogin(); throw new ApiError('unauthorized', 401); }
  if (!res.ok) throw new ApiError((data && data.error) || (typeof data === 'string' && data) || `HTTP ${res.status}`, res.status);
  return data;
}

function errText(e) {
  if (!(e instanceof ApiError)) return String(e && e.message || e);
  if (e.status === 0) return t('netError');
  if (e.status === 403) return t('forbidden');
  if (e.status === 409) return t('nothing');
  if (I18N.en['err.' + e.code]) return t('err.' + e.code);
  if (e.code.startsWith('missing:')) return t('err.missing') + e.code.slice(8);
  return e.code;
}
/** Runs an action; toasts errors (except 401, which shows the login screen). */
async function act(fn, okMsg) {
  try { const r = await fn(); if (okMsg) toast(typeof okMsg === 'function' ? okMsg(r) : okMsg); return r; }
  catch (e) { if (e.status !== 401) toast(errText(e), 'err'); return undefined; }
}
/** Shows a spinner in a button while an async action runs. */
async function busy(btn, fn) {
  if (btn.disabled) return;
  btn.disabled = true; btn.classList.add('busy');
  const sp = el('span', { class: 'spin', 'aria-hidden': 'true' }); btn.prepend(sp);
  try { return await fn(); } finally { sp.remove(); btn.disabled = false; btn.classList.remove('busy'); }
}

/* ---------------- toasts & confirm ---------------- */
function toast(msg, kind = 'ok') {
  const box = el('div', { class: 'toast ' + kind, role: kind === 'err' ? 'alert' : 'status' },
    icon(kind === 'err' ? 'alert' : 'check'), el('span', { text: msg }));
  box.addEventListener('click', () => box.remove());
  $('toasts').append(box);
  raiseToasts();
  while ($('toasts').children.length > 3) $('toasts').firstElementChild.remove();   // keep the stack short
  setTimeout(() => { box.classList.add('out'); setTimeout(() => box.remove(), 300); }, kind === 'err' ? 6000 : 3200);
}
/** A modal sheet sits in the top layer, above everything else; showing the toasts as a popover again puts them above it. */
function raiseToasts() {
  const box = $('toasts');
  if (!box.showPopover) return;
  try { if (box.matches(':popover-open')) box.hidePopover(); box.showPopover(); } catch {}
}
function confirmBox(msg) {
  const d = $('confirm');
  $('confirm-msg').textContent = msg;
  d.returnValue = '';
  d.showModal();
  // Resolve on the button's submit (synchronous) or on close (Esc); the first one wins.
  return new Promise(res => {
    const ac = new AbortController(), done = v => { ac.abort(); res(v); };
    d.querySelector('form').addEventListener('submit', e => done(e.submitter?.value === 'yes'), { signal: ac.signal });
    d.addEventListener('close', () => done(d.returnValue === 'yes'), { signal: ac.signal });
  });
}
/** confirmBox with a field (a password unless told otherwise); resolves to what was typed, or null when cancelled. */
async function promptBox(msg, { type = 'password', value = '' } = {}) {
  const inp = $('confirm-input');
  inp.type = type; inp.value = value; inp.hidden = false;
  setTimeout(() => { inp.focus(); inp.select(); });
  try { return (await confirmBox(msg)) ? inp.value : null; }
  finally { inp.hidden = true; inp.value = ''; }
}
// Enter in the field means OK (the form's default button would be Cancel).
$('confirm-input').addEventListener('keydown', e => {
  if (e.key === 'Enter') { e.preventDefault(); $('confirm').querySelector('button[value=yes]').click(); }
});

/* ---------------- theme & language ---------------- */
const darkMq = matchMedia('(prefers-color-scheme: dark)');
function applyTheme() {
  const th = store.get('panda.theme') || (darkMq.matches ? 'dark' : 'light');
  document.documentElement.dataset.theme = th;
  document.querySelectorAll('.js-theme').forEach(b => setIcon(b, th === 'dark' ? 'sun' : 'moon'));
}
darkMq.addEventListener?.('change', applyTheme);

function applyLang() {
  document.documentElement.lang = lang;
  document.querySelectorAll('[data-i18n]').forEach(e => { e.textContent = t(e.dataset.i18n); });
  document.querySelectorAll('[data-i18n-ph]').forEach(e => { e.placeholder = t(e.dataset.i18nPh); });
  document.querySelectorAll('[data-i18n-aria]').forEach(e => setLabel(e, t(e.dataset.i18nAria)));
  document.querySelectorAll('.js-lang-label').forEach(e => { e.textContent = lang.toUpperCase(); });
  if (me.name) applyRole();
  lastQueueSig = lastHistorySig = '';
  if (S) { render(); if (libBox.open) loadLib(); if (statsBox.open) loadStats(); }
  if (settings) fillSettings(settings);
}

document.querySelectorAll('.js-theme').forEach(b => b.addEventListener('click', () => {
  store.set('panda.theme', document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark'); applyTheme();
}));
document.querySelectorAll('.js-lang').forEach(b => b.addEventListener('click', () => {
  lang = lang === 'en' ? 'tr' : 'en'; store.set('panda.lang', lang); applyLang();
}));

/* ---------------- login / logout ---------------- */
function showLogin() {
  stopStream();
  $('app').hidden = true; $('login').hidden = false;
  if ($('settings').open) $('settings').close();
  if ($('system').open) $('system').close();
  $('login-name').value ||= store.get('panda.user') || '';
  ($('login-name').value ? $('login-pw') : $('login-name')).focus();
}
function showApp() { $('login').hidden = true; $('app').hidden = false; }

$('login-form').addEventListener('submit', async e => {
  e.preventDefault();
  const btn = e.submitter || $('login-form').querySelector('button[type=submit]');
  $('login-err').textContent = '';
  await busy(btn, async () => {
    try {
      const name = $('login-name').value.trim();
      await api('POST', '/api/login', { name, password: $('login-pw').value });
      store.set('panda.user', name);
      $('login-pw').value = '';
      await boot();
    } catch (err) { $('login-err').textContent = errText(err); $('login-pw').select(); }
  });
});
$('logout').addEventListener('click', async () => { await act(() => api('POST', '/api/logout')); showLogin(); });

/* ---------------- state & live stream ---------------- */
let S = null, recvAt = 0;           // last state + when it arrived
let es = null, backoff = 1000, retryTimer = 0, lastMsg = 0;

function setState(s) { S = s; recvAt = Date.now(); lastMsg = recvAt; $('offline').hidden = true; render(); }

function startStream() {
  stopStream();
  es = new EventSource(B('/events'));
  es.onmessage = ev => { backoff = 1000; try { setState(JSON.parse(ev.data)); } catch {} };
  es.onerror = () => { stopStream(); retryTimer = setTimeout(recover, backoff); backoff = Math.min(backoff * 2, 30000); };
}
function stopStream() { if (es) { es.close(); es = null; } clearTimeout(retryTimer); }

/** Keeps the chosen bot when it still exists, otherwise takes the first one. */
async function pickBot() {
  const list = await api('GET', '/api/bots');
  if (!list.some(b => b.id === botId)) botId = list[0]?.id || '';
  store.set('panda.bot', botId);
}
/* Who is logged in. "dj" users only watch and add songs; the server enforces it, the page hides the rest. */
let me = { name: '', role: 'admin' };
const isAdmin = () => me.role === 'admin';
function applyRole() {
  document.body.dataset.role = me.role;
  $('pw-user').textContent = me.name;
  $('pw-form').elements.username.value = me.name;
  setLabel($('logout'), `${t('logout')} (${me.name})`);
}

/** Loads the chosen bot's state and follows its live updates. */
async function loadBot() {
  const m = await api('GET', '/api/me');
  if (m.role !== me.role) lastQueueSig = lastHistorySig = lastBotsSig = '';   // rows have role-specific buttons
  me = m; applyRole();
  try { setState(await api('GET', B('/state'))); }
  catch (e) { if (e.status !== 404) throw e; await pickBot(); setState(await api('GET', B('/state'))); }   // it was removed
  startStream();
  if (statsBox.open) loadStats();   // open since the last visit: its "toggle" came before there was a state
}
/** After a stream error: check the session (401 → login), then reopen the stream. */
async function recover() {
  try { await loadBot(); }
  catch (e) { if (e.status !== 401) { $('offline').hidden = false; retryTimer = setTimeout(recover, backoff); backoff = Math.min(backoff * 2, 30000); } }
}
// Watchdog: the server sends at least every 15 s; reconnect if it has gone quiet.
setInterval(() => { if (es && Date.now() - lastMsg > 40000) { lastMsg = Date.now(); stopStream(); recover(); } }, 5000);

async function boot() {
  try { await pickBot(); await loadBot(); showApp(); if (isAdmin()) checkUpdate(false); }
  catch (e) { if (e.status !== 401) { showApp(); $('offline').hidden = false; retryTimer = setTimeout(recover, backoff); } }
}

function selectBot(id) {
  closeBotMenu();
  if (id === botId) return;
  botId = id; store.set('panda.bot', id);
  lastQueueSig = lastHistorySig = ''; shownTrackId = null;
  stopStream(); backoff = 1000; recover();
  if (statsBox.open) loadStats();
}

/* ---------------- rendering ---------------- */
function render() { renderBot(); renderNow(); renderQueue(); renderPlaylists(); renderHistory(); renderLib(); }

const stateText = st => t(st === 'connected' ? 'connected' : st === 'connecting' ? 'connecting' : 'disconnected');
const manyBots = () => !!S && S.bots.length > 1;
const botName = () => S?.bots.find(x => x.id === botId)?.nickname || '';

function renderBot() {
  const b = S.bot, st = $('status');
  st.dataset.state = b.state;
  let text;
  if (b.state === 'connected') text = [b.serverName, b.channelName, t('listeners', b.listeners), b.following && '🐾 ' + b.following].filter(Boolean).join(' · ');
  else text = b.raisingLevel ? t('raisingId', b.raisingLevel) : stateText(b.state);
  // With several bots the pill also says which one is shown.
  $('status-name').hidden = !manyBots();
  $('status-name').textContent = botName();
  $('status-text').textContent = text;
  st.title = b.error || text;
  $('status-err').hidden = !(b.error && b.state !== 'connected');
  $('status-err').textContent = b.error || '';
  document.querySelectorAll('.js-many').forEach(x => { x.hidden = !manyBots(); });
  renderBotMenu();
}

/* ---------------- bot picker ---------------- */
let lastBotsSig = '';
function renderBotMenu() {
  const s = lang + botId + JSON.stringify(S.bots);
  if (s === lastBotsSig) return;
  lastBotsSig = s;
  $('bot-list').replaceChildren(...S.bots.map(b => {
    const item = el('button', { class: 'menu-item', type: 'button', 'data-state': b.state, 'aria-current': String(b.id === botId) },
      el('span', { class: 'dot' }),
      el('span', { class: 'tx' }, el('span', { class: 'nm', text: b.nickname }),
        el('span', { class: 'song', text: b.song ? '♪ ' + b.song : b.state === 'connected' ? t('idle') : stateText(b.state) })));
    item.addEventListener('click', () => selectBot(b.id));
    return el('li', {}, item);
  }));
}
function openBotMenu() { $('bot-menu').hidden = false; $('status').setAttribute('aria-expanded', 'true'); }
function closeBotMenu() { $('bot-menu').hidden = true; $('status').setAttribute('aria-expanded', 'false'); }
$('status').addEventListener('click', () => $('bot-menu').hidden ? openBotMenu() : closeBotMenu());
document.addEventListener('click', e => { if (!e.target.closest('.bot-pick')) closeBotMenu(); });
document.addEventListener('keydown', e => { if (e.key === 'Escape' && !$('bot-menu').hidden) { closeBotMenu(); $('status').focus(); } });
$('bot-add').addEventListener('click', e => busy(e.currentTarget, async () => {
  const b = await act(() => api('POST', '/api/bots', { like: botId }), t('botAdded'));
  if (!b) return;
  selectBot(b.id);
  openSettings();
}));

let shownTrackId = null;
function renderNow() {
  const cur = S.player.current, p = S.player;
  const playing = !!cur && !cur.paused;
  document.body.classList.toggle('playing', playing);
  $('np-empty').hidden = !!cur; $('np-track').hidden = !cur;
  $('np-live').hidden = !(cur && cur.track.isLive);
  const tg = $('btn-toggle'); setIcon(tg, playing ? 'pause' : 'play'); setLabel(tg, t(playing ? 'pause' : 'play'));
  tg.classList.toggle('on', playing);

  if (cur) {
    const tr = cur.track;
    if (shownTrackId !== tr.id + tr.requestedBy) {
      shownTrackId = tr.id + tr.requestedBy;
      const src = safeUrl(tr.thumbnail);
      for (const img of [$('np-thumb'), $('np-bg')]) { img.hidden = !src; if (src) img.src = src; }
      const a = $('np-title'); a.textContent = tr.title;
      const href = safeUrl(tr.url); if (href) a.href = href; else a.removeAttribute('href');
      $('np-channel').textContent = tr.channel;
    }
    $('np-req').textContent = `${t('requestedBy')} ${tr.requestedBy}`;
    $('seek').hidden = tr.isLive;
    $('seek').disabled = !isAdmin();
    $('seek').max = Math.max(1, tr.duration);
    $('t-dur').textContent = tr.isLive ? t('live') : fmt(tr.duration);
    document.title = `${playing ? '▶' : '⏸'} ${tr.title} — Panda 🐼`;
  } else {
    shownTrackId = null; $('np-bg').hidden = true; document.title = 'Panda 🐼';
  }

  const loop = p.loop || 'Off', lb = $('btn-loop');
  setIcon(lb, loop === 'One' ? 'repeat1' : 'repeat');
  lb.classList.toggle('on', loop !== 'Off');
  setLabel(lb, t('loop' + loop)); lb.setAttribute('aria-pressed', String(loop !== 'Off'));
  const rb = $('btn-radio');
  rb.classList.toggle('on', !!p.radio); rb.setAttribute('aria-pressed', String(!!p.radio));
  setLabel(rb, t(p.radio ? 'radioOn' : 'radioOff'));

  if (!volDragging) showVolume(p.volume);
  tick();
}

/** Smoothly interpolated position. */
function position() {
  const cur = S && S.player.current;
  if (!cur) return 0;
  let pos = cur.position + (cur.paused ? 0 : (Date.now() - recvAt) / 1000);
  if (cur.track.duration > 0) pos = Math.min(pos, cur.track.duration);
  return pos;
}
function tick() {
  const cur = S && S.player.current;
  if (!cur) return;
  const seek = $('seek');
  if (!seekDragging) {
    const pos = position();
    seek.value = Math.floor(pos);
    $('t-pos').textContent = fmt(pos);
  }
  const d = cur.track.duration;
  seek.style.setProperty('--p', d > 0 ? (seek.value / d * 100) + '%' : '100%');
  seek.setAttribute('aria-valuetext', `${fmt(seek.value)} / ${fmt(d)}`);
}
(function loop() { tick(); setTimeout(() => requestAnimationFrame(loop), 200); })();

/* ---------------- controls ---------------- */
const ctl = (name, msg) => act(() => api('POST', B('/control/' + name)), msg);
$('btn-toggle').addEventListener('click', () => ctl('toggle'));
$('btn-skip').addEventListener('click', () => ctl('skip'));
$('btn-shuffle').addEventListener('click', () => ctl('shuffle', '🔀'));
$('btn-stop').addEventListener('click', async () => { if (await confirmBox(t('confirmStop'))) ctl('stop'); });
$('btn-clear').addEventListener('click', async () => { if (await confirmBox(t('confirmClear'))) ctl('clear'); });
$('btn-loop').addEventListener('click', () => {
  const next = { Off: 'All', All: 'One', One: 'Off' }[S.player.loop] || 'All';
  S.player.loop = next; renderNow();                       // optimistic
  act(() => api('POST', B('/loop'), { mode: next }));
});
$('btn-radio').addEventListener('click', () => {
  const on = !S.player.radio;
  S.player.radio = on; renderNow();                         // optimistic
  act(() => api('POST', B('/radio'), { on }), on ? t('radioToast') : null);
});

// Seek bar: preview while dragging, commit on change.
let seekDragging = false, seekPending = false;
const commitSeek = debounce(async v => {
  const cur = S.player.current;
  if (cur) { cur.position = v; recvAt = Date.now(); }       // optimistic
  await act(() => api('POST', B('/seek'), { seconds: v }));
  seekPending = seekDragging = false;
}, 250);
const seek = $('seek');
seek.addEventListener('input', () => { seekDragging = true; $('t-pos').textContent = fmt(seek.value); tick(); });
seek.addEventListener('change', () => { seekPending = true; commitSeek(+seek.value); });
// Released without changing the value: resume following the playback position.
for (const ev of ['pointerup', 'keyup', 'blur']) seek.addEventListener(ev, () => setTimeout(() => { if (!seekPending) seekDragging = false; }, 300));

// Volume: debounced; incoming state is ignored while the user is moving the slider.
let volDragging = false, volIdle = 0, lastVol = 50;
function showVolume(v) {
  const r = $('vol'); r.value = v; r.style.setProperty('--p', v + '%'); $('vol-val').textContent = v;
  setIcon($('btn-mute'), v == 0 ? 'mute' : 'vol'); setLabel($('btn-mute'), t(v == 0 ? 'unmute' : 'mute'));
  if (v > 0) lastVol = v;
}
const sendVolume = debounce(v => act(() => api('POST', B('/volume'), { volume: v })), 200);
function setVolume(v) {
  volDragging = true; clearTimeout(volIdle);
  volIdle = setTimeout(() => { volDragging = false; }, 1200);
  showVolume(v); sendVolume(v);
}
$('vol').addEventListener('input', e => setVolume(+e.target.value));
$('btn-mute').addEventListener('click', () => setVolume(+$('vol').value === 0 ? (lastVol || 50) : 0));

/* ---------------- add / search ---------------- */
const looksLikeUrl = q => /^(https?:\/\/|www\.)|(^|\.)youtu(\.be|be\.com)\//i.test(q);
let searchSeq = 0;

$('add-form').addEventListener('submit', async e => {
  e.preventDefault();
  const q = $('add-q').value.trim();
  if (!q) return $('add-q').focus();
  await busy($('add-go'), async () => {
    if (looksLikeUrl(q)) {
      const tr = await act(() => api('POST', B('/play'), { query: q }), r => `${t('added')}: ${r.title}`);
      if (tr) $('add-q').value = '';
      return;
    }
    const seq = ++searchSeq;
    showResults(null);
    // Saved songs answer instantly; YouTube takes a few seconds.
    const saved = api('GET', '/api/library?q=' + encodeURIComponent(q)).then(r => r.items.slice(0, 5)).catch(() => []);
    saved.then(items => { if (seq === searchSeq && items.length) showResults(items); });
    const list = await act(() => api('GET', '/api/search?q=' + encodeURIComponent(q)));
    if (seq === searchSeq) showResults(await saved, list ? list.slice(0, 10) : []);
  });
});
$('results-close').addEventListener('click', () => { searchSeq++; showResults(null); });

/** saved: library hits (shown first, null = reset), yt: YouTube results (undefined = still loading). */
function showResults(saved, yt) {
  $('results-wrap').hidden = !saved;
  if (!saved) { $('lib-results').replaceChildren(); $('results').replaceChildren(); return; }
  const ids = new Set(saved.map(x => x.id));
  $('lib-results').replaceChildren(...saved.map(tr => resultRow(tr, true)));
  $('lib-res-h').hidden = !saved.length;
  $('yt-res-h').hidden = yt === undefined || !saved.length;
  if (yt === undefined) return;
  const rest = yt.filter(x => !ids.has(x.id));
  $('results').replaceChildren(...rest.map(tr => resultRow(tr, false)));
  if (!rest.length && !saved.length) $('results').append(el('li', { class: 'empty muted', text: t('noResults') }));
}
function resultRow(tr, saved) {
  const extra = saved ? [el('span', { class: 'badge saved sm', text: t('savedBadge') })] : [];
  return el('li', { class: 'track' }, thumb(tr), trackText(tr, { req: false, extra }), playButtons(tr, true));
}
/** "Add" and "Play now" buttons (plus extra buttons) for anything with a url. */
function playButtons(tr, labelled, ...more) {
  const add = el('button', { class: labelled ? 'btn soft sm' : 'icon-btn sm', type: 'button', title: t('add'), 'aria-label': `${t('add')}: ${tr.title}` },
    icon('plus'), labelled && el('span', { class: 'lbl', text: t('add') }));
  const now = el('button', { class: 'icon-btn sm admin-only', type: 'button', title: t('playNow'), 'aria-label': `${t('playNow')}: ${tr.title}` }, icon('play'));
  add.addEventListener('click', () => addTrack(add, tr, false));
  now.addEventListener('click', () => addTrack(now, tr, true));
  return el('div', { class: 'acts' }, add, now, ...more);
}
async function addTrack(btn, tr, now) {
  await busy(btn, async () => {
    const r = await act(() => api('POST', B('/play'), { query: tr.url, now }), r => `${now ? t('playNow') : t('added')}: ${r.title}`);
    if (r) btn.closest('li')?.classList.add('done');
  });
}

/** Title (linked), channel · duration · requester · extra nodes. */
function trackText(tr, { req = true, extra = [] } = {}) {
  const href = safeUrl(tr.url);
  const title = el(href ? 'a' : 'span', { class: 'tt', text: tr.title, title: tr.title, href, target: href && '_blank', rel: href && 'noopener noreferrer' });
  const meta = el('span', { class: 'tm' }, el('span', { text: tr.channel }),
    el('span', { class: tr.isLive ? 'badge live sm' : '', text: tr.isLive ? t('live') : fmt(tr.duration) }), ...extra);
  if (req && tr.requestedBy) meta.append(el('span', { class: 'req' }, icon('user', 'ic sm'), document.createTextNode(tr.requestedBy)));
  return el('div', { class: 'tx' }, title, meta);
}

/* ---------------- library ---------------- */
const LIB_PAGE = 100;
let libItems = [], libShown = 0, libSeq = 0, libKey = '';
const libBox = $('lib-box');

function fmtBytes(n) {
  const u = ['B', 'KB', 'MB', 'GB', 'TB']; let i = 0;
  while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
  return `${n.toLocaleString(lang, { maximumFractionDigits: n < 10 && i > 1 ? 1 : 0 })} ${u[i]}`;
}
/** Count, usage bar and hints come straight from state.library; the list reloads when it changed. */
function renderLib() {
  const L = S.library;
  libBox.hidden = !L;
  if (!L) return;
  $('lib-sum').textContent = `(${L.count})`;
  const limited = L.limitBytes > 0, pct = limited ? Math.min(100, L.usedBytes / L.limitBytes * 100) : 0;
  $('lib-usage').textContent = limited ? t('usage', fmtBytes(L.usedBytes), fmtBytes(L.limitBytes)) : fmtBytes(L.usedBytes);
  $('lib-meter').hidden = !limited;
  $('lib-meter').firstElementChild.style.width = pct + '%';
  $('lib-meter').classList.toggle('full', pct >= 90);
  $('lib-off').hidden = L.enabled;
  const key = `${L.count}|${L.usedBytes}`;
  if (key !== libKey) { libKey = key; if (libBox.open) reloadLib(); }
}

async function loadLib() {
  const seq = ++libSeq, q = $('lib-q').value.trim();
  const r = await act(() => api('GET', '/api/library?q=' + encodeURIComponent(q)));
  if (!r || seq !== libSeq) return;
  libItems = r.items; libShown = 0;
  $('lib-list').replaceChildren();
  renderLibPage();
  $('lib-empty').hidden = libItems.length > 0;
  $('lib-empty').textContent = t(q ? 'libNoMatch' : 'libEmpty');
  $('lib-capped').hidden = !!q || libItems.length >= r.count;   // the API returns at most 500 songs
}
const reloadLib = debounce(loadLib, 300);

/** Renders the next page of rows; keeps the DOM small with big libraries. */
function renderLibPage() {
  const frag = document.createDocumentFragment();
  for (const it of libItems.slice(libShown, libShown + LIB_PAGE)) frag.append(libRow(it));
  libShown = Math.min(libItems.length, libShown + LIB_PAGE);
  $('lib-list').append(frag);
  $('lib-more').hidden = libShown >= libItems.length;
}
function libRow(it) {
  const del = el('button', { class: 'icon-btn sm del admin-only', type: 'button', title: t('delete'), 'aria-label': `${t('delete')}: ${it.title}` }, icon('trash'));
  const li = el('li', { class: 'track' }, thumb(it),
    trackText(it, { req: false, extra: [el('span', { text: t('plays', it.plays) }), el('span', { text: fmtBytes(it.size) })] }),
    playButtons(it, false, starButton(it), del));
  del.addEventListener('click', async () => {
    if (!(await confirmBox(t('confirmLibDelete', it.title)))) return;
    busy(del, async () => {
      const ok = await act(() => api('DELETE', '/api/library/' + encodeURIComponent(it.id)).then(() => true), `${t('deleted')}: ${it.title}`);
      if (ok) { li.remove(); libItems.splice(libItems.indexOf(it), 1); libShown--; }
    });
  });
  return li;
}

libBox.open = store.get('panda.libOpen') !== '0';
libBox.addEventListener('toggle', () => { store.set('panda.libOpen', libBox.open ? '1' : '0'); if (libBox.open && S) loadLib(); });
$('lib-q').addEventListener('input', reloadLib);
$('lib-more').addEventListener('click', renderLibPage);

/* ---------------- queue ---------------- */
let lastQueueSig = '', lastHistorySig = '', dragFrom = -1;
const sig = list => JSON.stringify(list.map(x => [x.id, x.requestedBy]));

function renderQueue() {
  const q = S.player.queue;
  const total = q.reduce((s, x) => s + (x.duration || 0), 0);
  $('queue-sum').textContent = q.length ? `· ${t('songs', q.length)} · ${fmt(total)}${q.some(x => x.isLive) ? '+' : ''}` : '';
  $('queue-empty').hidden = q.length > 0;
  $('btn-clear').hidden = q.length === 0;
  if (dragFrom >= 0) return;                                // don't rebuild under the user's finger
  const s = sig(q);
  if (s === lastQueueSig) return;
  lastQueueSig = s;
  const ol = $('queue');
  const focusKey = document.activeElement?.dataset?.key;    // keep keyboard focus after reorder
  ol.replaceChildren(...q.map((tr, i) => queueItem(tr, i, q.length)));
  if (focusKey) ol.querySelector(`[data-key="${CSS.escape(focusKey)}"]`)?.focus();
}

function queueItem(tr, i, n) {
  const key = tr.id + '|' + tr.requestedBy;
  const btn = (name, label, fn, dis) => {
    const b = el('button', { class: 'icon-btn sm', type: 'button', title: label, 'aria-label': `${label}: ${tr.title}`, disabled: dis, 'data-key': `${name}:${key}` }, icon(name));
    b.addEventListener('click', fn); return b;
  };
  const li = el('li', { class: 'track', draggable: isAdmin() ? 'true' : null },
    el('span', { class: 'grip admin-only', 'aria-hidden': 'true' }, icon('grip')),
    el('span', { class: 'num', text: i + 1 }),
    thumb(tr), trackText(tr),
    el('div', { class: 'acts admin-only' },
      btn('up', t('moveUp'), () => move(i, i - 1), i === 0),
      btn('down', t('moveDown'), () => move(i, i + 1), i === n - 1),
      btn('x', t('remove'), () => remove(i, tr.id))));
  li.dataset.index = i;
  li.addEventListener('dragstart', e => { dragFrom = i; li.classList.add('dragging'); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', String(i)); });
  li.addEventListener('dragend', () => { dragFrom = -1; li.classList.remove('dragging'); clearDropMarks(); renderQueue(); });
  li.addEventListener('dragover', e => {
    if (dragFrom < 0) return;
    e.preventDefault(); clearDropMarks();
    if (i !== dragFrom) li.classList.add(i < dragFrom ? 'drop-before' : 'drop-after');
  });
  li.addEventListener('drop', e => { e.preventDefault(); const from = dragFrom; dragFrom = -1; clearDropMarks(); if (from >= 0 && from !== i) move(from, i); });
  return li;
}
function clearDropMarks() { document.querySelectorAll('.drop-before,.drop-after').forEach(x => x.classList.remove('drop-before', 'drop-after')); }

function move(from, to) {
  const q = S.player.queue;
  if (to < 0 || to >= q.length) return;
  const id = q[from].id;
  q.splice(to, 0, q.splice(from, 1)[0]); renderQueue();   // optimistic
  queueEdit(() => api('POST', B('/queue/move'), { from, to, id }));
}
function remove(i, id) {
  S.player.queue.splice(i, 1); renderQueue();              // optimistic
  queueEdit(() => api('DELETE', B(`/queue/${i}?id=${encodeURIComponent(id)}`)));
}
/** The server refuses (409) when the queue moved on in the meantime; then show its real state. */
async function queueEdit(fn) {
  try { await fn(); }
  catch (e) {
    if (e.status === 409) { const s = await api('GET', B('/state')).catch(() => null); if (s) setState(s); }
    else if (e.status !== 401) toast(errText(e), 'err');
  }
}

/* ---------------- history ---------------- */
/** "Today 21:14", "Yesterday 23:02", "10 Oct 18:30" (+ year when not this year), in the viewer's time zone. */
function playedAt(iso) {
  const d = new Date(iso);
  if (isNaN(d)) return '';
  const time = d.toLocaleTimeString(lang, { hour: '2-digit', minute: '2-digit' });
  const day = x => new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
  const days = Math.round((day(new Date()) - day(d)) / 86400000);
  if (days === 0) return `${t('today')} ${time}`;
  if (days === 1) return `${t('yesterday')} ${time}`;
  const sameYear = d.getFullYear() === new Date().getFullYear();
  return `${d.toLocaleDateString(lang, { day: 'numeric', month: 'short', year: sameYear ? undefined : 'numeric' })} ${time}`;
}

function renderHistory() {
  const h = S.player.history;
  $('history-sum').textContent = h.length ? `(${h.length})` : '';
  $('history-empty').hidden = h.length > 0;
  // The date is part of the signature so "Today" turns into "Yesterday" after midnight.
  const s = new Date().toDateString() + JSON.stringify(h.map(x => [x.id, x.requestedBy, x.playedAt]));
  if (s === lastHistorySig) return;
  if (lastHistorySig) reloadStats();   // a song finished
  lastHistorySig = s;
  $('history').replaceChildren(...h.map(tr => {
    const b = el('button', { class: 'icon-btn sm', type: 'button', title: t('addAgain'), 'aria-label': `${t('addAgain')}: ${tr.title}` }, icon('plus'));
    b.addEventListener('click', () => busy(b, () => act(() => api('POST', B('/play'), { query: tr.url }), r => `${t('added')}: ${r.title}`)));
    const when = el('span', { class: 'when', title: new Date(tr.playedAt).toLocaleString(lang) }, icon('clock', 'ic sm'), document.createTextNode(playedAt(tr.playedAt)));
    return el('li', { class: 'track' }, thumb(tr), trackText(tr, { extra: [when] }), el('div', { class: 'acts' }, starButton(tr), b));
  }));
}

/* ---------------- settings ---------------- */
let settings = null, pwTouched = {};
const F = () => $('set-form').elements;

/** Settings of the bot shown in the panel (plus the shared ones). Others only get their password. */
async function openSettings() {
  if (!$('settings').open) $('settings').showModal();
  $('set-bot').textContent = isAdmin() ? botName() : '';
  const v = api('GET', '/api/version').then(v => { $('version').textContent = 'v' + v.version; }).catch(() => {});
  if (isAdmin()) {
    const s = await act(() => api('GET', B('/settings')));
    if (s) fillSettings(s);
    loadUsers();
  }
  await v;
}
$('open-settings').addEventListener('click', openSettings);

/* ---------------- panel users (admins) ---------------- */
async function loadUsers() {
  const list = await act(() => api('GET', '/api/users'));
  if (!list) return;
  $('user-list').replaceChildren(...list.map(u => {
    const self = u.name.toLowerCase() === me.name.toLowerCase();
    const role = el('select', { 'aria-label': `${t('role')}: ${u.name}` },
      el('option', { value: 'admin', text: t('roleAdmin') }), el('option', { value: 'dj', text: t('roleDj') }));
    role.value = u.role;
    role.addEventListener('change', async () => {
      const ok = await act(() => api('PUT', '/api/users/' + encodeURIComponent(u.name), { role: role.value }).then(() => true), t('roleChanged'));
      if (!ok) role.value = u.role; else u.role = role.value;
      if (ok && self) loadBot().catch(() => {});   // demoted yourself: the page follows
    });
    const pw = el('button', { class: 'icon-btn sm', type: 'button', title: t('resetPw'), 'aria-label': `${t('resetPw')}: ${u.name}` }, icon('key'));
    pw.addEventListener('click', async () => {
      const v = await promptBox(t('resetPwFor', u.name));
      if (v == null) return;
      if (v.length < 6) return toast(t('err.password-too-short'), 'err');
      await act(() => api('PUT', '/api/users/' + encodeURIComponent(u.name), { password: v }), t('pwReset'));
    });
    const del = el('button', { class: 'icon-btn sm del', type: 'button', title: t('delete'), 'aria-label': `${t('delete')}: ${u.name}`, disabled: self }, icon('trash'));
    del.addEventListener('click', async () => {
      if (!(await confirmBox(t('confirmUserRemove', u.name)))) return;
      const ok = await act(() => api('DELETE', '/api/users/' + encodeURIComponent(u.name)).then(() => true), t('userRemoved'));
      if (ok) loadUsers();
    });
    return el('li', {}, el('span', { class: 'nm', text: u.name }, self && el('span', { class: 'you', text: ' ' + t('you') })), role, pw, del);
  }));
}
$('user-form').addEventListener('submit', async e => {
  e.preventDefault();
  const f = e.target.elements;
  await busy(e.submitter || e.target.querySelector('[type=submit]'), async () => {
    const ok = await act(() => api('POST', '/api/users', { name: f.name.value.trim(), password: f.password.value, role: f.role.value }).then(() => true), t('userAdded'));
    if (ok) { e.target.reset(); loadUsers(); }
  });
});

$('bot-remove').addEventListener('click', async e => {
  const btn = e.currentTarget, name = settings?.nickname || botName();
  if (!(await confirmBox(t('confirmBotRemove', name)))) return;
  busy(btn, async () => {
    const ok = await act(() => api('DELETE', B('')).then(() => true), `${t('botRemoved')}: ${name}`);
    if (!ok) return;
    $('settings').close();
    stopStream(); lastQueueSig = lastHistorySig = ''; shownTrackId = null;
    await act(pickBot);
    recover();
  });
});
$('settings').addEventListener('click', e => {
  if (e.target === $('settings') || e.target.closest('[data-close]')) $('settings').close();   // backdrop / close button
});

function fillSettings(s) {
  settings = s; pwTouched = {};
  $('set-bot').textContent = s.nickname;
  const f = F();
  for (const k of ['serverAddress', 'channel', 'nickname', 'commandPrefix', 'language', 'maxQueueLength', 'maxDurationMinutes']) f[k].value = s[k] ?? '';
  f.announce.checked = !!s.announce; f.showSongInDescription.checked = !!s.showSongInDescription;
  f.allowedUsers.value = (s.allowedUsers || []).join('\n');
  f.allowedServerGroups.value = (s.allowedServerGroups || []).join(', ');
  f.libraryEnabled.checked = !!s.libraryEnabled; f.normalizeVolume.checked = !!s.normalizeVolume;
  f.libraryMaxGb.value = Math.round((s.libraryMaxSizeMb || 0) / 1024 * 100) / 100;
  for (const k of ['serverPassword', 'channelPassword']) {
    const has = s['has' + k[0].toUpperCase() + k.slice(1)];
    f[k].value = ''; f[k].disabled = false; f[k].placeholder = has ? t('unchanged') : '';
    f[k + 'Remove'].checked = false;
    f[k + 'Remove'].closest('label').hidden = !has;
  }
  const c = $('cookie-status');
  c.textContent = t(s.hasCookies ? 'cookiesOn' : 'cookiesOff'); c.classList.toggle('on', s.hasCookies);
  $('cookie-remove').hidden = !s.hasCookies;
}
for (const k of ['serverPassword', 'channelPassword']) {
  F()[k].addEventListener('input', () => { pwTouched[k] = true; });
  F()[k + 'Remove'].addEventListener('change', e => { F()[k].disabled = e.target.checked; if (e.target.checked) F()[k].value = ''; });
}
/** Password fields: null = keep, "" = remove (only via the checkbox), otherwise the new value. */
function pwValue(k) {
  const f = F();
  if (f[k + 'Remove'].checked) return '';
  return pwTouched[k] && f[k].value ? f[k].value : null;
}

$('set-form').addEventListener('submit', async e => {
  e.preventDefault();
  const form = $('set-form'), f = form.elements;
  if (!settings) return;
  const gb = +f.libraryMaxGb.value;
  f.libraryMaxGb.setCustomValidity(gb === 0 || gb >= 0.1 ? '' : t('sLibMaxBad'));
  if (!form.checkValidity()) { form.reportValidity(); return toast(t('invalid'), 'err'); }
  const body = {
    ...settings,
    serverAddress: f.serverAddress.value.trim(), serverPassword: pwValue('serverPassword'),
    channel: f.channel.value.trim(), channelPassword: pwValue('channelPassword'),
    nickname: f.nickname.value.trim(), commandPrefix: f.commandPrefix.value.trim(), language: f.language.value,
    announce: f.announce.checked, showSongInDescription: f.showSongInDescription.checked,
    maxQueueLength: +f.maxQueueLength.value, maxDurationMinutes: +f.maxDurationMinutes.value,
    allowedUsers: f.allowedUsers.value.split(/\r?\n/).map(x => x.trim()).filter(Boolean),
    libraryEnabled: f.libraryEnabled.checked, libraryMaxSizeMb: Math.round(gb * 1024), normalizeVolume: f.normalizeVolume.checked,
    allowedServerGroups: f.allowedServerGroups.value.split(/[,\s;]+/).map(x => x.trim()).filter(x => /^\d+$/.test(x)).map(Number),
  };
  const old = settings;
  const reconnects = body.serverAddress !== old.serverAddress || body.channel !== old.channel || body.nickname !== old.nickname ||
    body.serverPassword !== null || body.channelPassword !== null;
  await busy($('set-save'), async () => {
    const s = await act(() => api('PUT', B('/settings'), body), t(reconnects ? 'reconnecting' : 'saved'));
    if (s) fillSettings(s);
  });
});
$('set-reconnect').addEventListener('click', e => busy(e.currentTarget, () => act(() => api('POST', B('/reconnect')), t('reconnectSent'))));

// Cookies
$('cookie-file').addEventListener('change', async e => {
  const file = e.target.files[0];
  if (file) $('cookie-text').value = await file.text();
  e.target.value = '';
});
$('cookie-save').addEventListener('click', e => {
  const text = $('cookie-text').value;
  if (!text.trim()) return toast(t('cookiesEmpty'), 'err');
  busy(e.currentTarget, async () => {
    const r = await act(() => api('POST', '/api/cookies', text), t('cookiesSaved'));
    if (r) { $('cookie-text').value = ''; fillSettings({ ...settings, hasCookies: r.hasCookies }); }
  });
});
$('cookie-remove').addEventListener('click', async e => {
  const btn = e.currentTarget;
  if (!(await confirmBox(t('confirmCookies')))) return;
  busy(btn, async () => {
    const r = await act(() => api('POST', '/api/cookies', ''), t('cookiesRemoved'));
    if (r) fillSettings({ ...settings, hasCookies: r.hasCookies });
  });
});

// Panel password
$('pw-form').addEventListener('submit', async e => {
  e.preventDefault();
  const f = e.target.elements;
  if (f.new.value.length < 6) return toast(t('err.password-too-short'), 'err');
  await busy(e.submitter || e.target.querySelector('[type=submit]'), async () => {
    const ok = await act(() => api('POST', '/api/password', { current: f.current.value, new: f.new.value }).then(() => true), t('pwChanged'));
    if (ok) e.target.reset();
  });
});

/* ---------------- playlists ---------------- */
const plBox = $('pl-box');
let lastPlSig = '', openPl = null;
const asSong = tr => ({ id: tr.id, title: tr.title, channel: tr.channel, duration: tr.duration });
const plUrl = name => '/api/playlists/' + encodeURIComponent(name);

plBox.open = store.get('panda.plOpen') === '1';
plBox.addEventListener('toggle', () => store.set('panda.plOpen', plBox.open ? '1' : '0'));

/** The list of playlists comes with every state update; a playlist's songs load when it is opened. */
function renderPlaylists() {
  const P = S.playlists || [];
  $('pl-sum').textContent = P.length ? `(${P.length})` : '';
  $('pl-empty').hidden = P.length > 0;
  const s = lang + me.role + JSON.stringify(P);
  if (s === lastPlSig) return;
  lastPlSig = s;
  if (openPl && !P.some(p => p.name === openPl)) openPl = null;
  $('pl-list').replaceChildren(...P.map(p => {
    const btn = (icon_, label, fn, cls = '') => {
      const b = el('button', { class: 'icon-btn sm ' + cls, type: 'button', title: label, 'aria-label': `${label}: ${p.name}` }, icon(icon_));
      b.addEventListener('click', () => fn(b)); return b;
    };
    const open = el('button', { class: 'open', type: 'button', 'aria-expanded': String(openPl === p.name) },
      el('span', { class: 'nm', text: p.name }), el('span', { class: 'meta', text: `${t('songs', p.count)} · ${fmt(p.duration)}` }));
    open.addEventListener('click', () => { openPl = openPl === p.name ? null : p.name; lastPlSig = ''; renderPlaylists(); });
    const li = el('li', {}, el('div', { class: 'pl-row' }, open,
      btn('plus', t('plPlay'), b => playPlaylist(b, p.name, false)),
      btn('shuffle', t('plShuffle'), b => playPlaylist(b, p.name, true)),
      btn('pencil', t('plRename'), () => renamePlaylist(p.name), 'admin-only'),
      btn('trash', t('plDelete'), () => deletePlaylist(p.name), 'del admin-only')));
    if (openPl === p.name) loadPlaylistSongs(p.name, li);
    return li;
  }));
}

async function loadPlaylistSongs(name, li) {
  const p = await api('GET', plUrl(name)).catch(() => null);
  if (!p || openPl !== name) return;
  li.append(el('ul', { class: 'tracks pl-songs' }, ...p.songs.map((s, i) => {
    const rm = el('button', { class: 'icon-btn sm admin-only', type: 'button', title: t('remove'), 'aria-label': `${t('remove')}: ${s.title}` }, icon('x'));
    rm.addEventListener('click', () => act(() => api('DELETE', `${plUrl(name)}/songs/${i}?id=${encodeURIComponent(s.id)}`)));
    return el('li', { class: 'track' }, thumb(s), trackText(s, { req: false }), playButtons(s, false, rm));
  })));
}

function playPlaylist(btn, name, shuffle) {
  busy(btn, () => act(() => api('POST', B('/playlist'), { name, shuffle }), r => t('plQueued', r.added, name)));
}
async function renamePlaylist(name) {
  const v = await promptBox(t('plName'), { type: 'text', value: name });
  if (v == null || v.trim() === name) return;
  if (openPl === name) openPl = v.trim();
  await act(() => api('PUT', plUrl(name), { name: v.trim() }), t('plRenamed'));
}
async function deletePlaylist(name) {
  if (await confirmBox(t('confirmPlDelete', name))) act(() => api('DELETE', plUrl(name)), t('plDeleted'));
}
/** Adds songs to a playlist (made when it doesn't exist yet). */
const saveToPlaylist = (name, body) => act(() => api('POST', '/api/playlists', { name, ...body }), r => t('plSaved', r.name));

$('pl-save-queue').addEventListener('click', async () => {
  const name = await promptBox(t('plName'), { type: 'text' });
  if (name?.trim()) saveToPlaylist(name.trim(), { fromBot: botId });
});

/* "Add to playlist" menu next to a star button */
let plMenuSongs = [];
function openPlMenu(anchor, songs) {
  plMenuSongs = songs.map(asSong);
  const m = $('pl-menu');
  $('pl-menu-list').replaceChildren(...(S.playlists || []).map(p => {
    const b = el('button', { class: 'menu-item', type: 'button' }, icon('list'),
      el('span', { class: 'tx' }, el('span', { class: 'nm', text: p.name }), el('span', { class: 'song', text: t('songs', p.count) })));
    b.addEventListener('click', () => { closePlMenu(); saveToPlaylist(p.name, { songs: plMenuSongs }); });
    return el('li', {}, b);
  }));
  m.hidden = false;
  const r = anchor.getBoundingClientRect(), w = m.offsetWidth, h = m.offsetHeight;
  m.style.left = Math.max(8, Math.min(r.right - w, innerWidth - w - 8)) + 'px';
  m.style.top = (r.bottom + h + 8 < innerHeight ? r.bottom + 6 : Math.max(8, r.top - h - 6)) + 'px';
}
function closePlMenu() { $('pl-menu').hidden = true; }
$('pl-menu-new').addEventListener('click', async () => {
  closePlMenu();
  const name = await promptBox(t('plName'), { type: 'text' });
  if (name?.trim()) saveToPlaylist(name.trim(), { songs: plMenuSongs });
});
document.addEventListener('click', e => { if (!e.target.closest('#pl-menu, .js-star, #np-save')) closePlMenu(); });
document.addEventListener('keydown', e => { if (e.key === 'Escape') closePlMenu(); });
addEventListener('scroll', closePlMenu, { passive: true });
$('np-save').addEventListener('click', e => { const cur = S?.player.current; if (cur) openPlMenu(e.currentTarget, [cur.track]); });
/** Star button for a track row (admins). */
function starButton(tr) {
  const b = el('button', { class: 'icon-btn sm admin-only js-star', type: 'button', title: t('plAdd'), 'aria-label': `${t('plAdd')}: ${tr.title}` }, icon('star'));
  b.addEventListener('click', () => openPlMenu(b, [tr]));
  return b;
}

/* ---------------- statistics ---------------- */
const statsBox = $('stats-box');
let statsSeq = 0;

async function loadStats() {
  const seq = ++statsSeq;
  const bot = manyBots() && $('stats-bot').value === 'all' ? 'all' : botId;
  const r = await act(() => api('GET', `/api/stats?bot=${encodeURIComponent(bot)}&days=${$('stats-days').value}&tz=${new Date().getTimezoneOffset()}`));
  if (r && seq === statsSeq) renderStats(r);
}
/** A song finished: refresh the numbers, but not on every little change. */
const reloadStats = debounce(() => { if (statsBox.open) loadStats(); }, 1500);
const fmtListened = sec => t('uptime', Math.floor(sec / 86400), Math.floor(sec / 3600) % 24, Math.floor(sec / 60) % 60);

statsBox.open = store.get('panda.statsOpen') === '1';
statsBox.addEventListener('toggle', () => { store.set('panda.statsOpen', statsBox.open ? '1' : '0'); if (statsBox.open && S) loadStats(); });
$('stats-days').value = store.get('panda.statsDays') || '30';
$('stats-days').addEventListener('change', () => { store.set('panda.statsDays', $('stats-days').value); loadStats(); });
$('stats-bot').addEventListener('change', loadStats);

function renderStats(r) {
  if (!r.plays) return $('stats').replaceChildren(el('p', { class: 'empty muted', text: t('statsEmpty') }));
  const num = n => n.toLocaleString(lang);
  const tile = (v, k) => el('div', { class: 'tile' }, el('b', { text: v }), el('span', { text: k }));
  /** Bar chart: items → bars as tall as their plays, a tooltip each and a few labels under it. */
  const chart = (title, items, tip, labels) => {
    const max = Math.max(1, ...items.map(x => x.plays));
    return el('section', { class: 'chart' }, el('h4', { class: 'eyebrow', text: title }),
      el('div', { class: 'bars', role: 'img', 'aria-label': title },
        ...items.map(x => el('i', { class: x.plays ? '' : 'zero', style: `height:${Math.max(2, x.plays / max * 100)}%`, title: `${tip(x)}: ${t('plays', x.plays)}` }))),
      el('div', { class: 'bar-labels' }, ...labels.map(l => el('span', { text: l }))));
  };
  const dayLabel = k => new Date(k + 'T12:00').toLocaleDateString(lang, { day: 'numeric', month: 'short' });
  const monthLabel = k => new Date(k + '-15T12:00').toLocaleDateString(lang, { month: 'short', year: '2-digit' });
  const label = r.bucket === 'day' ? dayLabel : monthLabel;
  const tl = r.timeline;
  const pick = [...new Set([0, Math.floor((tl.length - 1) / 2), tl.length - 1])].filter(i => i >= 0 && i < tl.length);

  const maxPerson = Math.max(1, ...r.topPeople.map(p => p.plays));
  $('stats').replaceChildren(
    el('div', { class: 'tiles' },
      tile(num(r.plays), t('sPlays')), tile(fmtListened(r.listened), t('sListened')),
      tile(num(r.songs), t('sSongs')), tile(num(r.people), t('sPeople'))),
    tl.length > 1 ? chart(t('activity'), tl, x => label(x.key), pick.map(i => label(tl[i].key))) : null,
    chart(t('byHour'), r.hours.map((plays, h) => ({ plays, h })), x => `${x.h}:00–${x.h}:59`, ['0:00', '6:00', '12:00', '18:00', '23:00']),
    el('section', {}, el('h4', { class: 'eyebrow', text: t('topSongs') }),
      el('ol', { class: 'tracks queue' }, ...r.topSongs.map((s, i) => el('li', { class: 'track' },
        el('span', { class: 'num', text: i + 1 }), thumb(s),
        trackText(s, { req: false, extra: [el('span', { text: t('plays', s.plays) }), el('span', { text: fmtListened(s.listened) })] }),
        playButtons(s, false))))),
    el('section', {}, el('h4', { class: 'eyebrow', text: t('topPeople') }),
      el('ol', { class: 'people' }, ...r.topPeople.map((p, i) => el('li', {},
        el('span', { class: 'num', text: i + 1 }), el('span', { class: 'nm', text: p.name, title: p.name }),
        el('span', { class: 'val', text: `${t('requests', p.plays)} · ${fmtListened(p.listened)}` }),
        el('div', { class: 'meter' }, el('span', { style: `width:${p.plays / maxPerson * 100}%` })))))));
}

/* ---------------- system: health + logs (admins) ---------------- */
let healthTimer = 0, logEs = null, logLines = [], logLast = 0;
const LEVELS = { debug: 0, info: 1, warn: 2, error: 3 };
const pct = n => n == null ? '—' : new Intl.NumberFormat(lang, { style: 'percent', maximumFractionDigits: 1 }).format(n / 100);

$('open-system').addEventListener('click', () => { $('system').showModal(); showSysTab(store.get('panda.sysTab') || 'health'); });
$('system').addEventListener('click', e => {
  if (e.target === $('system') || e.target.closest('[data-close]')) $('system').close();
});
$('system').addEventListener('close', () => { clearTimeout(healthTimer); stopLogs(); });
$('tab-health').addEventListener('click', () => showSysTab('health'));
$('tab-logs').addEventListener('click', () => showSysTab('logs'));

function showSysTab(name) {
  store.set('panda.sysTab', name);
  for (const n of ['health', 'logs']) {
    $('tab-' + n).setAttribute('aria-selected', String(n === name));
    $('sys-' + n).hidden = n !== name;
  }
  clearTimeout(healthTimer);
  if (name === 'health') { stopLogs(); loadHealth(); renderUpdate(); checkUpdate(false); } else startLogs();
}

/* Updates: one row for Panda, one for yt-dlp, each with what is installed, what is newest and what
   an update did. GitHub is asked at most hourly by the server; "Check" asks right away. */
let updateInfo = null, updChecking = false, upgradeWatch = false;
const updMsg = { panda: null, yt: null };   // { kind: 'busy' | 'ok' | 'err', text }

async function checkUpdate(fresh, follow = true) {
  if (fresh) {
    updChecking = true;
    for (const k of ['panda', 'yt']) if (updMsg[k]?.kind !== 'busy') updMsg[k] = null;   // a running update stays
    renderUpdate();
  }
  const u = await api('GET', '/api/update' + (fresh ? '?fresh=true' : '')).catch(() => null);
  updChecking = false;
  if (u) { updateInfo = u; $('open-system').classList.toggle('badge-dot', !!u.newer); }
  renderUpdate();
  // An update started earlier (before a reload, or from another tab) is still running: follow it.
  if (!follow) return;
  if (u?.upgrading) waitForUpgrade(u.current);
  if (u?.ytDlpUpdating && updMsg.yt?.kind !== 'busy') updateYtDlp();
}

function renderUpdate() {
  const u = updateInfo;
  const button = (label, iconName, fn, primary) => {
    const b = el('button', { class: `btn sm ${primary ? 'primary' : 'ghost'}`, type: 'button' }, icon(iconName), el('span', { text: label }));
    b.addEventListener('click', () => busy(b, fn)); return b;
  };
  const check = button(t('updCheck'), 'refresh', () => checkUpdate(true));
  if (updChecking) { check.disabled = true; check.prepend(el('span', { class: 'spin', 'aria-hidden': 'true' })); }
  const when = u?.checkedAt && !u.error ? el('span', { class: 'hint', text: t('updChecked', playedAt(u.checkedAt)) }) : null;
  const sub = u?.error ? el('p', { class: 'hint bad', text: t('updUnreachable') }) : null;

  const item = ({ name, what, installed, newest, newer, msg, actions = [], hint }) => {
    const pill = msg?.kind === 'busy' ? null
      : !newest ? null
      : newer ? el('span', { class: 'upd-pill new', text: t('updNew') })
      : el('span', { class: 'upd-pill ok' }, icon('check'), el('span', { text: t('updOk') }));
    const status = msg && el('p', { class: 'upd-msg ' + msg.kind, role: 'status' },
      msg.kind === 'busy' ? el('span', { class: 'spin', 'aria-hidden': 'true' }) : icon(msg.kind === 'err' ? 'alert' : 'check'),
      el('span', { text: msg.text }));
    return el('div', { class: 'upd-item' },
      el('div', { class: 'upd-top' }, el('strong', { text: name }), pill),
      el('p', { class: 'hint', text: what }),
      el('div', { class: 'upd-versions' },
        el('span', {}, el('span', { class: 'k', text: t('updInstalled') }), el('b', { text: installed || t('missingTool') })),
        el('span', {}, el('span', { class: 'k', text: t('updNewest') }), el('b', { class: newer ? 'new' : '', text: newest || '—' }))),
      status, hint ? el('p', { class: 'hint', text: hint }) : null,
      actions.length && msg?.kind !== 'busy' ? el('div', { class: 'save-row' }, ...actions) : null);
  };

  const pandaActions = [];
  if (u?.newer && u.install === 'systemd') pandaActions.push(button(t('updNow'), 'download', startUpgrade, true));
  if (u?.newer && u.notes) pandaActions.push(el('details', { class: 'upd-notes' }, el('summary', { text: t('updNotes') }), el('pre', { class: 'notes', text: u.notes })));
  if (u?.newer && u.url && safeUrl(u.url)) pandaActions.push(el('a', { class: 'btn ghost sm', href: safeUrl(u.url), target: '_blank', rel: 'noopener noreferrer' }, icon('ext'), el('span', { text: t('updRelease') })));
  // Unknown newest version (GitHub unreachable) or a missing yt-dlp: let them try anyway.
  const ytActions = u && (u.ytDlpNewer || !u.ytDlpLatest || !u.ytDlp) ? [button(t('ytUpdate'), 'download', updateYtDlp, !!u.ytDlpNewer)] : [];

  $('update').replaceChildren(
    el('div', { class: 'upd-head' }, el('h3', { text: t('updates') }), el('div', { class: 'upd-check' }, when, check)), sub ?? '',
    item({ name: 'Panda', what: t('updPandaWhat'), installed: u && 'v' + u.current, newest: u?.latest && 'v' + u.latest,
      newer: u?.newer, msg: updMsg.panda, actions: pandaActions,
      hint: u?.newer && u.install !== 'systemd' ? t('updHow.' + u.install) : null }),
    item({ name: 'yt-dlp', what: t('updYtWhat'), installed: u?.ytDlp, newest: u?.ytDlpLatest,
      newer: u?.ytDlpNewer, msg: updMsg.yt, actions: ytActions }));
}

async function startUpgrade() {
  if (upgradeWatch) return;
  updMsg.panda = { kind: 'busy', text: t('updStarted') }; renderUpdate();
  try { await api('POST', '/api/update'); }
  catch (e) { updMsg.panda = { kind: 'err', text: errText(e) }; return renderUpdate(); }
  await waitForUpgrade(updateInfo?.current);
}

/** The installer replaces Panda and restarts it; waits for the new version, then loads the new panel. */
async function waitForUpgrade(from) {
  if (upgradeWatch) return;
  upgradeWatch = true;
  updMsg.panda = { kind: 'busy', text: t('updStarted') }; renderUpdate();
  try {
    // Up to 16 minutes while the installer runs (systemd stops it after 15), 5 without any answer.
    for (let i = 0, idle = 0, gone = 0; i < 192 && gone < 60; i++) {
      await new Promise(r => setTimeout(r, 5000));
      // Fails while Panda restarts; the new Panda answers with its version.
      const p = await api('GET', '/api/update/progress').catch(() => null);
      if (!p) { gone++; continue; }
      gone = 0;
      if (p.version !== from) {
        updMsg.panda = { kind: 'ok', text: t('updDone', p.version) }; renderUpdate();
        setTimeout(() => location.reload(), 2000); return;
      }
      // "idle" twice in a row: the installer finished without bringing a new Panda.
      idle = p.state === 'idle' ? idle + 1 : 0;
      if (p.state === 'failed' || idle >= 2) {
        updMsg.panda = { kind: 'err', text: t('updFailed') }; renderUpdate(); return;
      }
    }
    updMsg.panda = { kind: 'err', text: t('updSlow') }; renderUpdate();
  } finally { upgradeWatch = false; }
}

async function updateYtDlp() {
  if (updMsg.yt?.kind === 'busy') return;
  updMsg.yt = { kind: 'busy', text: t('ytBusy') }; renderUpdate();
  try {
    const r = await api('POST', '/api/update/yt-dlp');
    updMsg.yt = r.result === 'updated' || r.result === 'installed' ? { kind: 'ok', text: t('ytDone', r.from, r.to) }
      : r.result === 'current' ? { kind: 'ok', text: t('ytCurrent', r.to) }
      : r.result === 'pending' ? { kind: 'busy', text: t('ytPending') }
      : r.result === 'reinstall' ? { kind: 'err', text: t('updHow.systemd-old') }
      : { kind: 'err', text: t('err.ytdlp-update-failed') };
  } catch (e) { updMsg.yt = { kind: 'err', text: errText(e) }; }
  if (updMsg.yt.kind === 'busy') updMsg.yt.kind = 'ok';   // nothing runs here any more; just a note
  await checkUpdate(false, false);   // following again would start the update again if systemd never picked it up
}

/** Refreshes every 3 s while the status tab is open. */
async function loadHealth(freshTools) {
  clearTimeout(healthTimer);
  try { renderHealth(await api('GET', '/api/system' + (freshTools === true ? '?fresh=true' : ''))); }
  catch (e) { if (e.status === 401) return; }
  if ($('system').open && !$('sys-health').hidden) healthTimer = setTimeout(loadHealth, 3000);
}

function renderHealth(h) {
  const box = $('health');
  // Rings and bars move from their last value instead of jumping (the view is rebuilt every 3 s).
  const was = {}, moves = [];
  for (const e of box.querySelectorAll('[data-k]')) was[e.dataset.k] = e.dataset.to;
  const clamp = p => Math.min(100, Math.max(0, p || 0));
  const level = p => p >= 90 ? ' bad' : p >= 75 ? ' warn' : '';
  const grow = (e, k, p, set) => {
    e.dataset.k = k; e.dataset.to = clamp(p);
    set(e, was[k] ?? 0); moves.push(() => set(e, e.dataset.to));
    return e;
  };
  const whole = new Intl.NumberFormat(lang, { style: 'percent', maximumFractionDigits: 0 });
  const kv = (k, v, bad) => el('div', { class: 'kv' }, el('span', { class: 'k', text: k }), el('span', { class: 'v' + (bad ? ' bad' : ''), text: v }));
  const meter = (k, p) => el('div', { class: 'meter' + level(p) }, grow(el('span'), k, p, (e, v) => { e.style.width = v + '%'; }));
  /** p = null: nothing to measure against; text is shown instead of a percentage. */
  const ring = (k, p, text) => {
    const label = text ?? whole.format(p / 100);
    return grow(el('div', { class: 'ring' + (p == null ? '' : level(p)), role: 'img', 'aria-label': label }, el('span', { text: label })),
      k, p, (e, v) => e.style.setProperty('--p', v));
  };
  const title = (emoji, text) => el('h3', {}, el('span', { 'aria-hidden': 'true', text: emoji }), document.createTextNode(text));
  const card = (emoji, name, ...kids) => el('section', { class: 'hcard' }, title(emoji, name), ...kids);
  const gauge = (emoji, name, r, big, cap, ...foot) => el('section', { class: 'hcard gauge' }, title(emoji, name), r,
    el('div', { class: 'gauge-main' }, el('strong', { text: big }), el('span', { class: 'muted', text: cap })),
    el('div', { class: 'gauge-foot' }, ...foot));
  const up = h.uptimeSeconds, m = h.machine, d = h.disk, L = h.library;
  const ram = m.memoryUsedBytes != null && m.memoryTotalBytes ? m.memoryUsedBytes / m.memoryTotalBytes * 100 : null;
  const disk = d ? (1 - d.freeBytes / d.totalBytes) * 100 : null;
  const lib = L.limitBytes > 0 ? L.usedBytes / L.limitBytes * 100 : null;

  const bots = h.bots.map(b => el('div', { class: 'hbot', 'data-state': b.state },
    el('span', { class: 'nm', 'data-state': b.state }, el('span', { class: 'dot' }), document.createTextNode(b.nickname)),
    el('span', { class: 'muted', text: b.state === 'connected'
      ? [b.server, b.channel, t('listeners', b.listeners)].filter(Boolean).join(' · ')
      : `${stateText(b.state)} · ${b.address}` }),
    b.error && b.state !== 'connected' ? el('span', { class: 'err', text: b.error }) : null));

  box.replaceChildren(
    el('div', { class: 'gauges' },
      gauge('⚡', t('hCpu'), ring('cpu', m.cpuPercent, m.cpuPercent == null ? '—' : null), t('cores', m.cores), t('hMachine'),
        kv(t('hThis'), pct(h.process.cpuPercent)), meter('cpu-panda', h.process.cpuPercent)),
      gauge('🧠', t('hRam'), ring('ram', ram, ram == null ? '—' : null),
        m.memoryTotalBytes ? (ram != null ? t('usage', fmtBytes(m.memoryUsedBytes), fmtBytes(m.memoryTotalBytes)) : fmtBytes(m.memoryTotalBytes)) : '—',
        t('hMachine'), kv(t('hThis'), fmtBytes(h.process.memoryBytes)),
        m.memoryTotalBytes ? meter('ram-panda', h.process.memoryBytes / m.memoryTotalBytes * 100) : null),
      gauge('💾', t('hDisk'), ring('disk', disk, d ? null : '—'),
        d ? `${fmtBytes(d.freeBytes)} ${t('hFree')}` : '—',
        d ? t('hUsed', fmtBytes(d.totalBytes - d.freeBytes), fmtBytes(d.totalBytes)) : '',
        d ? kv(d.path, fmtBytes(d.totalBytes)) : null),
      gauge('📚', t('hLib'), ring('lib', lib, lib == null ? '∞' : null),
        t('requests', L.count), L.enabled ? t('hLibUsed', fmtBytes(L.usedBytes)) : t('hLibOff'),
        kv(t('hLimit'), L.limitBytes > 0 ? fmtBytes(L.limitBytes) : t('hNoLimit')))),
    el('div', { class: 'infos' },
      card('🎧', t('hTs'), ...bots),
      card('🐼', t('hPanda'),
        kv(t('hVersion'), 'v' + h.version),
        kv(t('hUptime'), t('uptime', Math.floor(up / 86400), Math.floor(up / 3600) % 24, Math.floor(up / 60) % 60)),
        kv(t('hRuntime'), h.runtime), kv(t('hOs'), h.os),
        kv('ffmpeg', h.tools.ffmpeg || t('missingTool'), !h.tools.ffmpeg))));
  void box.offsetWidth;   // lay out the old values first so the change animates
  moves.forEach(f => f());
}

/* Logs: the server sends what it has, then new lines in batches. */
function startLogs() {
  stopLogs();
  logEs = new EventSource('/api/system/logs?after=' + logLast);
  logEs.onmessage = ev => { try { addLogs(JSON.parse(ev.data)); } catch {} };
  renderLogs();
}
function stopLogs() { if (logEs) { logEs.close(); logEs = null; } }

function logVisible(e) {
  const q = $('log-q').value.trim().toLowerCase();
  return LEVELS[e.level] >= LEVELS[$('log-level').value] && (!q || `${e.source} ${e.message}`.toLowerCase().includes(q));
}
function logRow(e) {
  const d = new Date(e.time);
  const row = el('div', { class: 'll ' + e.level },
    el('time', { datetime: e.time, title: d.toLocaleString(lang), text: d.toLocaleTimeString(lang, { hour: '2-digit', minute: '2-digit', second: '2-digit' }) }),
    // Upper case in code: CSS would make "info" "İNFO" on a Turkish page.
    el('span', { class: 'lv', text: e.level.toUpperCase() }), el('span', { class: 'src', text: e.source }), el('span', { class: 'msg', text: e.message }));
  row.dataset.seq = e.seq;
  return row;
}
function addLogs(batch) {
  const fresh = batch.filter(e => e.seq > logLast);   // a reconnect sends some lines again
  if (!fresh.length) return;
  logLast = fresh.at(-1).seq;
  logLines.push(...fresh);
  const list = $('log-list');
  if (logLines.length > 2000) {
    logLines.splice(0, logLines.length - 2000);
    while (list.firstElementChild && +list.firstElementChild.dataset.seq < logLines[0].seq) list.firstElementChild.remove();
  }
  list.append(...fresh.filter(logVisible).map(logRow));
  $('log-empty').hidden = list.childElementCount > 0;
  if ($('log-follow').checked) list.scrollTop = list.scrollHeight;
}
function renderLogs() {
  const list = $('log-list');
  list.replaceChildren(...logLines.filter(logVisible).map(logRow));
  $('log-empty').hidden = list.childElementCount > 0 || !logLines.length;
  if ($('log-follow').checked) list.scrollTop = list.scrollHeight;
}
$('log-level').addEventListener('change', renderLogs);
$('log-q').addEventListener('input', debounce(renderLogs, 200));
$('log-follow').addEventListener('change', e => { if (e.target.checked) $('log-list').scrollTop = $('log-list').scrollHeight; });
// Scrolling up to read stops following; scrolling back to the end follows again.
$('log-list').addEventListener('scroll', () => {
  const l = $('log-list');
  $('log-follow').checked = l.scrollHeight - l.scrollTop - l.clientHeight < 30;
}, { passive: true });

/* ---------------- keyboard nicety & start ---------------- */
document.addEventListener('keydown', e => {
  if (e.key === '/' && !$('app').hidden && !e.target.closest('input,textarea,select,dialog')) { e.preventDefault(); $('add-q').focus(); }
});

applyTheme();
applyLang();
boot();
