/* NPC Cards - the page, shown as the phone app and as the Side Hustle menu surface.
 *
 * Four tabs: Browse (NPC Vault), Installed, Import (Personnel's drop folder) and Account (sign-in, cards sent from
 * the website, uploads). The host (Host/Calls.cs) answers every call at once; vault work answers later through the
 * "vault.reply" event with the id the page sent. Cards installed now arrive after the next game start, which the
 * amber band says.
 *
 * Written against the Sideload engine, not a browser: every box is a column flexbox, s1.call is synchronous,
 * replaceChildren() takes no arguments, and only Latin-1 glyphs draw.
 */

const host = (typeof window !== 'undefined' && window.npcCardsMock) ? window.npcCardsMock : s1;
const $ = id => document.getElementById(id);

let strings = {};
function t(en, ...args) {
  let s = strings[en] ?? en;
  args.forEach((a, i) => { s = s.split('{' + i + '}').join(String(a)); });
  return s;
}

function el(tag, cls, text) {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (text !== undefined && text !== null) n.textContent = String(text);
  return n;
}

function icon(name, cls) {
  const i = el('img', 'ic' + (cls ? ' ' + cls : ''));
  i.setAttribute('src', 'icons/' + name + '.png');
  return i;
}

function img(cls, src) {
  const i = el('img', cls);
  if (src) i.setAttribute('src', src);
  return i;
}

function button(cls, iconName, text, onClick) {
  const b = el('button', 'btn' + (cls ? ' ' + cls : ''));
  if (iconName) b.appendChild(icon(iconName));
  if (text) b.appendChild(el('span', null, text));
  b.addEventListener('click', () => { if (!b.disabled) onClick(); });
  return b;
}

function clear(node) { node.replaceChildren(); }

const valueOf = e => (e && e.value !== undefined && e.value !== null ? e.value : e && e.target ? e.target.value : '');

function call(name, arg) {
  let raw = '';
  try { raw = host.call(name, arg === undefined ? '' : JSON.stringify(arg)); }
  catch (e) { console.error('[npccards] call ' + name + ' threw: ' + (e && e.message)); }
  if (!raw) { console.error('[npccards] ' + name + ' answered nothing'); return { ok: false, msg: t('Something went wrong. See the log.') }; }
  try { return JSON.parse(raw); }
  catch (e) { console.error('[npccards] ' + name + ' answered something that is not JSON: ' + e.message); return { ok: false, msg: t('Something went wrong. See the log.') }; }
}

function parse(p, what) {
  if (p && typeof p === 'object') return p;
  try { return JSON.parse(p); }
  catch (e) { console.error('[npccards] event ' + what + ' carried something that is not JSON: ' + e.message); return null; }
}

// ---- vault requests: answered through "vault.reply" ------------------------------------------------------------

const reqs = {};
let reqSeq = 0;

function vaultCall(name, arg, cb) {
  const id = 'r' + (++reqSeq);
  reqs[id] = cb;
  const r = call(name, Object.assign({ id }, arg));
  if (!r.ok) { delete reqs[id]; cb(false, r.msg); }
}

host.on('vault.reply', p => {
  const x = parse(p, 'vault.reply');
  if (!x) return;
  const cb = reqs[x.id];
  delete reqs[x.id];
  if (!cb) { console.warn('[npccards] vault reply for unknown request ' + x.id); return; }
  cb(!!x.ok, x.body);
});

// ---- state -----------------------------------------------------------------------------------------------------

const ROLES = [['', 'All'], ['customer', 'Customer'], ['dealer', 'Dealer'], ['supplier', 'Supplier'], ['townsfolk', 'Civilian']];
const SORTS = [['new', 'New'], ['hearts', 'Hearts'], ['downloads', 'Downloads']];
const roleName = r => t((ROLES.find(x => x[0] === r) || ROLES[4])[1]);

const app = {
  where: 'phone',
  tab: 'browse',
  vault: { connected: false, baseUrl: 'https://personify.doomods.com' },
  personifyUrl: '',
  importFolder: '',
  installed: [],
  installedCodes: {},
  next: { arriving: [], leaving: [], changed: [] },
  confirmRemove: null,
};

const browse = { q: '', role: '', sort: 'new', cards: [], next: null, loading: false, error: null, seq: 0 };
const thumbs = {};   // code -> s1:// url, or null after a failed load
const pics = {};     // code -> the <img> elements waiting for that picture
let detail = null;   // { card, info, busy }
let connect = null;  // the last "vault.connect" state while signing in
let uploads = { files: [], busy: null, results: {} };

// ---- toast -----------------------------------------------------------------------------------------------------

let toastSeq = 0;
function toast(text, kind) {
  const n = $('toast');
  n.textContent = text;
  n.className = 'toast show' + (kind ? ' ' + kind : '');
  const mine = ++toastSeq;
  setTimeout(() => { if (mine === toastSeq) n.className = 'toast'; }, 4200);
}

// ---- header, tabs and the band ------------------------------------------------------------------------------------

const TABS = [['browse', 'Browse', 'search'], ['installed', 'Installed', 'card'], ['import', 'Import', 'import'], ['account', 'Account', 'user']];

// Landscape shows the tabs in the header, portrait in their own row under it; both are drawn, CSS picks one.
function drawTabs() {
  drawTabsInto($('tabs'));
  drawTabsInto($('tabs-under'));
}

function drawTabsInto(box) {
  clear(box);
  for (const [id, label, ic] of TABS) {
    const b = el('button', 'tab' + (app.tab === id ? ' on' : ''));
    b.appendChild(icon(ic));
    b.appendChild(el('span', null, t(label)));
    if (id === 'installed' && app.installed.length) b.appendChild(el('span', 'count', app.installed.length));
    b.addEventListener('click', () => show(id));
    box.appendChild(b);
  }
}

function count(list, one, many) { return list.length === 1 ? t(one) : t(many, list.length); }

function drawBand() {
  const band = $('band');
  clear(band);
  const n = app.next;
  const parts = [];
  if (n.arriving.length) parts.push(count(n.arriving, '1 NPC arrives after the next game start.', '{0} NPCs arrive after the next game start.'));
  if (n.leaving.length) parts.push(count(n.leaving, '1 NPC leaves after the next game start.', '{0} NPCs leave after the next game start.'));
  if (n.changed.length) parts.push(count(n.changed, '1 NPC changes after the next game start.', '{0} NPCs change after the next game start.'));
  band.className = parts.length ? 'band' : 'band hidden';
  if (!parts.length) return;
  band.appendChild(icon('restart'));
  band.appendChild(el('div', 'grow', parts.join(' ')));
}

// The Siesta tip counts the NPCs of the next game start, so it appears with the install that reaches the threshold.
let siestaTip = null;

function drawTip() {
  const tip = $('tip');
  clear(tip);
  const npcs = siestaTip && siestaTip.offer ? siestaTip.npcs + app.next.arriving.length - app.next.leaving.length : 0;
  const on = siestaTip && siestaTip.offer && npcs >= siestaTip.threshold;
  tip.className = on ? 'band tip' : 'band tip hidden';
  if (!on) return;
  tip.appendChild(el('div', 'grow', t('{0} NPCs can slow your game down. Siesta helps.', npcs)));
  const get = el('button', 'btn small');
  get.appendChild(icon('link'));
  get.appendChild(el('span', null, t('Get Siesta')));
  get.addEventListener('click', () => openUrl(siestaTip.url));
  tip.appendChild(get);
  const close = el('button', 'btn ghost small square');
  close.appendChild(icon('close'));
  close.addEventListener('click', () => {
    const r = call('siesta.close');
    if (!r.ok) { toast(r.msg, 'bad'); return; }
    siestaTip.offer = false;
    drawTip();
  });
  tip.appendChild(close);
}

function show(tab) {
  app.tab = tab;
  app.confirmRemove = null;
  for (const [id] of TABS) $('pane-' + id).className = 'pane' + (id === tab ? '' : ' hidden');
  drawTabs();
  if (tab === 'browse' && !browse.cards.length && !browse.loading) search();
  else if (tab === 'browse') drawBrowse();
  if (tab === 'installed') loadInstalled();
  if (tab === 'import') loadImport();
  if (tab === 'account') loadAccount();
}

// ---- installed list (also feeds the band, the tab count and the "Installed" badges) -------------------------------

function loadInstalled() {
  const r = call('cards.installed');
  if (!r.ok) { toast(r.msg, 'bad'); return; }
  app.installed = r.cards || [];
  app.installedCodes = {};
  for (const c of app.installed) app.installedCodes[c.code] = true;
  if (r.next) app.next = r.next;
  drawBand();
  drawTip();
  drawTabs();
  if (app.tab === 'installed') drawInstalled();
  if (app.tab === 'browse') drawBrowse();
  if (detail) drawDetail();
}

host.on('cards.changed', p => {
  const x = parse(p, 'cards.changed');
  if (x) app.next = x;
  loadInstalled();
  if (app.tab === 'account') drawAccount();
});

function drawInstalled() {
  const box = $('installed-list');
  clear(box);
  if (!app.installed.length) {
    const e = el('div', 'empty');
    e.appendChild(el('div', null, t('No cards installed.')));
    e.appendChild(button('vault', 'search', t('Browse NPC Vault'), () => show('browse')));
    box.appendChild(e);
    return;
  }
  for (const c of app.installed) box.appendChild(installedRow(c));
  if (app.next.leaving.length) {
    box.appendChild(el('div', 'section', t('Removed')));
    for (const name of app.next.leaving) {
      const r = el('div', 'row dim');
      const tx = el('div', 'row-text');
      tx.appendChild(el('div', 'row-name', name));
      tx.appendChild(el('div', 'row-note', t('Leaves after the next game start.')));
      r.appendChild(tx);
      box.appendChild(r);
    }
  }
}

function installedRow(c) {
  const r = el('div', 'row');
  r.appendChild(img('thumb', c.picture));
  const tx = el('div', 'row-text');
  tx.appendChild(el('div', 'row-name', c.name));
  tx.appendChild(el('div', 'row-sub', roleName(c.role) + (c.creator ? ' - ' + t('by {0}', c.creator) : '')));
  if (!c.loaded) tx.appendChild(el('div', 'row-note', t('Arrives after the next game start.')));
  r.appendChild(tx);
  if (app.confirmRemove === c.code) {
    r.appendChild(button('ghost small', null, t('Keep'), () => { app.confirmRemove = null; drawInstalled(); }));
    r.appendChild(button('danger small', 'trash', t('Remove'), () => remove(c)));
  } else {
    r.appendChild(button('ghost small square', 'trash', null, () => { app.confirmRemove = c.code; drawInstalled(); }));
  }
  return r;
}

function remove(c) {
  app.confirmRemove = null;
  const r = call('cards.remove', { code: c.code });
  if (!r.ok) { toast(r.msg, 'bad'); drawInstalled(); return; }
  toast(t('{0} removed. Leaves after the next game start.', c.name), 'good');
  loadInstalled();
}

// ---- browse ------------------------------------------------------------------------------------------------------

function drawChips() {
  const roles = $('roles');
  clear(roles);
  for (const [id, label] of ROLES) {
    const b = el('button', 'chip' + (browse.role === id ? ' on' : ''), t(label));
    b.addEventListener('click', () => { if (browse.role === id) return; browse.role = id; drawChips(); search(); });
    roles.appendChild(b);
  }
  const sorts = $('sorts');
  clear(sorts);
  for (const [id, label] of SORTS) {
    const b = el('button', 'chip' + (browse.sort === id ? ' on' : ''), t(label));
    b.addEventListener('click', () => { if (browse.sort === id) return; browse.sort = id; drawChips(); search(); });
    sorts.appendChild(b);
  }
}

function listPath(cursor) {
  let p = '/api/v1/cards?sort=' + browse.sort;
  if (browse.q) p += '&q=' + encodeURIComponent(browse.q);
  if (browse.role) p += '&role=' + browse.role;
  if (cursor) p += '&cursor=' + encodeURIComponent(cursor);
  return p;
}

function search() { load(null); }

function load(cursor) {
  const mine = ++browse.seq;
  browse.loading = true;
  browse.error = null;
  if (!cursor) { browse.cards = []; browse.next = null; }
  drawBrowse();
  vaultCall('vault.get', { path: listPath(cursor) }, (ok, body) => {
    if (mine !== browse.seq) { console.log('[npccards] an older vault list answered after a newer search - ignored'); return; }
    browse.loading = false;
    if (!ok || !body || !Array.isArray(body.cards)) {
      browse.error = (typeof body === 'string' && body) ? body : t('NPC Vault could not be reached.');
      drawBrowse();
      return;
    }
    browse.cards = browse.cards.concat(body.cards);
    browse.next = body.next || null;
    drawBrowse();
  });
}

function wantThumb(code, node) {
  if (thumbs[code]) { node.setAttribute('src', thumbs[code]); return; }
  (pics[code] = pics[code] || []).push(node);
  if (thumbs[code] === null) return;
  const r = call('vault.thumb', { code });
  if (r.ok && r.url) { thumbs[code] = r.url; node.setAttribute('src', r.url); }
}

host.on('vault.thumb', p => {
  const x = parse(p, 'vault.thumb');
  if (!x) return;
  thumbs[x.code] = x.url || null;
  const waiting = pics[x.code] || [];
  delete pics[x.code];
  if (!x.url) return;
  for (const n of waiting) if (document.body.contains(n)) n.setAttribute('src', x.url);
});

function drawBrowse() {
  const box = $('browse-list');
  clear(box);
  for (const k of Object.keys(pics)) delete pics[k];
  if (browse.error && !browse.cards.length) {
    box.appendChild(errorBox(browse.error, search));
    return;
  }
  if (!browse.cards.length) {
    box.appendChild(el('div', 'empty', browse.loading ? t('Loading...') : t('No cards found.')));
    return;
  }
  const grid = el('div', 'grid');
  for (const c of browse.cards) grid.appendChild(tile(c));
  box.appendChild(grid);
  const more = el('div', 'more');
  if (browse.error) more.appendChild(errorBox(browse.error, () => load(browse.next)));
  else if (browse.loading) more.appendChild(el('div', 'hint', t('Loading...')));
  else if (browse.next) more.appendChild(button('ghost', 'chev-down', t('Load more'), () => load(browse.next)));
  box.appendChild(more);
}

function tile(c) {
  const n = el('div', 'tile');
  const pic = img('pic');
  wantThumb(c.code, pic);
  n.appendChild(pic);
  n.appendChild(el('div', 'tile-name', c.name));
  const meta = el('div', 'tile-meta');
  meta.appendChild(el('span', null, roleName(c.role)));
  meta.appendChild(el('span', 'grow'));
  meta.appendChild(icon('heart'));
  meta.appendChild(el('span', null, c.hearts || 0));
  n.appendChild(meta);
  if (app.installedCodes[c.code]) n.appendChild(el('div', 'badge', t('Installed')));
  n.addEventListener('click', () => openDetail(c));
  return n;
}

function errorBox(msg, retry) {
  const e = el('div', 'error');
  e.appendChild(el('div', 'msg', msg));
  if (retry) e.appendChild(button('ghost small', 'refresh', t('Try again'), retry));
  return e;
}

// ---- card detail -----------------------------------------------------------------------------------------------

let scrim = null;

function openDetail(card) {
  detail = { card, info: null, busy: false, error: null };
  drawDetail();
  vaultCall('vault.get', { path: '/api/v1/cards/' + card.code }, (ok, body) => {
    if (!detail || detail.card.code !== card.code) return;
    if (ok && body && body.code) detail.info = body;
    else detail.error = (typeof body === 'string' && body) ? body : t('NPC Vault could not be reached.');
    drawDetail();
  });
}

function closeDetail() {
  detail = null;
  if (scrim) { scrim.remove(); scrim = null; }
}

function greeting(lines) {
  if (!lines) return null;
  if (typeof lines === 'string') return lines;
  if (Array.isArray(lines)) { const f = lines.find(x => typeof x === 'string' || (x && x.text)); return f ? (f.text || f) : null; }
  for (const k of ['greeting', 'hello', 'greet']) if (typeof lines[k] === 'string') return lines[k];
  for (const k of Object.keys(lines)) {
    const v = lines[k];
    if (typeof v === 'string') return v;
    if (Array.isArray(v) && typeof v[0] === 'string') return v[0];
  }
  return null;
}

function fact(iconName, text) {
  const f = el('div', 'fact');
  if (iconName) f.appendChild(icon(iconName));
  f.appendChild(el('span', null, text));
  return f;
}

function drawDetail() {
  if (!detail) return;
  if (!scrim) {
    scrim = el('div', 'scrim');
    scrim.addEventListener('click', e => { if (e.target === scrim) closeDetail(); });
    $('screen').appendChild(scrim);
  }
  clear(scrim);
  const c = detail.card, info = detail.info || {};
  const sheet = el('div', 'sheet');
  sheet.addEventListener('click', e => e.stopPropagation());

  const pic = img('sheet-pic');
  wantThumb(c.code, pic);
  sheet.appendChild(pic);

  const side = el('div', 'sheet-info');
  const head = el('div', 'sheet-head');
  const names = el('div', 'grow');
  names.appendChild(el('div', 'sheet-name', c.name));
  names.appendChild(el('div', 'hint', t('by {0}', c.author || (c.creator && c.creator.name) || '?')));
  head.appendChild(names);
  head.appendChild(button('ghost small square', 'close', null, closeDetail));
  side.appendChild(head);

  const facts = el('div', 'facts');
  facts.appendChild(fact('user', roleName(c.role)));
  if (info.npc && info.npc.region) facts.appendChild(fact('pin', info.npc.region));
  facts.appendChild(fact('heart', c.hearts || 0));
  facts.appendChild(fact('download', c.downloads || 0));
  if (c.heightCm) facts.appendChild(fact(null, c.heightCm + ' cm'));
  side.appendChild(facts);

  const line = info.npc ? greeting(info.npc.lines) : null;
  if (line) side.appendChild(el('div', 'line', '"' + line + '"'));
  if (info.credit && info.credit.author) {
    if (info.credit.author !== c.author) {
      side.appendChild(el('div', 'credit', t('Original by {0}', info.credit.author) + (info.credit.source ? ' (' + info.credit.source + ')' : '')));
    } else if (info.credit.source) {
      side.appendChild(el('div', 'credit', t('From {0}', info.credit.source)));
    }
  }
  if (detail.error) side.appendChild(el('div', 'row-err', detail.error));
  side.appendChild(el('div', 'spacer'));

  const acts = el('div', 'sheet-actions');
  if (app.installedCodes[c.code]) {
    const b = button('off', 'check', t('Installed'), () => {});
    b.disabled = true;
    acts.appendChild(b);
  } else if (detail.busy) {
    const b = button('off', 'download', t('Installing...'), () => {});
    b.disabled = true;
    acts.appendChild(b);
  } else {
    acts.appendChild(button('', 'install', t('Install'), () => install(c)));
  }
  acts.appendChild(button('ghost', 'link', t('Open on NPC Vault'), () => openUrl(vaultPage(c.code))));
  side.appendChild(acts);
  if (app.installedCodes[c.code] && !isLoaded(c.code)) side.appendChild(el('div', 'row-note', t('Arrives after the next game start.')));

  sheet.appendChild(side);
  scrim.appendChild(sheet);
}

function isLoaded(code) { const c = app.installed.find(x => x.code === code); return !!(c && c.loaded); }

function vaultPage(code) { return (app.vault.baseUrl || '').replace(/\/+$/, '') + '/c/' + code; }

function install(c) {
  detail.busy = true;
  drawDetail();
  vaultCall('vault.install', { code: c.code }, (ok, r) => {
    if (detail && detail.card.code === c.code) detail.busy = false;
    if (ok && r && r.ok) {
      toast(r.alreadyInstalled ? t('{0} is already installed.', r.name || c.name) : t('{0} installed. Arrives after the next game start.', r.name || c.name), 'good');
    } else {
      toast((r && (r.message || r.msg)) || (typeof r === 'string' ? r : t('Install failed. See the log.')), 'bad');
    }
    loadInstalled();
  });
}

function openUrl(url) {
  const r = call('open.url', { url });
  if (!r.ok) toast(r.msg, 'bad');
  else toast(t('Opened in your browser.'));
}

// ---- import folder -----------------------------------------------------------------------------------------------

let importFiles = [];

function loadImport() {
  const r = call('cards.importFiles');
  if (!r.ok) { importFiles = []; drawImport(r.msg); return; }
  importFiles = r.files || [];
  if (r.folder) app.importFolder = r.folder;
  drawImport();
}

function drawImport(error) {
  $('import-hint').textContent = t('Card files in this folder install by themselves at the next game start.');
  const box = $('import-list');
  clear(box);
  if (error) { box.appendChild(errorBox(error, loadImport)); return; }
  if (!importFiles.length) {
    const e = el('div', 'empty');
    e.appendChild(el('div', null, t('No card files in the import folder.')));
    e.appendChild(el('div', 'hint', app.importFolder));
    box.appendChild(e);
    return;
  }
  for (const f of importFiles) {
    const r = el('div', 'row' + (f.card ? '' : ' dim'));
    r.appendChild(img('thumb', f.picture));
    const tx = el('div', 'row-text');
    tx.appendChild(el('div', 'row-name', f.card ? f.name : f.file));
    if (f.card) tx.appendChild(el('div', 'row-sub', roleName(f.role) + (f.creator ? ' - ' + t('by {0}', f.creator) : '') + ' - ' + f.file));
    else tx.appendChild(el('div', 'row-err', f.message || t('This file is not a card.')));
    r.appendChild(tx);
    if (f.card) r.appendChild(button('small', 'install', t('Install'), () => importOne(f)));
    box.appendChild(r);
  }
}

function importOne(f) {
  const r = call('cards.importFile', { file: f.file });
  if (r.ok) toast(r.alreadyInstalled ? t('{0} is already installed.', r.name) : t('{0} installed. Arrives after the next game start.', r.name), 'good');
  else toast(r.message || r.msg || t('Install failed. See the log.'), 'bad');
  loadImport();
  loadInstalled();
}

// ---- account -----------------------------------------------------------------------------------------------------

function loadAccount() {
  const s = call('vault.state');
  if (s.ok) app.vault = s;
  const u = call('cards.uploadFiles');
  uploads.files = u.ok ? (u.files || []) : [];
  drawAccount();
}

host.on('vault.connect', p => {
  const x = parse(p, 'vault.connect');
  if (!x) return;
  connect = x;
  if (x.state === 'connected') { connect = null; toast(t('Connected as {0}.', x.userName || '?'), 'good'); }
  if (app.tab === 'account') drawAccount();
});

host.on('vault.state', p => {
  const x = parse(p, 'vault.state');
  if (!x) return;
  app.vault = x;
  if (app.tab === 'account') loadAccount();
});

function drawAccount() {
  const box = $('account-list');
  clear(box);
  box.appendChild(signInBox());
  box.appendChild(arrivalsBox());
  if (app.vault.connected) box.appendChild(uploadBox());
}

function signInBox() {
  const b = el('div', 'box');
  const head = el('div', 'box-head');
  head.appendChild(icon('vault'));
  head.appendChild(el('div', 'box-title grow', 'NPC Vault'));
  b.appendChild(head);

  if (app.vault.connected) {
    b.appendChild(el('div', 'ok', t('Connected as {0}.', app.vault.userName || '?')));
    b.appendChild(el('div', 'hint', t('Cards you send to the game on the website arrive here.')));
    const row = el('div', 'box-row');
    row.appendChild(button('ghost small', 'link', t('Open NPC Vault'), () => openUrl((app.vault.baseUrl || '').replace(/\/+$/, '') + '/')));
    row.appendChild(button('danger small', 'close', t('Disconnect'), () => {
      const s = call('vault.disconnect');
      if (s.ok) app.vault = s;
      drawAccount();
    }));
    b.appendChild(row);
    return b;
  }

  if (connect && connect.state === 'waiting') {
    b.appendChild(el('div', null, t('Enter this code on the NPC Vault page:')));
    b.appendChild(el('div', 'code', connect.userCode || '------'));
    if (connect.verifyUrl) b.appendChild(el('div', 'hint', connect.verifyUrl));
    const row = el('div', 'box-row');
    row.appendChild(button('vault small', 'link', t('Open page'), () => openUrl(connect.verifyUrl)));
    row.appendChild(button('ghost small', 'copy', t('Copy code'), () => {
      const r = call('copy', { text: connect.userCode });
      toast(r.ok ? t('Code copied.') : r.msg, r.ok ? 'good' : 'bad');
    }));
    b.appendChild(row);
    b.appendChild(el('div', 'hint', t('Waiting for the sign-in...')));
    return b;
  }

  b.appendChild(el('div', 'hint', t('Connect this game to get cards sent from the website and to upload your own.')));
  if (connect && connect.state !== 'waiting') {
    b.appendChild(el('div', 'row-err', connect.state === 'expired' ? t('The code expired. Start again.') : (connect.message || t('Sign-in failed. See the log.'))));
  }
  b.appendChild(button('vault', 'user', t('Connect'), () => {
    connect = { state: 'waiting' };
    const r = call('vault.connect');
    if (!r.ok) { connect = { state: 'failed', message: r.msg }; }
    drawAccount();
  }));
  return b;
}

function arrivalsBox() {
  const b = el('div', 'box');
  const head = el('div', 'box-head');
  head.appendChild(icon('download'));
  head.appendChild(el('div', 'box-title grow', t('Sent from the website')));
  b.appendChild(head);
  const r = call('cards.inbox');
  const cards = r.ok ? (r.cards || []) : [];
  if (!cards.length) {
    b.appendChild(el('div', 'hint', app.vault.connected ? t('Nothing arrived yet. Use "Send to game" on a card on the website.') : t('Connect first to get cards from the website.')));
    return b;
  }
  for (const c of cards) {
    const row = el('div', 'row');
    row.appendChild(img('thumb', c.picture));
    const tx = el('div', 'row-text');
    tx.appendChild(el('div', 'row-name', c.name));
    tx.appendChild(el('div', 'row-note', t('Arrived at {0}. Shows up after the next game start.', c.at)));
    row.appendChild(tx);
    b.appendChild(row);
  }
  return b;
}

function uploadBox() {
  const b = el('div', 'box');
  const head = el('div', 'box-head');
  head.appendChild(icon('publish'));
  head.appendChild(el('div', 'box-title grow', t('Upload a card')));
  b.appendChild(head);
  if (!uploads.files.length) {
    b.appendChild(el('div', 'hint', t('Put a card PNG into the import folder to upload it.')));
    const row = el('div', 'box-row');
    row.appendChild(button('ghost small', 'folder', t('Open folder'), openFolder));
    b.appendChild(row);
    return b;
  }
  for (const f of uploads.files) {
    const row = el('div', 'row');
    row.appendChild(img('thumb', f.picture));
    const tx = el('div', 'row-text');
    tx.appendChild(el('div', 'row-name', f.name));
    tx.appendChild(el('div', 'row-sub', f.file));
    const res = uploads.results[f.file];
    if (res) tx.appendChild(el('div', res.ok ? 'ok' : 'row-err', res.text));
    row.appendChild(tx);
    if (res && res.url) row.appendChild(button('ghost small', 'link', t('Open'), () => openUrl(res.url)));
    else if (uploads.busy === f.file) { const w = button('off small', 'publish', t('Uploading...'), () => {}); w.disabled = true; row.appendChild(w); }
    else row.appendChild(button('vault small', 'publish', t('Upload'), () => upload(f)));
    b.appendChild(row);
  }
  return b;
}

function upload(f) {
  uploads.busy = f.file;
  drawAccount();
  vaultCall('vault.upload', { file: f.file }, (ok, r) => {
    uploads.busy = null;
    if (ok && r && r.ok) uploads.results[f.file] = { ok: true, text: t('Published as {0}.', r.code), url: r.url };
    else if (ok && r && r.duplicate) uploads.results[f.file] = { ok: true, text: t('Already on NPC Vault as {0}.', r.code), url: r.code ? vaultPage(r.code) : null };
    else uploads.results[f.file] = { ok: false, text: (r && (r.message || r.msg)) || (typeof r === 'string' ? r : t('Upload failed. See the log.')) };
    if (app.tab === 'account') drawAccount();
  });
}

function openFolder() {
  const r = call('cards.openFolder');
  if (!r.ok) toast(r.msg, 'bad');
}

// ---- back: the phone's back gesture, Escape or right-click on the menu page ---------------------------------------------

function back() {
  if (detail) { closeDetail(); return true; }
  if (app.confirmRemove) { app.confirmRemove = null; drawInstalled(); return true; }
  return false;
}

document.addEventListener('back', e => { if (back()) e.preventDefault(); });

host.on('nav.back', () => { if (!back()) closeMenu(); });

function closeMenu() {
  const r = call('menu.close');
  if (!r.ok) console.warn('[npccards] menu.close refused: ' + r.msg);
}

// ---- start -------------------------------------------------------------------------------------------------------

function start() {
  const r = call('init');
  if (r.ok) {
    strings = r.strings || {};
    app.where = r.where || 'phone';
    if (r.vault) app.vault = r.vault;
    app.personifyUrl = r.personifyUrl || '';
    app.importFolder = r.importFolder || '';
  } else {
    console.error('[npccards] init failed: ' + r.msg);
  }
  if (app.where === 'menu') document.body.classList.add('menu');
  $('close').className = app.where === 'menu' ? 'btn ghost square' : 'hidden';
  $('close').addEventListener('click', closeMenu);
  $('make-text').textContent = t('Make your own');
  $('make').addEventListener('click', () => openUrl(app.personifyUrl));
  $('import-refresh-text').textContent = t('Refresh');
  $('import-refresh').addEventListener('click', loadImport);
  $('import-open-text').textContent = t('Open folder');
  $('import-open').addEventListener('click', openFolder);
  const q = $('q');
  q.setAttribute('placeholder', t('Search names'));
  // The engine hands the value on input and change; keydown carries only the key.
  let typed = '';
  const commit = () => {
    const v = typed.trim();
    if (v === browse.q) return;
    browse.q = v;
    search();
  };
  q.addEventListener('input', e => { typed = String(valueOf(e) || ''); });
  q.addEventListener('change', e => { typed = String(valueOf(e) || ''); commit(); });
  q.addEventListener('keydown', e => { if (e.key === 'Enter') commit(); });
  drawChips();
  const hint = call('siesta.hint');
  if (hint.ok) siestaTip = hint;
  else console.error('[npccards] siesta.hint failed: ' + hint.msg);
  loadInstalled();
  show('browse');
}

start();
