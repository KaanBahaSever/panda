// Checks the panel's translations: every language has every key, every key the panel uses exists,
// and the chat languages offered in the settings match the ones the bot knows.
// usage: node tests/i18n-check.mjs        Exit code 1 when something is missing.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const src = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'Panda');
const js = readFileSync(join(src, 'wwwroot', 'app.js'), 'utf8');
const html = readFileSync(join(src, 'wwwroot', 'index.html'), 'utf8');
const strings = readFileSync(join(src, 'Strings.cs'), 'utf8');

const start = js.indexOf('const I18N = ');
const end = js.indexOf('\n};\n', start) + 3;
const I18N = eval('(' + js.slice(start + 'const I18N = '.length, end - 1) + ')');
const langs = Object.keys(I18N);
const all = new Set(langs.flatMap(l => Object.keys(I18N[l])));
const problems = [];

for (const l of langs) {
  const missing = [...all].filter(k => !(k in I18N[l]));
  if (missing.length) problems.push(`panel ${l}: missing ${missing.join(', ')}`);
  for (const k of Object.keys(I18N[l]))
    if (typeof I18N[l][k] !== typeof I18N.en[k]) problems.push(`panel ${l}: "${k}" is a ${typeof I18N[l][k]}, English has a ${typeof I18N.en[k]}`);
}

const used = new Set();
for (const m of js.slice(end).matchAll(/\bt\(\s*'([^']+)'\s*[,)]/g)) used.add(m[1]);   // not t('loop' + mode)
for (const m of html.matchAll(/data-i18n(?:-ph|-aria)?="([^"]+)"/g)) used.add(m[1]);
const undefinedKeys = [...used].filter(k => !all.has(k) && !k.endsWith('.'));
if (undefinedKeys.length) problems.push(`panel: used but not defined: ${undefinedKeys.join(', ')}`);

// Chat: Strings.Languages, the settings' language options and the panel languages.
const chat = (strings.match(/Languages = \[([^\]]*)\]/)?.[1] ?? '').match(/"(\w+)"/g)?.map(s => s.slice(1, -1)) ?? [];
const select = html.match(/<select name="language">([\s\S]*?)<\/select>/)?.[1] ?? '';
const options = [...select.matchAll(/value="(\w+)"/g)].map(m => m[1]);
if (chat.join() !== options.join()) problems.push(`chat languages ${chat.join('/')} but the settings offer ${options.join('/')}`);
if (chat.join() !== langs.join()) problems.push(`chat languages ${chat.join('/')} but the panel has ${langs.join('/')}`);

// Every chat string has a text for every language: the tuple has one field per language.
const fields = strings.match(/Dictionary<string, \(([^)]*)\)> All/)?.[1].split(',').length ?? 0;
if (fields !== chat.length) problems.push(`Strings.cs has ${fields} texts per string but ${chat.length} languages`);

if (problems.length) {
  console.log(problems.join('\n'));
  process.exit(1);
}
console.log(`translations ok: panel ${langs.join('/')} (${all.size} keys), chat ${chat.join('/')}`);
