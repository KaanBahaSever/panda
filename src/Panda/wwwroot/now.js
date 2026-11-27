'use strict';
// What the bots play, for anyone (when the panel's admins turned this page on); no names of who asked.
const fmt = s => { s = Math.max(0, Math.floor(s)); const m = Math.floor(s / 60) % 60, h = Math.floor(s / 3600); return (h ? h + ':' + String(m).padStart(2, '0') : m) + ':' + String(s % 60).padStart(2, '0'); };
const safe = u => { try { const x = new URL(u, location.href); return /^https?:$/.test(x.protocol) ? x.href : null; } catch { return null; } };
function el(tag, cls, text) { const e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; }
async function load() {
  const box = document.getElementById('bots');
  let r;
  try { r = await fetch('api/public/now', { cache: 'no-store' }); } catch { return; }
  if (r.status === 404) { box.replaceChildren(el('p', 'off card', 'This page is turned off.')); return; }
  if (!r.ok) return;
  const bots = await r.json();
  box.replaceChildren(...bots.map(b => {
    const card = el('section', 'card');
    card.append(el('div', 'bot', b.nickname));
    if (!b.title) { card.append(el('p', 'idle', 'Nothing is playing right now.')); return card; }
    const now = el('div', 'now');
    const img = el('img'); img.alt = ''; img.referrerPolicy = 'no-referrer'; img.src = safe(b.thumbnail) || 'img/avatar.png';
    img.onerror = () => { img.onerror = null; img.src = 'img/avatar.png'; };
    const text = el('div');
    text.append(el('div', 'title', b.title), el('div', 'meta', [b.streamTitle, b.channel].filter(Boolean).join(' · ')));
    if (b.live) text.append(el('span', 'live', 'LIVE'));
    else {
      const bar = el('div', 'bar'), fill = el('span'); fill.style.width = Math.min(100, b.position / Math.max(1, b.duration) * 100) + '%';
      bar.append(fill); text.append(bar, el('div', 'meta', `${fmt(b.position)} / ${fmt(b.duration)}`));
    }
    now.append(img, text); card.append(now);
    if (b.next.length) { const ol = el('ol'); for (const n of b.next) ol.append(el('li', null, n)); card.append(ol); }
    return card;
  }));
}
load(); setInterval(load, 5000);
