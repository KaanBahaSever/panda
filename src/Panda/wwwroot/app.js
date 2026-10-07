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
    addSong: 'Add a song', addPh: 'Song name or YouTube link', add: 'Add', playNow: 'Play now', added: 'Added', results: 'Results',
    noResults: 'No results 🐾', close: 'Close', queue: 'Queue', clear: 'Clear', queueEmpty: 'The queue is empty. Search for something above!',
    history: 'Recently played', historyEmpty: 'Nothing played yet.', addAgain: 'Add again', remove: 'Remove', moveUp: 'Move up', moveDown: 'Move down',
    requestedBy: 'requested by', songs: n => n === 1 ? '1 song' : `${n} songs`,
    connected: 'Connected', connecting: 'Connecting…', disconnected: 'Disconnected', listeners: n => n === 1 ? '1 listener' : `${n} listeners`,
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
    'err.playlist': 'Playlists aren\'t supported — send a single song link.', 'err.not-youtube': 'Only YouTube links work.',
    'err.too-long': 'That song is too long.', 'err.bot-check': 'YouTube wants a bot check — add cookies in Settings.',
    'err.unavailable': 'That video is unavailable.', 'err.age': 'That video is age-restricted — add cookies in Settings.',
    'err.queue-full': 'The queue is full.', 'err.wrong-password': 'Wrong password.', 'err.too-many-attempts': 'Too many attempts — wait a few minutes.',
    'err.password-too-short': 'The new password needs at least 6 characters.', 'err.cookies-invalid': 'That doesn\'t look like a YouTube cookies.txt.',
    'err.too-large': 'That file is too large.', 'err.missing': 'Missing tool on the server: ',
  },
  tr: {
    loginHint: 'Sevimli TeamSpeak DJ\'in', password: 'Şifre', login: 'Giriş yap', language: 'Dil: Türkçe (English\'e geç)',
    theme: 'Karanlık modu aç/kapat', settings: 'Ayarlar', logout: 'Çıkış yap', offline: 'Panda ile bağlantı koptu — yeniden bağlanılıyor…',
    nowPlaying: 'Şimdi çalıyor', live: 'CANLI', napping: 'Panda uyukluyor 💤 — bir şarkı ekle!', seek: 'Sar',
    shuffle: 'Sırayı karıştır', stop: 'Durdur', play: 'Oynat', pause: 'Duraklat', skip: 'Geç', mute: 'Sessize al', unmute: 'Sesi aç', volume: 'Ses',
    loopOff: 'Tekrar: kapalı', loopAll: 'Tekrar: tüm sıra', loopOne: 'Tekrar: bu şarkı',
    addSong: 'Şarkı ekle', addPh: 'Şarkı adı ya da YouTube linki', add: 'Ekle', playNow: 'Hemen çal', added: 'Eklendi', results: 'Sonuçlar',
    noResults: 'Sonuç yok 🐾', close: 'Kapat', queue: 'Sıra', clear: 'Temizle', queueEmpty: 'Sıra boş. Yukarıdan bir şey ara!',
    history: 'Son çalınanlar', historyEmpty: 'Henüz bir şey çalınmadı.', addAgain: 'Tekrar ekle', remove: 'Kaldır', moveUp: 'Yukarı taşı', moveDown: 'Aşağı taşı',
    requestedBy: 'İsteyen:', songs: n => `${n} şarkı`,
    connected: 'Bağlı', connecting: 'Bağlanıyor…', disconnected: 'Bağlı değil', listeners: n => `${n} dinleyici`,
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
    'err.playlist': 'Çalma listeleri desteklenmiyor — tek bir şarkı linki gönderin.', 'err.not-youtube': 'Sadece YouTube linkleri çalışır.',
    'err.too-long': 'Bu şarkı çok uzun.', 'err.bot-check': 'YouTube bot doğrulaması istiyor — Ayarlar\'dan çerez ekleyin.',
    'err.unavailable': 'Bu video kullanılamıyor.', 'err.age': 'Bu video yaş sınırlı — Ayarlar\'dan çerez ekleyin.',
    'err.queue-full': 'Sıra dolu.', 'err.wrong-password': 'Şifre yanlış.', 'err.too-many-attempts': 'Çok fazla deneme — birkaç dakika bekleyin.',
    'err.password-too-short': 'Yeni şifre en az 6 karakter olmalı.', 'err.cookies-invalid': 'Bu bir YouTube cookies.txt dosyasına benzemiyor.',
    'err.too-large': 'Dosya çok büyük.', 'err.missing': 'Sunucuda eksik araç: ',
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
  while ($('toasts').children.length > 3) $('toasts').firstElementChild.remove();   // keep the stack short
  setTimeout(() => { box.classList.add('out'); setTimeout(() => box.remove(), 300); }, kind === 'err' ? 6000 : 3200);
}
function confirmBox(msg) {
  const d = $('confirm');
  $('confirm-msg').textContent = msg;
  d.returnValue = '';
  d.showModal();
  return new Promise(res => d.addEventListener('close', () => res(d.returnValue === 'yes'), { once: true }));
}

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
  lastQueueSig = lastHistorySig = '';
  if (S) render();
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
  $('login-pw').focus();
}
function showApp() { $('login').hidden = true; $('app').hidden = false; }

$('login-form').addEventListener('submit', async e => {
  e.preventDefault();
  const btn = e.submitter || $('login-form').querySelector('button[type=submit]');
  $('login-err').textContent = '';
  await busy(btn, async () => {
    try {
      await api('POST', '/api/login', { password: $('login-pw').value });
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
  es = new EventSource('/api/events');
  es.onmessage = ev => { backoff = 1000; try { setState(JSON.parse(ev.data)); } catch {} };
  es.onerror = () => { stopStream(); retryTimer = setTimeout(recover, backoff); backoff = Math.min(backoff * 2, 30000); };
}
function stopStream() { if (es) { es.close(); es = null; } clearTimeout(retryTimer); }
/** After a stream error: check the session (401 → login), then reopen the stream. */
async function recover() {
  try { setState(await api('GET', '/api/state')); startStream(); }
  catch (e) { if (e.status !== 401) { $('offline').hidden = false; retryTimer = setTimeout(recover, backoff); backoff = Math.min(backoff * 2, 30000); } }
}
// Watchdog: the server sends at least every 15 s; reconnect if it has gone quiet.
setInterval(() => { if (es && Date.now() - lastMsg > 40000) { lastMsg = Date.now(); stopStream(); recover(); } }, 5000);

async function boot() {
  try { setState(await api('GET', '/api/state')); showApp(); startStream(); }
  catch (e) { if (e.status !== 401) { showApp(); $('offline').hidden = false; retryTimer = setTimeout(recover, backoff); } }
}

/* ---------------- rendering ---------------- */
function render() { renderBot(); renderNow(); renderQueue(); renderHistory(); }

function renderBot() {
  const b = S.bot, st = $('status');
  st.dataset.state = b.state;
  let text;
  if (b.state === 'connected') text = [b.serverName, b.channelName, t('listeners', b.listeners)].filter(Boolean).join(' · ');
  else text = t(b.state === 'connecting' ? 'connecting' : 'disconnected');
  $('status-text').textContent = text;
  st.title = b.error || text;
  $('status-err').hidden = !(b.error && b.state !== 'connected');
  $('status-err').textContent = b.error || '';
}

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
const ctl = (name, msg) => act(() => api('POST', '/api/control/' + name), msg);
$('btn-toggle').addEventListener('click', () => ctl('toggle'));
$('btn-skip').addEventListener('click', () => ctl('skip'));
$('btn-shuffle').addEventListener('click', () => ctl('shuffle', '🔀'));
$('btn-stop').addEventListener('click', async () => { if (await confirmBox(t('confirmStop'))) ctl('stop'); });
$('btn-clear').addEventListener('click', async () => { if (await confirmBox(t('confirmClear'))) ctl('clear'); });
$('btn-loop').addEventListener('click', () => {
  const next = { Off: 'All', All: 'One', One: 'Off' }[S.player.loop] || 'All';
  S.player.loop = next; renderNow();                       // optimistic
  act(() => api('POST', '/api/loop', { mode: next }));
});

// Seek bar: preview while dragging, commit on change.
let seekDragging = false, seekPending = false;
const commitSeek = debounce(async v => {
  const cur = S.player.current;
  if (cur) { cur.position = v; recvAt = Date.now(); }       // optimistic
  await act(() => api('POST', '/api/seek', { seconds: v }));
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
const sendVolume = debounce(v => act(() => api('POST', '/api/volume', { volume: v })), 200);
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
      const tr = await act(() => api('POST', '/api/play', { query: q }), r => `${t('added')}: ${r.title}`);
      if (tr) $('add-q').value = '';
      return;
    }
    const seq = ++searchSeq;
    const list = await act(() => api('GET', '/api/search?q=' + encodeURIComponent(q)));
    if (list && seq === searchSeq) showResults(list.slice(0, 10));
  });
});
$('results-close').addEventListener('click', closeResults);
function closeResults() { $('results-wrap').hidden = true; $('results').replaceChildren(); }

function showResults(list) {
  const ul = $('results');
  ul.replaceChildren();
  if (!list.length) ul.append(el('li', { class: 'empty muted', text: t('noResults') }));
  for (const tr of list) {
    const add = el('button', { class: 'btn soft sm', type: 'button', title: t('add'), 'aria-label': `${t('add')}: ${tr.title}` }, icon('plus'), el('span', { class: 'lbl', text: t('add') }));
    const now = el('button', { class: 'icon-btn sm', type: 'button', title: t('playNow'), 'aria-label': `${t('playNow')}: ${tr.title}` }, icon('play'));
    add.addEventListener('click', () => addTrack(add, tr, false));
    now.addEventListener('click', () => addTrack(now, tr, true));
    ul.append(el('li', { class: 'track' }, thumb(tr), trackText(tr, false), el('div', { class: 'acts' }, add, now)));
  }
  $('results-wrap').hidden = false;
}
async function addTrack(btn, tr, now) {
  await busy(btn, async () => {
    const r = await act(() => api('POST', '/api/play', { query: tr.url, now }), r => `${now ? t('playNow') : t('added')}: ${r.title}`);
    if (r) btn.closest('li')?.classList.add('done');
  });
}

/** Title (linked), channel · duration · requester. */
function trackText(tr, withReq = true) {
  const href = safeUrl(tr.url);
  const title = el(href ? 'a' : 'span', { class: 'tt', text: tr.title, title: tr.title, href, target: href && '_blank', rel: href && 'noopener noreferrer' });
  const meta = el('span', { class: 'tm' }, el('span', { text: tr.channel }),
    el('span', { class: tr.isLive ? 'badge live sm' : '', text: tr.isLive ? t('live') : fmt(tr.duration) }));
  if (withReq && tr.requestedBy) meta.append(el('span', { class: 'req' }, icon('user', 'ic sm'), document.createTextNode(tr.requestedBy)));
  return el('div', { class: 'tx' }, title, meta);
}

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
  const li = el('li', { class: 'track', draggable: 'true' },
    el('span', { class: 'grip', 'aria-hidden': 'true' }, icon('grip')),
    el('span', { class: 'num', text: i + 1 }),
    thumb(tr), trackText(tr),
    el('div', { class: 'acts' },
      btn('up', t('moveUp'), () => move(i, i - 1), i === 0),
      btn('down', t('moveDown'), () => move(i, i + 1), i === n - 1),
      btn('x', t('remove'), () => remove(i))));
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
  q.splice(to, 0, q.splice(from, 1)[0]); renderQueue();   // optimistic
  act(() => api('POST', '/api/queue/move', { from, to }));
}
function remove(i) {
  S.player.queue.splice(i, 1); renderQueue();              // optimistic
  act(() => api('DELETE', '/api/queue/' + i));
}

/* ---------------- history ---------------- */
function renderHistory() {
  const h = S.player.history;
  $('history-sum').textContent = h.length ? `(${h.length})` : '';
  $('history-empty').hidden = h.length > 0;
  const s = sig(h);
  if (s === lastHistorySig) return;
  lastHistorySig = s;
  $('history').replaceChildren(...h.map(tr => {
    const b = el('button', { class: 'icon-btn sm', type: 'button', title: t('addAgain'), 'aria-label': `${t('addAgain')}: ${tr.title}` }, icon('plus'));
    b.addEventListener('click', () => busy(b, () => act(() => api('POST', '/api/play', { query: tr.url }), r => `${t('added')}: ${r.title}`)));
    return el('li', { class: 'track' }, thumb(tr), trackText(tr), el('div', { class: 'acts' }, b));
  }));
}

/* ---------------- settings ---------------- */
let settings = null, pwTouched = {};
const F = () => $('set-form').elements;

$('open-settings').addEventListener('click', async () => {
  $('settings').showModal();
  const [s, v] = await Promise.all([act(() => api('GET', '/api/settings')), api('GET', '/api/version').catch(() => null)]);
  if (s) fillSettings(s);
  if (v) $('version').textContent = 'v' + v.version;
});
$('settings').addEventListener('click', e => {
  if (e.target === $('settings') || e.target.closest('[data-close]')) $('settings').close();   // backdrop / close button
});

function fillSettings(s) {
  settings = s; pwTouched = {};
  const f = F();
  for (const k of ['serverAddress', 'channel', 'nickname', 'commandPrefix', 'language', 'maxQueueLength', 'maxDurationMinutes']) f[k].value = s[k] ?? '';
  f.announce.checked = !!s.announce; f.showSongInDescription.checked = !!s.showSongInDescription;
  f.allowedUsers.value = (s.allowedUsers || []).join('\n');
  f.allowedServerGroups.value = (s.allowedServerGroups || []).join(', ');
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
  if (!form.checkValidity()) { form.reportValidity(); return toast(t('invalid'), 'err'); }
  const body = {
    ...settings,
    serverAddress: f.serverAddress.value.trim(), serverPassword: pwValue('serverPassword'),
    channel: f.channel.value.trim(), channelPassword: pwValue('channelPassword'),
    nickname: f.nickname.value.trim(), commandPrefix: f.commandPrefix.value.trim(), language: f.language.value,
    announce: f.announce.checked, showSongInDescription: f.showSongInDescription.checked,
    maxQueueLength: +f.maxQueueLength.value, maxDurationMinutes: +f.maxDurationMinutes.value,
    allowedUsers: f.allowedUsers.value.split(/\r?\n/).map(x => x.trim()).filter(Boolean),
    allowedServerGroups: f.allowedServerGroups.value.split(/[,\s;]+/).map(x => x.trim()).filter(x => /^\d+$/.test(x)).map(Number),
  };
  const old = settings;
  const reconnects = body.serverAddress !== old.serverAddress || body.channel !== old.channel || body.nickname !== old.nickname ||
    body.serverPassword !== null || body.channelPassword !== null;
  await busy($('set-save'), async () => {
    const s = await act(() => api('PUT', '/api/settings', body), t(reconnects ? 'reconnecting' : 'saved'));
    if (s) fillSettings(s);
  });
});
$('set-reconnect').addEventListener('click', e => busy(e.currentTarget, () => act(() => api('POST', '/api/reconnect'), t('reconnectSent'))));

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

/* ---------------- keyboard nicety & start ---------------- */
document.addEventListener('keydown', e => {
  if (e.key === '/' && !$('app').hidden && !e.target.closest('input,textarea,select,dialog')) { e.preventDefault(); $('add-q').focus(); }
});

applyTheme();
applyLang();
boot();
