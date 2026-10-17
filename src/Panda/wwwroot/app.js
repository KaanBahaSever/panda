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
    plFull: (x, n) => `"${x}" is full (500 songs): ${n === 1 ? '1 song' : `${n} songs`} didn't fit.`,
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
    sAnnounce: 'Announce each song in chat', sConfirm: 'Confirm chat commands',
    sChatHint: 'Announcing posts every song as it starts. Confirmations are the short replies to commands like !play and !skip. Questions (!np, !queue, !help) and errors are always answered.', sDesc: 'Show song in bot description', sMaxQueue: 'Max queue length', sMaxDur: 'Max song length (min)',
    sMaxDurHint: '0 = unlimited.', sPerUser: 'Songs per person', sPerUserHint: 'How many songs one person can have waiting in the queue. 0 = no limit. DJs and admins aren\'t limited.', 'err.user-limit': 'You already have as many songs waiting as allowed. Add more when one has played.', sMaxDurShared: 'Max song length is shared by all bots.',
    save: 'Save', reconnect: 'Reconnect bot',
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
    'err.timeout': 'YouTube didn\'t answer in time — try again.', 'err.no-bot': 'That bot no longer exists.',
    library: 'Library', savedBadge: 'saved', fromLibrary: 'From your library', fromYouTube: 'From YouTube', libFilter: 'Filter saved songs',
    usage: (a, b) => `${a} of ${b}`, libOff: 'Saving is off — turn it on in Settings.', libEmpty: 'No saved songs yet. Songs are saved as they are requested.',
    libNoMatch: 'No saved songs match.', libCapped: 'Showing the 500 most played songs — use the filter to find others.', showMore: 'Show more',
    plays: n => n === 1 ? '1 play' : `${n} plays`, requests: n => n === 1 ? '1 song' : `${n} songs`, delete: 'Delete', deleted: 'Deleted', confirmLibDelete: x => `Delete "${x}" from the library?`,
    sLib: 'Library', sLibOn: 'Save every requested song', sLibOnHint: 'When off, only the next two songs are downloaded ahead of time.',
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
    sPerm: "Chat permissions", sEveryone: "Everyone on the server can use the bot", sEveryoneHint: "When off, only DJs and admins can.", sAdmins: "Admins (everything)", sDjs: "DJs", sBlocked: "Blocked", sPeopleHint: "Pick people or server groups from the server, or paste a unique ID. Chat admins and DJs are not the panel's users.", addPerson: "Add", pickTitle: "Add from the server", pickSearch: "Search people and groups", pickGroups: "Server groups", pickPeople: "Online now", pickUid: "Or paste a unique ID", pickOffline: "The bot isn't connected; you can still paste a unique ID.", pickNoGroups: "The bot can't see the server groups (b_virtualserver_servergroup_list).", noOne: "Nobody yet", sOthers: "Who may skip, remove or move other people's songs", lvlEveryone: "Everyone", lvlDj: "DJs and admins", lvlAdmin: "Admins only", sVoteSkip: "The others can vote to skip", sVoteSkipHint: "People who may not skip someone else's song vote with !skip; it's skipped when half of the bot's channel voted. Whoever asked for a song can always skip it, and the panel skips at once.", sOnlyChannel: "Only people in the bot's channel can give commands", sOnlyChannelHint: "Admins can from anywhere; questions like !np are always answered.", sCmdLevels: "Who may use each command", sRecommended: "Use the recommended setup", sRecommendedHint: "Adding songs and voting for everyone; pausing, volume, the queue and the rest for DJs. Save to keep it.", sChecks: "The bot's permissions on the server", sChecksHint: "What the bot can do with its server group's permissions. \"Not tried yet\" fills in as the bot is used.", pcOk: "Works", pcUnknown: "Not tried yet", pcOffline: "The bot isn't connected.", refresh: "Refresh", sKeys: "API keys", sKeysHint: "For programs that use Panda without logging in (a script, a management panel). They send the key as \"Authorization: Bearer <key>\" and get the role's rights.", keyName: "Name", addKey: "Make a key", keyShown: "Copy the key now; it won't be shown again.", copy: "Copy", copied: "Copied", keyMade: "Key made", keyRevoked: "Key revoked", keyNever: "Never used", revoke: "Revoke", "err.bad-key-name": "Key names are 1–40 characters.", "err.too-many-keys": "There are already 50 keys.", "pc.talk": "Be heard in the channel", "pc.chat": "Write in the chat", "pc.description": "Show the song in its description", "pc.move": "Change channels (!join, !follow)", "pc.groups": "See the server groups", "cmd.play": "Add a song", "cmd.search": "Search and pick", "cmd.playnow": "Play right away", "cmd.skip": "Skip (own song, or vote)", "cmd.remove": "Remove from the queue", "cmd.move": "Move in the queue", "cmd.skipto": "Jump to a song", "cmd.back": "Previous song", "cmd.seek": "Wind the song", "cmd.pause": "Pause and resume", "cmd.stop": "Stop and clear", "cmd.clear": "Clear the queue", "cmd.shuffle": "Shuffle", "cmd.loop": "Repeat", "cmd.vol": "Volume", "cmd.radio": "Radio", "cmd.playlist": "Play a saved playlist", "cmd.fav": "Save to Favorites", "cmd.join": "Come to my channel", "cmd.follow": "Follow me",
    pcMissing: x => `Missing: give the bot's server group ${x}.`, pcTalk: (h, n) => `Talk power ${h}, the channel needs ${n}.`, confirmKeyRevoke: x => `Revoke the key "${x}"? Programs using it stop working.`, keyUsed: x => `Last used ${x}`
  },
  tr: {
    loginHint: 'Sevimli TeamSpeak DJ\'in', password: 'Şifre', login: 'Giriş yap', language: 'Dil: Türkçe (Français\'ye geç)',
    theme: 'Karanlık modu aç/kapat', settings: 'Ayarlar', logout: 'Çıkış yap', offline: 'Panda ile bağlantı koptu — yeniden bağlanılıyor…',
    nowPlaying: 'Şimdi çalıyor', live: 'CANLI', napping: 'Panda uyukluyor 💤 — bir şarkı ekle!', seek: 'Sar',
    shuffle: 'Sırayı karıştır', stop: 'Durdur', play: 'Oynat', pause: 'Duraklat', skip: 'Geç', mute: 'Sessize al', unmute: 'Sesi aç', volume: 'Ses',
    loopOff: 'Tekrar: kapalı', loopAll: 'Tekrar: tüm sıra', loopOne: 'Tekrar: bu şarkı',
    radioOn: 'Radyo: açık — sıra bitince benzer şarkılar gelir', radioOff: 'Radyo: kapalı', radioToast: '📻 Radyo açık: sıra bitince benzer şarkılar gelir',
    playlists: 'Çalma listeleri', plEmpty: 'Henüz kayıtlı liste yok.', plSaveQueue: 'Sırayı liste olarak kaydet', plAdd: 'Listeye ekle', plNew: 'Yeni liste…',
    plName: 'Listenin adı:', plPlay: 'Sıraya ekle', plShuffle: 'Karışık ekle', plRename: 'Adını değiştir', plDelete: 'Listeyi sil',
    confirmPlDelete: x => `"${x}" listesi silinsin mi?`, plQueued: (n, x) => `"${x}" listesinden ${n} şarkı eklendi`, plSaved: x => `"${x}" listesine kaydedildi`,
    plFull: (x, n) => `"${x}" dolu (500 şarkı): ${n} şarkı eklenemedi.`,
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
    sAnnounce: 'Her şarkıyı sohbette duyur', sConfirm: 'Sohbet komutlarını onayla',
    sChatHint: 'Duyuru, her şarkı başlarken kanala yazılır. Onay, !çal ve !geç gibi komutlara verilen kısa cevaptır. Sorular (!şimdi, !sıra, !yardım) ve hatalar her zaman cevaplanır.', sDesc: 'Şarkıyı bot açıklamasında göster', sMaxQueue: 'En fazla sıra uzunluğu', sMaxDur: 'En uzun şarkı (dk)',
    sMaxDurHint: '0 = sınırsız.', sPerUser: 'Kişi başı şarkı', sPerUserHint: 'Bir kişinin sırada aynı anda bekleyebilecek şarkı sayısı. 0 = sınır yok. DJ\'ler ve yöneticiler sınırlanmaz.', 'err.user-limit': 'Sırada izin verilen sayıda bekleyen şarkınız var. Biri çalınca yenisini ekleyebilirsiniz.', sMaxDurShared: 'En uzun şarkı süresi tüm botlar için geçerli.',
    save: 'Kaydet', reconnect: 'Botu yeniden bağla',
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
    'err.timeout': 'YouTube zamanında cevap vermedi — tekrar dene.', 'err.no-bot': 'Bu bot artık yok.',
    library: 'Kütüphane', savedBadge: 'kayıtlı', fromLibrary: 'Kütüphanenden', fromYouTube: 'YouTube\'dan', libFilter: 'Kayıtlı şarkılarda ara',
    usage: (a, b) => `${a} / ${b}`, libOff: 'Kaydetme kapalı — Ayarlar\'dan açabilirsin.', libEmpty: 'Henüz kayıtlı şarkı yok. Şarkılar istendikçe kaydedilir.',
    libNoMatch: 'Eşleşen kayıtlı şarkı yok.', libCapped: 'En çok dinlenen 500 şarkı gösteriliyor — diğerleri için aramayı kullan.', showMore: 'Daha fazla göster',
    plays: n => `${n} kez çalındı`, requests: n => `${n} şarkı`, delete: 'Sil', deleted: 'Silindi', confirmLibDelete: x => `"${x}" kütüphaneden silinsin mi?`,
    sLib: 'Kütüphane', sLibOn: 'İstenen her şarkıyı kaydet', sLibOnHint: 'Kapalıyken sadece sıradaki iki şarkı önceden indirilir.',
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
    sPerm: "Sohbet yetkileri", sEveryone: "Sunucudaki herkes botu kullanabilir", sEveryoneHint: "Kapalıyken sadece DJ'ler ve yöneticiler kullanabilir.", sAdmins: "Yöneticiler (her şey)", sDjs: "DJ'ler", sBlocked: "Engellenenler", sPeopleHint: "Sunucudaki kişileri ya da sunucu gruplarını seçin veya benzersiz kimlik yapıştırın. Sohbetteki yöneticiler ve DJ'ler panel kullanıcılarından ayrıdır.", addPerson: "Ekle", pickTitle: "Sunucudan ekle", pickSearch: "Kişi ve grup ara", pickGroups: "Sunucu grupları", pickPeople: "Şu an çevrimiçi", pickUid: "Ya da benzersiz kimlik yapıştırın", pickOffline: "Bot bağlı değil; yine de benzersiz kimlik yapıştırabilirsiniz.", pickNoGroups: "Bot sunucu gruplarını göremiyor (b_virtualserver_servergroup_list).", noOne: "Henüz kimse yok", sOthers: "Başkalarının şarkılarını kim geçebilir, silebilir, taşıyabilir", lvlEveryone: "Herkes", lvlDj: "DJ'ler ve yöneticiler", lvlAdmin: "Sadece yöneticiler", sVoteSkip: "Diğerleri geçmek için oy verebilir", sVoteSkipHint: "Başkasının şarkısını geçemeyenler !geç ile oy verir; botun kanalındakilerin yarısı oy verince şarkı geçilir. Şarkıyı isteyen her zaman geçebilir, panel de hemen geçer.", sOnlyChannel: "Komutları sadece botun kanalındakiler verebilir", sOnlyChannelHint: "Yöneticiler her yerden verebilir; !şimdi gibi sorular her zaman cevaplanır.", sCmdLevels: "Hangi komutu kim kullanabilir", sRecommended: "Önerilen ayarı kullan", sRecommendedHint: "Şarkı ekleme ve oylama herkese; duraklatma, ses, sıra ve gerisi DJ'lere. Kalması için kaydedin.", sChecks: "Botun sunucudaki izinleri", sChecksHint: "Botun, sunucu grubunun izinleriyle neler yapabildiği. \"Henüz denenmedi\" yazanlar bot kullanıldıkça dolar.", pcOk: "Çalışıyor", pcUnknown: "Henüz denenmedi", pcOffline: "Bot bağlı değil.", refresh: "Yenile", sKeys: "API anahtarları", sKeysHint: "Panda'yı giriş yapmadan kullanan programlar için (bir betik, bir yönetim paneli). Anahtarı \"Authorization: Bearer <anahtar>\" olarak gönderirler ve rolün yetkilerini alırlar.", keyName: "Ad", addKey: "Anahtar oluştur", keyShown: "Anahtarı şimdi kopyalayın; bir daha gösterilmeyecek.", copy: "Kopyala", copied: "Kopyalandı", keyMade: "Anahtar oluşturuldu", keyRevoked: "Anahtar iptal edildi", keyNever: "Hiç kullanılmadı", revoke: "İptal et", "err.bad-key-name": "Anahtar adı 1–40 karakter olmalı.", "err.too-many-keys": "Zaten 50 anahtar var.", "pc.talk": "Kanalda duyulmak", "pc.chat": "Sohbete yazmak", "pc.description": "Şarkıyı açıklamasında göstermek", "pc.move": "Kanal değiştirmek (!gel, !takip)", "pc.groups": "Sunucu gruplarını görmek", "cmd.play": "Şarkı ekle", "cmd.search": "Ara ve seç", "cmd.playnow": "Hemen çal", "cmd.skip": "Geç (kendi şarkısı ya da oy)", "cmd.remove": "Sıradan sil", "cmd.move": "Sırada taşı", "cmd.skipto": "Bir şarkıya atla", "cmd.back": "Önceki şarkı", "cmd.seek": "İleri geri sar", "cmd.pause": "Duraklat ve devam", "cmd.stop": "Durdur ve temizle", "cmd.clear": "Sırayı temizle", "cmd.shuffle": "Karıştır", "cmd.loop": "Tekrar", "cmd.vol": "Ses", "cmd.radio": "Radyo", "cmd.playlist": "Kayıtlı listeyi çal", "cmd.fav": "Favorilere kaydet", "cmd.join": "Kanalıma gel", "cmd.follow": "Beni takip et",
    pcMissing: x => `Eksik: botun sunucu grubuna ${x} iznini verin.`, pcTalk: (h, n) => `Konuşma gücü ${h}, kanal ${n} istiyor.`, confirmKeyRevoke: x => `"${x}" anahtarı iptal edilsin mi? Onu kullanan programlar çalışmayı bırakır.`, keyUsed: x => `Son kullanım: ${x}`
  },
  fr: {
    loginHint: 'Votre DJ TeamSpeak douillet', password: 'Mot de passe', login: 'Se connecter', language: 'Langue : français (passer à Deutsch)',
    theme: 'Activer/désactiver le mode sombre', settings: 'Paramètres', logout: 'Se déconnecter', offline: 'Connexion à Panda perdue — reconnexion…',
    nowPlaying: 'En cours de lecture', live: 'EN DIRECT', napping: 'Panda fait la sieste 💤 — ajoutez un morceau !', seek: 'Position',
    shuffle: 'Mélanger la file', stop: 'Arrêter', play: 'Lecture', pause: 'Pause', skip: 'Passer', mute: 'Couper le son', unmute: 'Rétablir le son', volume: 'Volume',
    loopOff: 'Répétition : désactivée', loopAll: 'Répétition : toute la file', loopOne: 'Répétition : ce morceau',
    radioOn: 'Radio : activée — des morceaux similaires suivent quand la file est finie', radioOff: 'Radio : désactivée', radioToast: '📻 Radio activée : des morceaux similaires suivent quand la file est finie',
    playlists: 'Playlists', plEmpty: 'Aucune playlist enregistrée pour l\'instant.', plSaveQueue: 'Enregistrer la file comme playlist', plAdd: 'Ajouter à une playlist', plNew: 'Nouvelle playlist…',
    plName: 'Nom de la playlist :', plPlay: 'Ajouter à la file', plShuffle: 'Ajouter dans le désordre', plRename: 'Renommer', plDelete: 'Supprimer la playlist',
    confirmPlDelete: x => `Supprimer la playlist « ${x} » ?`, plQueued: (n, x) => `${n} morceaux de « ${x} » ajoutés`, plSaved: x => `Enregistré dans « ${x} »`,
    plFull: (x, n) => `« ${x} » est pleine (500 morceaux) : ${n <= 1 ? '1 morceau n\'a pas pu être ajouté' : `${n} morceaux n'ont pas pu être ajoutés`}.`,
    plRenamed: 'Playlist renommée', plDeleted: 'Playlist supprimée', 'err.bad-playlist-name': 'Un nom de playlist fait 1 à 40 caractères, avec au moins une lettre ou un chiffre, et sans barre oblique.',
    'err.playlist-exists': 'Une playlist porte déjà ce nom.',
    addSong: 'Ajouter un morceau', addPh: 'Nom du morceau ou lien YouTube', add: 'Ajouter', playNow: 'Lire maintenant', added: 'Ajouté', results: 'Résultats',
    noResults: 'Aucun résultat 🐾', close: 'Fermer', queue: 'File d\'attente', clear: 'Vider', queueEmpty: 'La file est vide. Cherchez quelque chose ci-dessus !',
    history: 'Écoutés récemment', historyEmpty: 'Rien n\'a encore été joué.', today: 'Aujourd\'hui', yesterday: 'Hier', addAgain: 'Rajouter', remove: 'Retirer', moveUp: 'Monter', moveDown: 'Descendre',
    requestedBy: 'demandé par', songs: n => n <= 1 ? `${n} morceau` : `${n} morceaux`,
    connected: 'Connecté', connecting: 'Connexion…', disconnected: 'Déconnecté',
    raisingId: n => `Passage de la sécurité de l'identité au niveau ${n}…`, listeners: n => n <= 1 ? `${n} auditeur` : `${n} auditeurs`,
    sConn: 'Connexion', sConnHint: 'Modifier ces champs reconnecte le bot.', sAddress: 'Adresse du serveur', sServerPw: 'Mot de passe du serveur',
    sChannel: 'Canal', sChannelHint: 'Un chemin comme « Musique/Salon » ou un id comme « /5 ». Vide = canal par défaut.', sChannelPw: 'Mot de passe du canal',
    sNick: 'Pseudo', removePw: 'Supprimer le mot de passe', unchanged: 'inchangé', sBot: 'Bot', sLang: 'Langue du chat', sPrefix: 'Préfixe des commandes',
    sAnnounce: 'Annoncer chaque morceau dans le chat', sConfirm: 'Confirmer les commandes du chat',
    sChatHint: 'L\'annonce publie chaque morceau quand il commence. Les confirmations sont les réponses courtes aux commandes comme !jouer et !passer. Les questions (!encours, !file, !aide) et les erreurs reçoivent toujours une réponse.', sDesc: 'Afficher le morceau dans la description du bot', sMaxQueue: 'Longueur max. de la file', sMaxDur: 'Durée max. d\'un morceau (min)',
    sMaxDurHint: '0 = illimitée.', sPerUser: 'Morceaux par personne', sPerUserHint: 'Combien de morceaux une personne peut avoir en attente dans la file. 0 = pas de limite. Les DJ et les admins ne sont pas limités.', 'err.user-limit': 'Vous avez déjà autant de morceaux en attente que permis. Ajoutez-en quand l\'un d\'eux aura été joué.', sMaxDurShared: 'La durée max. d\'un morceau est commune à tous les bots.',
    save: 'Enregistrer', reconnect: 'Reconnecter le bot',
    sCookies: 'Cookies YouTube', sCookiesHelp: 'Si YouTube demande de « confirmer que vous n\'êtes pas un robot » : ouvrez une fenêtre de navigation privée, connectez-vous à YouTube, exportez les cookies avec une extension « cookies.txt », collez-les ici, puis fermez cette fenêtre.',
    chooseFile: 'Choisir un fichier', saveCookies: 'Enregistrer les cookies', removeCookies: 'Supprimer les cookies', cookiesOn: 'Cookies présents ✓', cookiesOff: 'Aucun cookie',
    sPanelPw: 'Mot de passe du panneau', pwCurrent: 'Mot de passe actuel', pwNew: 'Nouveau mot de passe (6 caractères min.)', pwChange: 'Changer le mot de passe',
    cancel: 'Annuler', ok: 'OK', saved: 'Paramètres enregistrés', reconnecting: 'Enregistré — le bot se reconnecte…', reconnectSent: 'Reconnexion du bot…',
    cookiesSaved: 'Cookies enregistrés 🍪', cookiesRemoved: 'Cookies supprimés', cookiesEmpty: 'Collez ou choisissez d\'abord un cookies.txt', pwChanged: 'Mot de passe changé',
    confirmStop: 'Arrêter la lecture et vider la file ?', confirmClear: 'Vider toute la file ?', confirmCookies: 'Supprimer les cookies YouTube enregistrés ?',
    nothing: 'Rien à faire pour le moment', invalid: 'Vérifiez les champs en surbrillance', netError: 'Erreur réseau — Panda est-il lancé ?',
    'err.not-found': 'Aucun morceau trouvé pour cette recherche.', 'err.empty': 'Tapez un nom de morceau ou collez un lien.',
    'err.playlist-empty': 'Cette playlist est vide.', 'err.not-youtube': 'Seuls les liens YouTube fonctionnent.',
    'err.too-long': 'Ce morceau est trop long.', 'err.bot-check': 'YouTube demande une vérification anti-robot — ajoutez des cookies dans les Paramètres.',
    'err.unavailable': 'Cette vidéo n\'est pas disponible.', 'err.age': 'Cette vidéo est soumise à une limite d\'âge — ajoutez des cookies dans les Paramètres.',
    'err.queue-full': 'La file est pleine.', 'err.wrong-password': 'Mot de passe incorrect.', 'err.too-many-attempts': 'Trop de tentatives — patientez quelques minutes.',
    'err.password-too-short': 'Le nouveau mot de passe doit faire au moins 6 caractères.', 'err.cookies-invalid': 'Ça ne ressemble pas à un cookies.txt de YouTube.',
    'err.too-large': 'Ce fichier est trop volumineux.', 'err.missing': 'Outil manquant sur le serveur : ',
    'err.timeout': 'YouTube n\'a pas répondu à temps — réessayez.', 'err.no-bot': 'Ce bot n\'existe plus.',
    library: 'Bibliothèque', savedBadge: 'enregistré', fromLibrary: 'Dans votre bibliothèque', fromYouTube: 'Sur YouTube', libFilter: 'Filtrer les morceaux enregistrés',
    usage: (a, b) => `${a} sur ${b}`, libOff: 'L\'enregistrement est désactivé — activez-le dans les Paramètres.', libEmpty: 'Aucun morceau enregistré pour l\'instant. Les morceaux sont enregistrés à mesure qu\'ils sont demandés.',
    libNoMatch: 'Aucun morceau enregistré ne correspond.', libCapped: 'Affichage des 500 morceaux les plus joués — utilisez le filtre pour trouver les autres.', showMore: 'Afficher plus',
    plays: n => n <= 1 ? `${n} écoute` : `${n} écoutes`, requests: n => n <= 1 ? `${n} morceau` : `${n} morceaux`, delete: 'Supprimer', deleted: 'Supprimé', confirmLibDelete: x => `Supprimer « ${x} » de la bibliothèque ?`,
    sLib: 'Bibliothèque', sLibOn: 'Enregistrer chaque morceau demandé', sLibOnHint: 'Désactivé, seuls les deux prochains morceaux sont téléchargés à l\'avance.',
    sLibMax: 'Taille max. (Go)', sLibMaxHint: 'Une fois pleine, les morceaux les moins joués sont supprimés en premier. 0 = pas de limite.', sLibMaxBad: 'Mettez 0 (pas de limite) ou au moins 0,1 Go.',
    sNorm: 'Jouer les morceaux au même volume', sNormHint: 'Chaque morceau enregistré est mesuré une fois et joué au même niveau sonore. La toute première écoute d\'un morceau, avant son enregistrement, est jouée telle quelle.',
    bots: 'Bots', addBot: 'Ajouter un bot', botAdded: 'Bot ajouté — choisissez maintenant son canal', idle: 'Inactif',
    sPrefixHint: 'Les bots d\'un même canal doivent avoir des préfixes différents, sinon ils répondent tous.', sShared: 'Commun à tous les bots.',
    sRemoveBot: 'Supprimer ce bot', sRemoveBotHint: 'Le déconnecte et supprime son identité TeamSpeak et son historique.', removeBot: 'Supprimer le bot',
    confirmBotRemove: x => `Supprimer « ${x} » ? Son identité et son historique seront effacés.`, botRemoved: 'Bot supprimé',
    userName: 'Nom d\'utilisateur', role: 'Rôle', roleAdmin: 'Admin', roleDj: 'Peut seulement ajouter des morceaux', sUsers2: 'Utilisateurs du panneau',
    sUsersHint2: 'Les admins peuvent tout faire. Les utilisateurs « Peut seulement ajouter des morceaux » peuvent regarder et ajouter des morceaux, rien d\'autre.',
    addUser: 'Ajouter un utilisateur', userAdded: 'Utilisateur ajouté', userRemoved: 'Utilisateur supprimé', roleChanged: 'Rôle modifié', you: '(vous)',
    confirmUserRemove: x => `Supprimer l'utilisateur « ${x} » ?`, resetPw: 'Définir un nouveau mot de passe', resetPwFor: x => `Nouveau mot de passe pour « ${x} » (6 caractères min.) :`, pwReset: 'Mot de passe défini',
    forbidden: 'Vous n\'avez pas le droit de faire ça.', 'err.bad-name': 'Un nom d\'utilisateur fait 2 à 32 caractères : lettres, chiffres, « . », « _ » ou « - ».',
    'err.user-exists': 'Ce nom d\'utilisateur est déjà pris.', 'err.bad-role': 'Rôle inconnu.', 'err.last-admin': 'Il doit rester au moins un admin.',
    'err.self': 'Vous ne pouvez pas vous supprimer vous-même.',
    system: 'Système', health: 'État', logs: 'Journaux', logLevel: 'Niveau', lvlAll: 'Tout', lvlWarn: 'Avertissements et erreurs', lvlError: 'Erreurs seulement',
    logSearch: 'Rechercher dans le journal', logFollow: 'Suivre', download: 'Télécharger', logNote: 'Les 2000 dernières lignes depuis le démarrage de Panda.', logEmpty: 'Aucune ligne ne correspond.',
    hPanda: 'Panda', hVersion: 'Version', hUptime: 'En marche depuis', hRuntime: 'Environnement', hOs: 'Système', hTs: 'TeamSpeak', hCpu: 'Processeur', hRam: 'Mémoire',
    hDisk: 'Disque', hThis: 'Panda', hMachine: 'Tout le serveur', hFree: 'libre', hLib: 'Bibliothèque',
    hUsed: (a, b) => `${a} sur ${b} utilisés`, hLibUsed: a => `${a} sur le disque`, hLibOff: 'Enregistrement désactivé', hLimit: 'Limite', hNoLimit: 'aucune', missingTool: 'introuvable',
    updates: 'Mises à jour', updCheck: 'Vérifier', updChecked: x => `Dernière vérification : ${x}`, updNotes: 'Nouveautés',
    updUnreachable: 'Impossible de joindre GitHub ; réessayez plus tard.', updRelease: 'Page de la version',
    updInstalled: 'Installée', updNewest: 'La plus récente', updOk: 'À jour', updNew: 'Nouvelle version',
    updPandaWhat: 'Le bot lui-même. Vos morceaux, l\'historique, les playlists et les paramètres sont conservés ; la file reprend après le redémarrage.',
    updYtWhat: 'L\'outil qui récupère les morceaux sur YouTube. YouTube change souvent ; le serveur le met aussi à jour chaque jour.',
    updNow: 'Mettre à jour Panda', updStarted: 'Mise à jour… Panda redémarre dans une ou deux minutes.', updDone: x => `Panda est passé en v${x}. Rechargement…`,
    updSlow: 'Panda n\'est pas encore revenu ; consultez les journaux sur le serveur.',
    updFailed: 'La mise à jour n\'a pas abouti (GitHub a peut-être eu un souci). Panda continue de tourner comme avant ; réessayez dans une minute.',
    'updHow.docker': 'Docker : dans le dossier de Panda, lancez « git pull && docker compose up -d --build ».',
    'updHow.systemd-old': 'Relancez une fois l\'installateur sur le serveur (la commande du README) ; ensuite vous pourrez mettre à jour d\'ici.',
    'updHow.manual': 'Téléchargez la nouvelle version sur GitHub et remplacez le programme.',
    ytUpdate: 'Mettre à jour yt-dlp', ytBusy: 'Mise à jour… cela peut prendre une minute.', ytDone: (a, b) => `Mis à jour : ${a || '?'} → ${b || '?'}`,
    ytCurrent: x => `Déjà la dernière version (${x || '?'}).`, ytPending: 'Le serveur est encore en train de le mettre à jour ; appuyez sur Vérifier dans une minute.',
    'err.ytdlp-update-failed': 'Impossible de mettre à jour yt-dlp ; consultez les journaux.',
    'err.cannot-update': 'Cette installation ne peut pas se mettre à jour elle-même.',
    cores: n => n <= 1 ? `${n} cœur` : `${n} cœurs`, uptime: (d, h, m) => d ? `${d} j ${h} h` : h ? `${h} h ${m} min` : `${m} min`,
    stats: 'Statistiques', statsRange: 'Période', last7: '7 derniers jours', last30: '30 derniers jours', last365: 'Dernière année', allTime: 'Depuis le début',
    thisBot: 'Ce bot', allBots: 'Tous les bots', statsEmpty: 'Rien n\'a été joué sur cette période.', sPlays: 'écoutes', sListened: 'd\'écoute',
    sSongs: 'morceaux différents', sPeople: 'personnes', topSongs: 'Les plus joués', topPeople: 'Ceux qui en demandent le plus', activity: 'Activité', byHour: 'Par heure de la journée',
    sPerm: "Droits dans le chat", sEveryone: "Tout le monde sur le serveur peut utiliser le bot", sEveryoneHint: "Désactivé, seuls les DJ et les admins le peuvent.", sAdmins: "Admins (tout)", sDjs: "DJ", sBlocked: "Bloqués", sPeopleHint: "Choisissez des personnes ou des groupes du serveur, ou collez un identifiant unique. Les admins et DJ du chat ne sont pas les utilisateurs du panneau.", addPerson: "Ajouter", pickTitle: "Ajouter depuis le serveur", pickSearch: "Chercher des personnes et des groupes", pickGroups: "Groupes du serveur", pickPeople: "En ligne", pickUid: "Ou collez un identifiant unique", pickOffline: "Le bot n'est pas connecté ; vous pouvez quand même coller un identifiant unique.", pickNoGroups: "Le bot ne voit pas les groupes du serveur (b_virtualserver_servergroup_list).", noOne: "Personne pour l'instant", sOthers: "Qui peut passer, retirer ou déplacer les morceaux des autres", lvlEveryone: "Tout le monde", lvlDj: "DJ et admins", lvlAdmin: "Admins seulement", sVoteSkip: "Les autres peuvent voter pour passer", sVoteSkipHint: "Ceux qui ne peuvent pas passer le morceau d'un autre votent avec !passer ; il est passé quand la moitié du canal du bot a voté. Celui qui l'a demandé peut toujours le passer, et le panneau le passe tout de suite.", sOnlyChannel: "Seules les personnes dans le canal du bot peuvent donner des commandes", sOnlyChannelHint: "Les admins le peuvent de partout ; les questions comme !encours ont toujours une réponse.", sCmdLevels: "Qui peut utiliser chaque commande", sRecommended: "Utiliser la configuration recommandée", sRecommendedHint: "Ajouter des morceaux et voter pour tout le monde ; pause, volume, file et le reste pour les DJ. Enregistrez pour la garder.", sChecks: "Droits du bot sur le serveur", sChecksHint: "Ce que le bot peut faire avec les droits de son groupe. « Pas encore essayé » se remplit à l'usage.", pcOk: "Fonctionne", pcUnknown: "Pas encore essayé", pcOffline: "Le bot n'est pas connecté.", refresh: "Actualiser", sKeys: "Clés d'API", sKeysHint: "Pour les programmes qui utilisent Panda sans se connecter (un script, un panneau de gestion). Ils envoient la clé en « Authorization: Bearer <clé> » et ont les droits du rôle.", keyName: "Nom", addKey: "Créer une clé", keyShown: "Copiez la clé maintenant ; elle ne sera plus affichée.", copy: "Copier", copied: "Copié", keyMade: "Clé créée", keyRevoked: "Clé révoquée", keyNever: "Jamais utilisée", revoke: "Révoquer", "err.bad-key-name": "Un nom de clé fait 1 à 40 caractères.", "err.too-many-keys": "Il y a déjà 50 clés.", "pc.talk": "Être entendu dans le canal", "pc.chat": "Écrire dans le chat", "pc.description": "Afficher le morceau dans sa description", "pc.move": "Changer de canal (!viens, !suivre)", "pc.groups": "Voir les groupes du serveur", "cmd.play": "Ajouter un morceau", "cmd.search": "Chercher et choisir", "cmd.playnow": "Lire tout de suite", "cmd.skip": "Passer (son morceau, ou voter)", "cmd.remove": "Retirer de la file", "cmd.move": "Déplacer dans la file", "cmd.skipto": "Aller à un morceau", "cmd.back": "Morceau précédent", "cmd.seek": "Avancer ou reculer", "cmd.pause": "Pause et reprise", "cmd.stop": "Arrêter et vider", "cmd.clear": "Vider la file", "cmd.shuffle": "Mélanger", "cmd.loop": "Répétition", "cmd.vol": "Volume", "cmd.radio": "Radio", "cmd.playlist": "Lire une playlist enregistrée", "cmd.fav": "Enregistrer dans les Favoris", "cmd.join": "Viens dans mon canal", "cmd.follow": "Suis-moi",
    pcMissing: x => `Manquant : donnez ${x} au groupe du bot.`, pcTalk: (h, n) => `Puissance de parole ${h}, le canal demande ${n}.`, confirmKeyRevoke: x => `Révoquer la clé « ${x} » ? Les programmes qui l'utilisent cesseront de fonctionner.`, keyUsed: x => `Dernière utilisation : ${x}`
  },
  de: {
    loginHint: 'Dein gemütlicher TeamSpeak-DJ', password: 'Passwort', login: 'Anmelden', language: 'Sprache: Deutsch (zu English wechseln)',
    theme: 'Dunkelmodus an/aus', settings: 'Einstellungen', logout: 'Abmelden', offline: 'Verbindung zu Panda verloren — verbinde neu…',
    nowPlaying: 'Läuft gerade', live: 'LIVE', napping: 'Panda macht ein Nickerchen 💤 — füg einen Song hinzu!', seek: 'Position',
    shuffle: 'Warteschlange mischen', stop: 'Stopp', play: 'Abspielen', pause: 'Pause', skip: 'Überspringen', mute: 'Stummschalten', unmute: 'Ton an', volume: 'Lautstärke',
    loopOff: 'Wiederholung: aus', loopAll: 'Wiederholung: ganze Warteschlange', loopOne: 'Wiederholung: dieser Song',
    radioOn: 'Radio: an — ähnliche Songs folgen, wenn die Warteschlange leer ist', radioOff: 'Radio: aus', radioToast: '📻 Radio an: ähnliche Songs folgen, wenn die Warteschlange leer ist',
    playlists: 'Playlists', plEmpty: 'Noch keine gespeicherten Playlists.', plSaveQueue: 'Warteschlange als Playlist speichern', plAdd: 'Zu Playlist hinzufügen', plNew: 'Neue Playlist…',
    plName: 'Name der Playlist:', plPlay: 'Zur Warteschlange hinzufügen', plShuffle: 'Gemischt hinzufügen', plRename: 'Umbenennen', plDelete: 'Playlist löschen',
    confirmPlDelete: x => `Playlist „${x}“ löschen?`, plQueued: (n, x) => `${n} Songs aus „${x}“ hinzugefügt`, plSaved: x => `In „${x}“ gespeichert`,
    plFull: (x, n) => `„${x}“ ist voll (500 Songs): ${n === 1 ? '1 Song hat' : `${n} Songs haben`} nicht mehr reingepasst.`,
    plRenamed: 'Playlist umbenannt', plDeleted: 'Playlist gelöscht', 'err.bad-playlist-name': 'Playlist-Namen haben 1–40 Zeichen, mindestens einen Buchstaben oder eine Ziffer und keine Schrägstriche.',
    'err.playlist-exists': 'Es gibt schon eine Playlist mit diesem Namen.',
    addSong: 'Song hinzufügen', addPh: 'Songname oder YouTube-Link', add: 'Hinzufügen', playNow: 'Sofort abspielen', added: 'Hinzugefügt', results: 'Ergebnisse',
    noResults: 'Keine Ergebnisse 🐾', close: 'Schließen', queue: 'Warteschlange', clear: 'Leeren', queueEmpty: 'Die Warteschlange ist leer. Such oben nach etwas!',
    history: 'Zuletzt gespielt', historyEmpty: 'Noch nichts gespielt.', today: 'Heute', yesterday: 'Gestern', addAgain: 'Nochmal hinzufügen', remove: 'Entfernen', moveUp: 'Nach oben', moveDown: 'Nach unten',
    requestedBy: 'gewünscht von', songs: n => n === 1 ? '1 Song' : `${n} Songs`,
    connected: 'Verbunden', connecting: 'Verbinde…', disconnected: 'Getrennt',
    raisingId: n => `Erhöhe die Sicherheitsstufe der Identität auf ${n}…`, listeners: n => n === 1 ? '1 Zuhörer' : `${n} Zuhörer`,
    sConn: 'Verbindung', sConnHint: 'Änderungen hier verbinden den Bot neu.', sAddress: 'Serveradresse', sServerPw: 'Serverpasswort',
    sChannel: 'Channel', sChannelHint: 'Ein Pfad wie „Musik/Lounge“ oder eine ID wie „/5“. Leer = Standard-Channel.', sChannelPw: 'Channel-Passwort',
    sNick: 'Nickname', removePw: 'Passwort entfernen', unchanged: 'unverändert', sBot: 'Bot', sLang: 'Chat-Sprache', sPrefix: 'Befehlspräfix',
    sAnnounce: 'Jeden Song im Chat ankündigen', sConfirm: 'Chat-Befehle bestätigen',
    sChatHint: 'Ankündigungen posten jeden Song, wenn er beginnt. Bestätigungen sind die kurzen Antworten auf Befehle wie !spielen und !überspringen. Fragen (!läuft, !warteschlange, !hilfe) und Fehler werden immer beantwortet.', sDesc: 'Song in der Bot-Beschreibung zeigen', sMaxQueue: 'Max. Länge der Warteschlange', sMaxDur: 'Max. Songlänge (Min.)',
    sMaxDurHint: '0 = unbegrenzt.', sPerUser: 'Songs pro Person', sPerUserHint: 'Wie viele Songs eine Person gleichzeitig in der Warteschlange haben darf. 0 = kein Limit. DJs und Admins sind nicht begrenzt.', 'err.user-limit': 'Du hast schon so viele Songs in der Warteschlange wie erlaubt. Füg neue hinzu, wenn einer gespielt wurde.', sMaxDurShared: 'Die max. Songlänge gilt für alle Bots.',
    save: 'Speichern', reconnect: 'Bot neu verbinden',
    sCookies: 'YouTube-Cookies', sCookiesHelp: 'Wenn YouTube „Bestätige, dass du kein Bot bist“ sagt: Öffne ein privates Fenster, melde dich bei YouTube an, exportiere die Cookies mit einer „cookies.txt“-Browsererweiterung, füge sie hier ein und schließ das Fenster.',
    chooseFile: 'Datei wählen', saveCookies: 'Cookies speichern', removeCookies: 'Cookies entfernen', cookiesOn: 'Cookies gesetzt ✓', cookiesOff: 'Keine Cookies',
    sPanelPw: 'Panel-Passwort', pwCurrent: 'Aktuelles Passwort', pwNew: 'Neues Passwort (min. 6)', pwChange: 'Passwort ändern',
    cancel: 'Abbrechen', ok: 'OK', saved: 'Einstellungen gespeichert', reconnecting: 'Gespeichert — der Bot verbindet sich neu…', reconnectSent: 'Bot verbindet sich neu…',
    cookiesSaved: 'Cookies gespeichert 🍪', cookiesRemoved: 'Cookies entfernt', cookiesEmpty: 'Füge zuerst eine cookies.txt ein oder wähle eine aus', pwChanged: 'Passwort geändert',
    confirmStop: 'Wiedergabe stoppen und Warteschlange leeren?', confirmClear: 'Die ganze Warteschlange leeren?', confirmCookies: 'Die gespeicherten YouTube-Cookies entfernen?',
    nothing: 'Gerade gibt es nichts zu tun', invalid: 'Bitte prüf die markierten Felder', netError: 'Netzwerkfehler — läuft Panda?',
    'err.not-found': 'Für diese Suche wurde kein Song gefunden.', 'err.empty': 'Gib einen Songnamen ein oder füg einen Link ein.',
    'err.playlist-empty': 'Diese Playlist ist leer.', 'err.not-youtube': 'Nur YouTube-Links funktionieren.',
    'err.too-long': 'Dieser Song ist zu lang.', 'err.bot-check': 'YouTube will eine Bot-Prüfung — füg in den Einstellungen Cookies hinzu.',
    'err.unavailable': 'Dieses Video ist nicht verfügbar.', 'err.age': 'Dieses Video hat eine Altersbeschränkung — füg in den Einstellungen Cookies hinzu.',
    'err.queue-full': 'Die Warteschlange ist voll.', 'err.wrong-password': 'Falsches Passwort.', 'err.too-many-attempts': 'Zu viele Versuche — warte ein paar Minuten.',
    'err.password-too-short': 'Das neue Passwort braucht mindestens 6 Zeichen.', 'err.cookies-invalid': 'Das sieht nicht nach einer YouTube-cookies.txt aus.',
    'err.too-large': 'Die Datei ist zu groß.', 'err.missing': 'Fehlendes Programm auf dem Server: ',
    'err.timeout': 'YouTube hat nicht rechtzeitig geantwortet — versuch es nochmal.', 'err.no-bot': 'Diesen Bot gibt es nicht mehr.',
    library: 'Bibliothek', savedBadge: 'gespeichert', fromLibrary: 'Aus deiner Bibliothek', fromYouTube: 'Von YouTube', libFilter: 'Gespeicherte Songs filtern',
    usage: (a, b) => `${a} von ${b}`, libOff: 'Speichern ist aus — schalte es in den Einstellungen ein.', libEmpty: 'Noch keine gespeicherten Songs. Songs werden gespeichert, sobald sie gewünscht werden.',
    libNoMatch: 'Keine gespeicherten Songs passen.', libCapped: 'Die 500 meistgespielten Songs werden gezeigt — nutz den Filter für die anderen.', showMore: 'Mehr zeigen',
    plays: n => n === 1 ? '1-mal gespielt' : `${n}-mal gespielt`, requests: n => n === 1 ? '1 Song' : `${n} Songs`, delete: 'Löschen', deleted: 'Gelöscht', confirmLibDelete: x => `„${x}“ aus der Bibliothek löschen?`,
    sLib: 'Bibliothek', sLibOn: 'Jeden gewünschten Song speichern', sLibOnHint: 'Wenn aus, werden nur die nächsten zwei Songs vorab heruntergeladen.',
    sLibMax: 'Größenlimit (GB)', sLibMaxHint: 'Wenn voll, werden die am wenigsten gespielten Songs zuerst gelöscht. 0 = kein Limit.', sLibMaxBad: 'Gib 0 (kein Limit) oder mindestens 0,1 GB ein.',
    sNorm: 'Songs gleich laut abspielen', sNormHint: 'Jeder gespeicherte Song wird einmal gemessen und gleich laut abgespielt. Das allererste Abspielen eines Songs, bevor er gespeichert ist, läuft unverändert.',
    bots: 'Bots', addBot: 'Bot hinzufügen', botAdded: 'Bot hinzugefügt — leg jetzt seinen Channel fest', idle: 'Bereit',
    sPrefixHint: 'Bots im selben Channel brauchen verschiedene Präfixe, sonst antworten alle.', sShared: 'Gilt für alle Bots.',
    sRemoveBot: 'Diesen Bot entfernen', sRemoveBotHint: 'Trennt ihn und löscht seine TeamSpeak-Identität und seinen Verlauf.', removeBot: 'Bot entfernen',
    confirmBotRemove: x => `„${x}“ entfernen? Seine Identität und sein Verlauf werden gelöscht.`, botRemoved: 'Bot entfernt',
    userName: 'Benutzername', role: 'Rolle', roleAdmin: 'Admin', roleDj: 'Darf nur Songs hinzufügen', sUsers2: 'Panel-Nutzer',
    sUsersHint2: 'Admins dürfen alles. Nutzer mit „Darf nur Songs hinzufügen“ können zusehen und Songs hinzufügen, sonst nichts.',
    addUser: 'Nutzer hinzufügen', userAdded: 'Nutzer hinzugefügt', userRemoved: 'Nutzer entfernt', roleChanged: 'Rolle geändert', you: '(du)',
    confirmUserRemove: x => `Den Nutzer „${x}“ entfernen?`, resetPw: 'Neues Passwort setzen', resetPwFor: x => `Neues Passwort für „${x}“ (min. 6):`, pwReset: 'Passwort gesetzt',
    forbidden: 'Das darfst du nicht.', 'err.bad-name': 'Benutzernamen haben 2–32 Buchstaben, Ziffern, „.“, „_“ oder „-“.',
    'err.user-exists': 'Dieser Benutzername ist schon vergeben.', 'err.bad-role': 'Unbekannte Rolle.', 'err.last-admin': 'Es muss mindestens einen Admin geben.',
    'err.self': 'Du kannst dich nicht selbst entfernen.',
    system: 'System', health: 'Status', logs: 'Protokoll', logLevel: 'Stufe', lvlAll: 'Alle', lvlWarn: 'Warnungen und Fehler', lvlError: 'Nur Fehler',
    logSearch: 'Im Protokoll suchen', logFollow: 'Mitlaufen', download: 'Herunterladen', logNote: 'Die letzten 2000 Zeilen seit dem Start von Panda.', logEmpty: 'Keine passenden Zeilen.',
    hPanda: 'Panda', hVersion: 'Version', hUptime: 'Läuft seit', hRuntime: 'Laufzeitumgebung', hOs: 'System', hTs: 'TeamSpeak', hCpu: 'CPU', hRam: 'Arbeitsspeicher',
    hDisk: 'Festplatte', hThis: 'Panda', hMachine: 'Ganzer Server', hFree: 'frei', hLib: 'Bibliothek',
    hUsed: (a, b) => `${a} von ${b} belegt`, hLibUsed: a => `${a} auf der Festplatte`, hLibOff: 'Speichern ist aus', hLimit: 'Limit', hNoLimit: 'keins', missingTool: 'nicht gefunden',
    updates: 'Updates', updCheck: 'Prüfen', updChecked: x => `Zuletzt geprüft: ${x}`, updNotes: 'Was ist neu',
    updUnreachable: 'GitHub war nicht erreichbar; versuch es später nochmal.', updRelease: 'Release-Seite',
    updInstalled: 'Installiert', updNewest: 'Neueste', updOk: 'Aktuell', updNew: 'Neue Version',
    updPandaWhat: 'Der Bot selbst. Deine Songs, der Verlauf, die Playlists und Einstellungen bleiben; die Warteschlange geht nach dem Neustart weiter.',
    updYtWhat: 'Das Programm, das Songs von YouTube holt. YouTube ändert sich oft; der Server aktualisiert es auch täglich selbst.',
    updNow: 'Panda aktualisieren', updStarted: 'Aktualisiere… Panda startet in ein, zwei Minuten neu.', updDone: x => `Panda wurde auf v${x} aktualisiert. Lade neu…`,
    updSlow: 'Panda ist noch nicht zurück; schau dir das Protokoll auf dem Server an.',
    updFailed: 'Das Update hat nicht geklappt (vielleicht hatte GitHub einen Aussetzer). Panda läuft weiter wie vorher; versuch es in einer Minute nochmal.',
    'updHow.docker': 'Docker: Führ im Panda-Ordner „git pull && docker compose up -d --build“ aus.',
    'updHow.systemd-old': 'Führ das Installationsskript einmal erneut auf dem Server aus (der Befehl aus der README); danach kannst du von hier aus aktualisieren.',
    'updHow.manual': 'Lade das neue Release von GitHub herunter und ersetze das Programm.',
    ytUpdate: 'yt-dlp aktualisieren', ytBusy: 'Aktualisiere… das kann eine Minute dauern.', ytDone: (a, b) => `Aktualisiert: ${a || '?'} → ${b || '?'}`,
    ytCurrent: x => `Schon die neueste Version (${x || '?'}).`, ytPending: 'Der Server aktualisiert es noch; drück in einer Minute auf Prüfen.',
    'err.ytdlp-update-failed': 'yt-dlp konnte nicht aktualisiert werden; schau ins Protokoll.',
    'err.cannot-update': 'Diese Installation kann sich nicht selbst aktualisieren.',
    cores: n => n === 1 ? '1 Kern' : `${n} Kerne`, uptime: (d, h, m) => d ? `${d} T. ${h} Std.` : h ? `${h} Std. ${m} Min.` : `${m} Min.`,
    stats: 'Statistiken', statsRange: 'Zeitraum', last7: 'Letzte 7 Tage', last30: 'Letzte 30 Tage', last365: 'Letztes Jahr', allTime: 'Gesamte Zeit',
    thisBot: 'Dieser Bot', allBots: 'Alle Bots', statsEmpty: 'In diesem Zeitraum wurde nichts gespielt.', sPlays: 'Wiedergaben', sListened: 'gehört',
    sSongs: 'verschiedene Songs', sPeople: 'Personen', topSongs: 'Meistgespielt', topPeople: 'Wer am meisten gewünscht hat', activity: 'Aktivität', byHour: 'Nach Tageszeit',
    sPerm: "Rechte im Chat", sEveryone: "Alle auf dem Server können den Bot benutzen", sEveryoneHint: "Wenn aus, nur DJs und Admins.", sAdmins: "Admins (alles)", sDjs: "DJs", sBlocked: "Gesperrt", sPeopleHint: "Wähl Leute oder Servergruppen vom Server oder füg eine Unique-ID ein. Admins und DJs im Chat sind nicht die Nutzer des Panels.", addPerson: "Hinzufügen", pickTitle: "Vom Server hinzufügen", pickSearch: "Leute und Gruppen suchen", pickGroups: "Servergruppen", pickPeople: "Gerade online", pickUid: "Oder eine Unique-ID einfügen", pickOffline: "Der Bot ist nicht verbunden; eine Unique-ID kannst du trotzdem einfügen.", pickNoGroups: "Der Bot sieht die Servergruppen nicht (b_virtualserver_servergroup_list).", noOne: "Noch niemand", sOthers: "Wer Songs anderer überspringen, entfernen oder verschieben darf", lvlEveryone: "Alle", lvlDj: "DJs und Admins", lvlAdmin: "Nur Admins", sVoteSkip: "Die anderen können fürs Überspringen abstimmen", sVoteSkipHint: "Wer Songs anderer nicht überspringen darf, stimmt mit !überspringen ab; der Song wird übersprungen, wenn die Hälfte der Leute im Channel des Bots abgestimmt hat. Wer ihn gewünscht hat, kann ihn immer überspringen, und das Panel überspringt sofort.", sOnlyChannel: "Nur Leute im Channel des Bots können Befehle geben", sOnlyChannelHint: "Admins können es von überall; Fragen wie !läuft werden immer beantwortet.", sCmdLevels: "Wer welchen Befehl benutzen darf", sRecommended: "Empfohlene Einstellung verwenden", sRecommendedHint: "Songs hinzufügen und abstimmen für alle; Pause, Lautstärke, Warteschlange und der Rest für DJs. Zum Behalten speichern.", sChecks: "Rechte des Bots auf dem Server", sChecksHint: "Was der Bot mit den Rechten seiner Servergruppe kann. „Noch nicht versucht“ füllt sich, während der Bot benutzt wird.", pcOk: "Geht", pcUnknown: "Noch nicht versucht", pcOffline: "Der Bot ist nicht verbunden.", refresh: "Aktualisieren", sKeys: "API-Schlüssel", sKeysHint: "Für Programme, die Panda ohne Anmeldung benutzen (ein Skript, ein Verwaltungspanel). Sie senden den Schlüssel als „Authorization: Bearer <Schlüssel>“ und bekommen die Rechte der Rolle.", keyName: "Name", addKey: "Schlüssel erstellen", keyShown: "Kopier den Schlüssel jetzt; er wird nicht noch einmal gezeigt.", copy: "Kopieren", copied: "Kopiert", keyMade: "Schlüssel erstellt", keyRevoked: "Schlüssel widerrufen", keyNever: "Noch nie benutzt", revoke: "Widerrufen", "err.bad-key-name": "Schlüsselnamen haben 1–40 Zeichen.", "err.too-many-keys": "Es gibt schon 50 Schlüssel.", "pc.talk": "Im Channel zu hören sein", "pc.chat": "Im Chat schreiben", "pc.description": "Den Song in der Beschreibung zeigen", "pc.move": "Den Channel wechseln (!komm, !folgen)", "pc.groups": "Die Servergruppen sehen", "cmd.play": "Song hinzufügen", "cmd.search": "Suchen und wählen", "cmd.playnow": "Sofort abspielen", "cmd.skip": "Überspringen (eigener Song oder abstimmen)", "cmd.remove": "Aus der Warteschlange entfernen", "cmd.move": "In der Warteschlange verschieben", "cmd.skipto": "Zu einem Song springen", "cmd.back": "Voriger Song", "cmd.seek": "Spulen", "cmd.pause": "Pause und weiter", "cmd.stop": "Stoppen und leeren", "cmd.clear": "Warteschlange leeren", "cmd.shuffle": "Mischen", "cmd.loop": "Wiederholen", "cmd.vol": "Lautstärke", "cmd.radio": "Radio", "cmd.playlist": "Gespeicherte Playlist abspielen", "cmd.fav": "In Favoriten speichern", "cmd.join": "Komm in meinen Channel", "cmd.follow": "Folge mir",
    pcMissing: x => `Fehlt: gib der Servergruppe des Bots ${x}.`, pcTalk: (h, n) => `Sprechkraft ${h}, der Channel braucht ${n}.`, confirmKeyRevoke: x => `Den Schlüssel „${x}“ widerrufen? Programme, die ihn benutzen, funktionieren dann nicht mehr.`, keyUsed: x => `Zuletzt benutzt: ${x}`
  },
};

/* ---------------- helpers ---------------- */
const $ = id => document.getElementById(id);
const store = { get: k => { try { return localStorage.getItem(k); } catch { return null; } },
                set: (k, v) => { try { localStorage.setItem(k, v); } catch {} } };
const LANGS = Object.keys(I18N);
const savedLang = store.get('panda.lang');
let lang = LANGS.includes(savedLang) ? savedLang : LANGS.find(l => (navigator.language || '').toLowerCase().startsWith(l)) || 'en';
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
  if (shownResults[0]) showResults(...shownResults);
  if ($('system').open) renderUpdate();
}

document.querySelectorAll('.js-theme').forEach(b => b.addEventListener('click', () => {
  store.set('panda.theme', document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark'); applyTheme();
}));
document.querySelectorAll('.js-lang').forEach(b => b.addEventListener('click', () => {
  lang = LANGS[(LANGS.indexOf(lang) + 1) % LANGS.length]; store.set('panda.lang', lang); applyLang();
}));

/* ---------------- login / logout ---------------- */
function showLogin() {
  stopStream();
  searchSeq++; showResults(null);
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
const pageVersion = new URL(document.currentScript?.src || location.href).searchParams.get('v');
async function loadBot() {
  // Updated meanwhile (from another tab, or on the server): load the new panel once.
  const v = await api('GET', '/api/version').catch(() => null);
  if (v && pageVersion && v.version !== pageVersion && store.get('panda.reloadedFor') !== v.version) {
    store.set('panda.reloadedFor', v.version);
    location.reload();
    return new Promise(() => {});
  }
  const m = await api('GET', '/api/me');
  if (m.role !== me.role) lastQueueSig = lastHistorySig = lastBotsSig = '';   // rows have role-specific buttons
  me = m; applyRole();
  try { setState(await api('GET', B('/state'))); }
  catch (e) { if (e.status !== 404) throw e; await pickBot(); setState(await api('GET', B('/state'))); }   // it was removed
  if ($('settings').open && settings && settingsBot !== botId) $('settings').close();
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
  if ($('settings').open) $('settings').close();
  lastQueueSig = lastHistorySig = ''; shownTrackId = null;
  stopStream(); backoff = 1000; recover();
  if (statsBox.open) loadStats();
}

/* ---------------- rendering ---------------- */
function render() { renderBot(); renderNow(); renderQueue(); renderPlaylists(); renderHistory(); renderLib(); watchPlays(); }

/** "All bots" statistics change when any bot finishes a song, not only the one on screen. */
let lastPlaysSig = '';
function watchPlays() {
  const s = JSON.stringify(S.bots.map(b => [b.id, b.played]));
  if (lastPlaysSig && s !== lastPlaysSig && manyBots() && $('stats-bot').value === 'all') reloadStats();
  lastPlaysSig = s;
}

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
  act(() => api('POST', B('/loop'), { mode: next })).then(r => { if (r === undefined) resync(); });
});
$('btn-radio').addEventListener('click', () => {
  const on = !S.player.radio;
  S.player.radio = on; renderNow();                         // optimistic
  act(() => api('POST', B('/radio'), { on }), on ? t('radioToast') : null).then(r => { if (r === undefined) resync(); });
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
let shownResults = [null];
function showResults(saved, yt) {
  shownResults = [saved, yt];
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
    // A short tick on the row (the toast says the rest); a lasting one stuck on lists that rarely redraw.
    const li = btn.closest('li');
    if (r && li) { li.classList.add('done'); setTimeout(() => li.classList.remove('done'), 2500); }
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
  const key = `${L.count}|${L.usedBytes}|${L.plays}`;   // plays: the counts and the order change
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
    if (e.status !== 409 && e.status !== 401) toast(errText(e), 'err');
    resync();   // the queue changed meanwhile (409) or the edit failed: show what is really there
  }
}
/** Takes the server's state again, after an optimistic change it didn't accept. */
async function resync() { const s = await api('GET', B('/state')).catch(() => null); if (s) setState(s); }

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
// The bot the sheet was opened for: saving, reconnecting and removing go to it, even if the panel
// meanwhile switched to another bot (say, because another admin removed this one).
let settingsBot = '';
const SB = path => `/api/bots/${encodeURIComponent(settingsBot)}${path}`;
const F = () => $('set-form').elements;

/** Settings of the bot shown in the panel (plus the shared ones). Others only get their password. */
async function openSettings() {
  if (!$('settings').open) $('settings').showModal();
  $('set-bot').textContent = isAdmin() ? botName() : '';
  const v = api('GET', '/api/version').then(v => { $('version').textContent = 'v' + v.version; }).catch(() => {});
  if (isAdmin()) {
    settingsBot = botId; settings = null;   // nothing to save until this bot's settings are in
    const s = await act(() => api('GET', SB('/settings')));
    if (s && settingsBot === botId) fillSettings(s);
    loadUsers();
    loadKeys();
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
async function loadKeys() {
  const list = await act(() => api('GET', '/api/apikeys'));
  if (!list) return;
  $('key-list').replaceChildren(...list.map(k => {
    const del = el('button', { class: 'btn ghost sm danger', type: 'button' }, el('span', { text: t('revoke') }));
    del.addEventListener('click', async () => {
      if (!(await confirmBox(t('confirmKeyRevoke', k.name)))) return;
      const ok = await act(() => api('DELETE', '/api/apikeys/' + encodeURIComponent(k.id)).then(() => true), t('keyRevoked'));
      if (ok) loadKeys();
    });
    return el('li', {}, el('span', { class: 'nm', text: k.name }),
      el('span', { class: 'when', text: k.lastUsed ? t('keyUsed', playedAt(k.lastUsed)) : t('keyNever') }),
      el('span', { class: 'chip', text: t(k.role === 'admin' ? 'roleAdmin' : 'roleDj') }), del);
  }));
}
$('key-form').addEventListener('submit', async e => {
  e.preventDefault();
  const f = e.target.elements;
  await busy(e.submitter || e.target.querySelector('[type=submit]'), async () => {
    const k = await act(() => api('POST', '/api/apikeys', { name: f.name.value.trim(), role: f.role.value }), t('keyMade'));
    if (!k) return;
    e.target.reset();
    $('key-secret').textContent = k.secret;
    $('key-new').hidden = false;
    loadKeys();
  });
});
$('key-copy').addEventListener('click', async () => {
  try { await navigator.clipboard.writeText($('key-secret').textContent); toast(t('copied')); }
  catch { getSelection().selectAllChildren($('key-secret')); }
});

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
    const ok = await act(() => api('DELETE', SB('')).then(() => true), `${t('botRemoved')}: ${name}`);
    if (!ok) return;
    $('settings').close();
    stopStream(); lastQueueSig = lastHistorySig = ''; shownTrackId = null;
    await act(pickBot);
    recover();
  });
});
// Typed passwords and cookies don't stay in the form for the next opening (or the next user).
$('settings').addEventListener('close', () => {
  const pw = $('pw-form').elements;
  pw.current.value = pw.new.value = '';
  $('user-form').reset();
  $('cookie-text').value = '';
  // A new API key is shown once.
  $('key-secret').textContent = ''; $('key-new').hidden = true; $('key-form').reset();
  if ($('picker').open) $('picker').close();
});
$('settings').addEventListener('click', e => {
  if (e.target === $('settings') || e.target.closest('[data-close]')) $('settings').close();   // backdrop / close button
});

function fillSettings(s) {
  settings = s; pwTouched = {};
  $('set-bot').textContent = s.nickname;
  const f = F();
  for (const k of ['serverAddress', 'channel', 'nickname', 'commandPrefix', 'language', 'maxQueueLength', 'maxDurationMinutes', 'maxSongsPerUser']) f[k].value = s[k] ?? '';
  f.announce.checked = !!s.announce; f.confirmCommands.checked = s.confirmCommands !== false; f.voteSkip.checked = !!s.voteSkip;
  f.showSongInDescription.checked = !!s.showSongInDescription;
  f.everyoneCanUse.checked = s.everyoneCanUse !== false; f.onlyFromChannel.checked = !!s.onlyFromChannel;
  f.othersSongs.value = s.othersSongs || 'everyone';
  fillPerm(s);
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
    announce: f.announce.checked, confirmCommands: f.confirmCommands.checked, voteSkip: f.voteSkip.checked, showSongInDescription: f.showSongInDescription.checked,
    maxQueueLength: +f.maxQueueLength.value, maxDurationMinutes: +f.maxDurationMinutes.value, maxSongsPerUser: +f.maxSongsPerUser.value,
    everyoneCanUse: f.everyoneCanUse.checked, onlyFromChannel: f.onlyFromChannel.checked, othersSongs: f.othersSongs.value,
    admins: perm.admins, adminGroups: perm.adminGroups, djs: perm.djs, djGroups: perm.djGroups, blocked: perm.blocked,
    commandLevels: perm.commandLevels,
    libraryEnabled: f.libraryEnabled.checked, libraryMaxSizeMb: Math.round(gb * 1024), normalizeVolume: f.normalizeVolume.checked,
  };
  const old = settings;
  const reconnects = body.serverAddress !== old.serverAddress || body.channel !== old.channel || body.nickname !== old.nickname ||
    body.serverPassword !== null || body.channelPassword !== null;
  await busy($('set-save'), async () => {
    const s = await act(() => api('PUT', SB('/settings'), body), t(reconnects ? 'reconnecting' : 'saved'));
    if (s) fillSettings(s);
  });
});
$('set-reconnect').addEventListener('click', e => busy(e.currentTarget, () => act(() => api('POST', SB('/reconnect')), t('reconnectSent'))));

/* ---------------- chat permissions ---------------- */
let perm = null;       // the lists being edited in the sheet; saved with the other settings
let tsPeople = null;   // who is on the bot's server, for the picker and the group names
const PERM_LISTS = { admins: 'adminGroups', djs: 'djGroups', blocked: null };
const CHAT_LEVELS = ['everyone', 'dj', 'admin'];
const lvlKey = v => ({ everyone: 'lvlEveryone', dj: 'lvlDj', admin: 'lvlAdmin' })[v];
// Same as ChatPermissions.Recommended on the server.
const RECOMMENDED = { play: 'everyone', search: 'everyone', skip: 'everyone', remove: 'everyone', playlist: 'everyone', fav: 'everyone',
  playnow: 'dj', move: 'dj', skipto: 'dj', back: 'dj', seek: 'dj', pause: 'dj', stop: 'dj', clear: 'dj', shuffle: 'dj', loop: 'dj',
  vol: 'dj', radio: 'dj', join: 'dj', follow: 'dj' };

function fillPerm(s) {
  perm = {
    admins: [...(s.admins || [])], adminGroups: [...(s.adminGroups || [])], djs: [...(s.djs || [])], djGroups: [...(s.djGroups || [])],
    blocked: [...(s.blocked || [])], commandLevels: { ...(s.commandLevels || {}) },
  };
  renderPerm();
  loadPeople();
  loadChecks();
}
async function loadPeople() {
  const bot = settingsBot;
  const p = await api('GET', SB('/ts/people')).catch(() => null);
  if (bot !== settingsBot) return;
  tsPeople = p;
  renderPerm();
}
const groupName = id => tsPeople?.groups?.find(g => g.id === id)?.name || `#${id}`;

function renderPerm() {
  if (!perm) return;
  for (const [list, groups] of Object.entries(PERM_LISTS)) {
    const items = [...perm[list].map(p => ({ p })), ...(groups ? perm[groups].map(id => ({ id })) : [])];
    document.querySelector(`.people[data-list="${list}"] .pchips`).replaceChildren(...(items.length
      ? items.map(it => permChip(list, groups, it)) : [el('li', { class: 'none', text: t('noOne') })]));
  }
  $('cmd-levels').replaceChildren(...Object.keys(perm.commandLevels).map(cmd => {
    const sel = el('select', { 'aria-label': `!${cmd}` }, ...CHAT_LEVELS.map(v => el('option', { value: v, text: t(lvlKey(v)) })));
    sel.value = perm.commandLevels[cmd];
    sel.addEventListener('change', () => { perm.commandLevels[cmd] = sel.value; });
    return el('label', { class: 'cmd-row' }, el('code', { text: '!' + cmd }), el('span', { class: 'desc', text: t('cmd.' + cmd) }), sel);
  }));
}
function permChip(list, groups, it) {
  const label = it.id != null ? `👥 ${groupName(it.id)}` : `👤 ${it.p.name || it.p.uid}`;
  const x = el('button', { class: 'x', type: 'button', title: t('remove'), 'aria-label': `${t('remove')}: ${label}` }, icon('x'));
  x.addEventListener('click', () => {
    if (it.id != null) perm[groups] = perm[groups].filter(g => g !== it.id);
    else perm[list] = perm[list].filter(p => p.uid !== it.p.uid);
    renderPerm();
  });
  return el('li', { title: it.id != null ? `#${it.id}` : it.p.uid }, el('span', { text: label }), x);
}

// The picker: people online and server groups of the bot's server, or a unique id typed in.
let pickFor = null;
document.querySelectorAll('.js-pick').forEach(b => b.addEventListener('click', async () => {
  const box = b.closest('.people');
  pickFor = { list: box.dataset.list, groups: box.dataset.groups || null };
  $('picker-search').value = ''; $('picker-uid').value = '';
  renderPicker();
  $('picker').showModal();
  await loadPeople();
  if ($('picker').open) renderPicker();
}));
function addPicked(item) {
  if (!pickFor || !perm) return;
  if (item.id != null) { if (!perm[pickFor.groups].includes(item.id)) perm[pickFor.groups].push(item.id); }
  else if (!perm[pickFor.list].some(p => p.uid === item.uid)) perm[pickFor.list].push({ uid: item.uid, name: item.name || '' });
  renderPerm();
  renderPicker();
}
function renderPicker() {
  const q = $('picker-search').value.trim().toLowerCase();
  const match = s => !q || s.toLowerCase().includes(q);
  const out = [];
  if (!tsPeople?.connected) out.push(el('p', { class: 'empty', text: t('pickOffline') }));
  else {
    if (pickFor?.groups) {
      out.push(el('h4', { text: t('pickGroups') }));
      const gs = (tsPeople.groups || []).filter(g => match(g.name) && !perm[pickFor.groups].includes(g.id));
      if (!tsPeople.groups?.length) out.push(el('p', { class: 'empty', text: t('pickNoGroups') }));
      for (const g of gs) {
        const b = el('button', { type: 'button' }, el('span', { text: `👥 ${g.name}` }), el('small', { text: `#${g.id}` }));
        b.addEventListener('click', () => addPicked({ id: g.id }));
        out.push(b);
      }
    }
    out.push(el('h4', { text: t('pickPeople') }));
    const ps = (tsPeople.people || []).filter(p => (match(p.name) || match(p.uid)) && !perm[pickFor.list].some(x => x.uid === p.uid));
    if (!ps.length) out.push(el('p', { class: 'empty', text: t('noOne') }));
    for (const p of ps) {
      const b = el('button', { type: 'button' }, el('span', { text: `👤 ${p.name}` }), el('small', { text: p.channel || p.uid }));
      b.addEventListener('click', () => addPicked({ uid: p.uid, name: p.name }));
      out.push(b);
    }
  }
  $('picker-list').replaceChildren(...out);
}
$('picker-search').addEventListener('input', renderPicker);
$('picker-uid-add').addEventListener('click', () => {
  const uid = $('picker-uid').value.trim();
  if (uid.length < 4 || uid.length > 100) return;
  addPicked({ uid, name: tsPeople?.people?.find(p => p.uid === uid)?.name || '' });
  $('picker-uid').value = '';
});
$('picker-uid').addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); $('picker-uid-add').click(); } });
$('cmd-recommended').addEventListener('click', () => {
  if (!perm) return;
  Object.assign(perm.commandLevels, RECOMMENDED);
  F().othersSongs.value = 'dj'; F().voteSkip.checked = true;
  renderPerm();
  toast(t('sRecommendedHint'));
});
// A vote is for the people below "other people's songs"; with everyone allowed there'd be nobody to vote.
F().voteSkip.addEventListener('change', e => { if (e.target.checked && F().othersSongs.value === 'everyone') F().othersSongs.value = 'dj'; });

// What the bot may do on the server.
async function loadChecks() {
  const bot = settingsBot;
  const list = await api('GET', SB('/ts/permissions')).catch(() => null);
  if (bot !== settingsBot) return;
  if (!list?.length) return $('perm-checks').replaceChildren(el('li', {}, el('span', { class: 'mark', text: '⚪' }), el('span', { class: 'what', text: t('pcOffline') })));
  $('perm-checks').replaceChildren(...list.map(c => {
    const [have, need] = (c.detail || '').split('/');
    const why = c.ok === true ? (c.key === 'talk' && +need > 0 ? t('pcTalk', have, need) : t('pcOk'))
      : c.ok === false ? (c.key === 'talk' ? `${t('pcTalk', have, need)} ${t('pcMissing', c.permission)}` : t('pcMissing', c.permission))
      : t('pcUnknown');
    return el('li', { class: c.ok === false ? 'bad' : '' },
      el('span', { class: 'mark', text: c.ok === true ? '✅' : c.ok === false ? '⛔' : '⚪' }),
      el('span', { class: 'what', text: t('pc.' + c.key) }), el('span', { class: 'why', text: why }));
  }));
}
$('checks-refresh').addEventListener('click', e => busy(e.currentTarget, loadChecks));

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
  busy(btn, () => act(() => api('POST', B('/playlist'), { name, shuffle }), r => t('plQueued', r.added, name) + (r.limited ? ' — ' + t('err.user-limit') : '')));
}
async function renamePlaylist(name) {
  const v = await promptBox(t('plName'), { type: 'text', value: name });
  if (v == null || v.trim() === name) return;
  const ok = await act(() => api('PUT', plUrl(name), { name: v.trim() }).then(() => true), t('plRenamed'));
  if (ok && openPl === name) openPl = v.trim();
}
async function deletePlaylist(name) {
  if (await confirmBox(t('confirmPlDelete', name))) act(() => api('DELETE', plUrl(name)), t('plDeleted'));
}
/** Adds songs to a playlist (made when it doesn't exist yet). */
const saveToPlaylist = (name, body) => act(() => api('POST', '/api/playlists', { name, ...body }), r => r.left ? t('plFull', r.name, r.left) : t('plSaved', r.name));

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
      el('span', { text: typeof msg.text === 'function' ? msg.text() : msg.text }));
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
  updMsg.panda = { kind: 'busy', text: () => t('updStarted') }; renderUpdate();
  try { await api('POST', '/api/update'); }
  catch (e) { updMsg.panda = { kind: 'err', text: () => errText(e) }; return renderUpdate(); }
  await waitForUpgrade(updateInfo?.current);
}

/** The installer replaces Panda and restarts it; waits for the new version, then loads the new panel. */
async function waitForUpgrade(from) {
  if (upgradeWatch) return;
  upgradeWatch = true;
  updMsg.panda = { kind: 'busy', text: () => t('updStarted') }; renderUpdate();
  try {
    // Up to 16 minutes while the installer runs (systemd stops it after 15), 5 without any answer.
    for (let i = 0, idle = 0, gone = 0; i < 192 && gone < 60; i++) {
      await new Promise(r => setTimeout(r, 5000));
      // Fails while Panda restarts; the new Panda answers with its version.
      const p = await api('GET', '/api/update/progress').catch(() => null);
      if (!p) { gone++; continue; }
      gone = 0;
      if (p.version !== from) {
        updMsg.panda = { kind: 'ok', text: () => t('updDone', p.version) }; renderUpdate();
        setTimeout(() => location.reload(), 2000); return;
      }
      // "idle" twice in a row: the installer finished without bringing a new Panda.
      idle = p.state === 'idle' ? idle + 1 : 0;
      if (p.state === 'failed' || idle >= 2) {
        updMsg.panda = { kind: 'err', text: () => t('updFailed') }; renderUpdate(); return;
      }
    }
    updMsg.panda = { kind: 'err', text: () => t('updSlow') }; renderUpdate();
  } finally { upgradeWatch = false; }
}

async function updateYtDlp() {
  if (updMsg.yt?.kind === 'busy') return;
  updMsg.yt = { kind: 'busy', text: () => t('ytBusy') }; renderUpdate();
  try {
    const r = await api('POST', '/api/update/yt-dlp');
    updMsg.yt = r.result === 'updated' || r.result === 'installed' ? { kind: 'ok', text: () => t('ytDone', r.from, r.to) }
      : r.result === 'current' ? { kind: 'ok', text: () => t('ytCurrent', r.to) }
      : r.result === 'pending' ? { kind: 'busy', text: () => t('ytPending') }
      : r.result === 'reinstall' ? { kind: 'err', text: () => t('updHow.systemd-old') }
      : { kind: 'err', text: () => t('err.ytdlp-update-failed') };
  } catch (e) { updMsg.yt = { kind: 'err', text: () => errText(e) }; }
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
  // The browser retries by itself, but not after an error page (a proxy's 502 while Panda restarts).
  const mine = logEs;
  logEs.onerror = () => { if (mine.readyState === EventSource.CLOSED) setTimeout(() => { if (logEs === mine) startLogs(); }, 3000); };
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
