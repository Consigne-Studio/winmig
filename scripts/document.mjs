// Imprime un document Markdown (instructions, questionnaire, méthode, CGV) en A4 à la charte du rapport.
// Usage : node scripts/document.mjs <source.md> <sortie.pdf|.html> ["Titre"] ["Sous-titre"]
// Une sortie .html s'arrête au HTML (fichier « modèle » dont les variables {{STUDIO_…}} sont remplacées à la publication).
import path from 'node:path';
import { readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const R = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const [src, sortie, titreArg, sousTitre = 'Migration WinForms et WPF vers le web · Consigne'] = process.argv.slice(2);
if (!src || !sortie) { console.error('usage : node scripts/document.mjs <source.md> <sortie.pdf|.html> ["Titre"] ["Sous-titre"]'); process.exit(2); }

const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const enLigne = (s) => esc(s)
  .replace(/`([^`]+)`/g, '<code>$1</code>')
  .replace(/\*\*([^*]+)\*\*/g, '<b>$1</b>')
  .replace(/(^|[^*])\*([^*\s][^*]*)\*/g, '$1<i>$2</i>')
  .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');

// Markdown simple : titres, paragraphes, listes, tableaux, citations, blocs de code.
function versHtml(md) {
  const l = md.replace(/\r/g, '').split('\n');
  const o = [];
  for (let i = 0; i < l.length;) {
    const x = l[i];
    if (!x.trim()) { i++; continue; }
    if (x.startsWith('```')) { const b = []; for (i++; i < l.length && !l[i].startsWith('```'); i++) b.push(esc(l[i])); i++; o.push(`<pre>${b.join('\n')}</pre>`); continue; }
    const h = x.match(/^(#{1,4}) (.*)$/);
    if (h) { o.push(`<h${h[1].length}>${enLigne(h[2])}</h${h[1].length}>`); i++; continue; }
    if (x.startsWith('|')) {
      const rows = []; for (; i < l.length && l[i].startsWith('|'); i++) rows.push(l[i]);
      const cells = (r) => r.replace(/^\||\|$/g, '').split('|').map((c) => c.trim());
      o.push(`<table><thead><tr>${cells(rows[0]).map((c) => `<th>${enLigne(c)}</th>`).join('')}</tr></thead><tbody>${rows.slice(2).map((r) => `<tr>${cells(r).map((c) => `<td>${enLigne(c)}</td>`).join('')}</tr>`).join('')}</tbody></table>`);
      continue;
    }
    if (x.startsWith('> ')) { const b = []; for (; i < l.length && l[i].startsWith('> '); i++) b.push(l[i].slice(2)); o.push(`<blockquote>${enLigne(b.join(' '))}</blockquote>`); continue; }
    const li = /^(\*|-|\d+\.) /;
    if (li.test(x)) {
      const ord = /^\d+\./.test(x); const debut = ord ? parseInt(x, 10) : 1; const items = [];
      for (; i < l.length && (li.test(l[i]) || (/^ {2,}\S/.test(l[i]) && items.length)); i++) {
        if (li.test(l[i])) items.push(l[i].replace(li, '')); else items[items.length - 1] += ' ' + l[i].trim();
      }
      o.push(`<${ord ? `ol${debut > 1 ? ` start="${debut}"` : ''}` : 'ul'}>${items.map((t) => `<li>${enLigne(t)}</li>`).join('')}</${ord ? 'ol' : 'ul'}>`);
      continue;
    }
    const p = []; for (; i < l.length && l[i].trim() && !/^(#|\||> |```|\* |- |\d+\. )/.test(l[i]); i++) p.push(l[i].trim());
    o.push(`<p>${enLigne(p.join(' '))}</p>`);
  }
  return o.join('\n');
}

const md = await readFile(src, 'utf8');
const titre = titreArg ?? (md.match(/^# (.*)$/m)?.[1] ?? 'Document');
const corps = versHtml(md.replace(/^# .*$/m, ''));
const b64 = async (n) => (await readFile(path.join(R, 'src', 'Winmig.Core', 'polices', n))).toString('base64');
const html = `<!doctype html><html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${esc(titre)} · Consigne</title><style>
@font-face{font-family:Archivo;src:url(data:font/woff2;base64,${await b64('Archivo-var.woff2')}) format('woff2');font-weight:100 900;font-stretch:62% 125%}
@font-face{font-family:'Martian Mono';src:url(data:font/woff2;base64,${await b64('MartianMono-var.woff2')}) format('woff2');font-weight:100 800;font-stretch:75% 112.5%}
@font-face{font-family:Atkinson;src:url(data:font/woff2;base64,${await b64('AtkinsonHyperlegibleNext-var.woff2')}) format('woff2');font-weight:200 800}
@page{size:A4;margin:18mm 18mm 20mm;@bottom-right{content:'DEV-605 · ' counter(page) ' / ' counter(pages);font:500 7.5px 'Martian Mono',monospace;color:#5C636A;letter-spacing:.06em}}
*{box-sizing:border-box}
body{font:13px/1.55 Atkinson,sans-serif;color:#1B1E21;margin:0 auto;max-width:820px;padding:24px 16px;background:#F3F4F5}
@media print{body{background:none;padding:0;max-width:none}}
header{display:flex;gap:14px;align-items:center;border-bottom:1.5px solid #1B1E21;padding-bottom:12px;margin-bottom:18px}
header .t{font:800 38px/.92 Archivo,sans-serif;font-stretch:62%;text-transform:uppercase}
header .d{font:500 9px/1.7 'Martian Mono',monospace;font-stretch:87.5%;letter-spacing:.07em;text-transform:uppercase;color:#5C636A}
h2,h3,h4{font-family:Archivo,sans-serif;font-weight:800;font-stretch:75%;text-transform:uppercase;line-height:1.1;margin:20px 0 6px;break-after:avoid}
h2{font-size:20px}h3{font-size:16px}h4{font-size:14px}
p{margin:0 0 8px}ul,ol{margin:0 0 8px 20px;padding:0}li{margin:0 0 4px}
a{color:#1B1E21}
code{font-family:'Martian Mono',monospace;font-stretch:87.5%;font-size:.84em;background:#E6E8EA;padding:0 3px;word-break:break-word}
pre{font:11px/1.5 'Martian Mono',monospace;font-stretch:87.5%;background:#fff;border:1px solid #9AA0A6;padding:10px 12px;white-space:pre-wrap;word-break:break-all;break-inside:avoid}
blockquote{margin:8px 0;padding:8px 12px;border-left:3px solid #2BB673;background:#fff}
table{width:100%;border-collapse:collapse;margin:6px 0 12px;font-size:12px;break-inside:auto}
th{text-align:left;font:500 8.5px 'Martian Mono',monospace;letter-spacing:.06em;text-transform:uppercase;color:#5C636A;border-bottom:1.5px solid #1B1E21;padding:5px 6px}
td{border-bottom:1px solid #D4D7DA;padding:6px;vertical-align:top}tr{break-inside:avoid}
</style></head><body><header><svg width="62" height="62" viewBox="0 0 64 64" aria-hidden="true"><circle cx="32" cy="32" r="30.6" fill="#2BB673" stroke="#1B1E21" stroke-width="2.8"/><line x1="1.4" y1="32" x2="62.6" y2="32" stroke="#1B1E21" stroke-width="2.8"/><text x="32" y="26" text-anchor="middle" font-family="Archivo" font-stretch="62%" font-weight="800" font-size="19" fill="#1B1E21">DEV</text><text x="32" y="49" text-anchor="middle" font-family="Martian Mono" font-stretch="87.5%" font-size="12.5" fill="#1B1E21">605</text></svg>
<div><div class="t">${esc(titre)}</div><div class="d">${esc(sousTitre)}</div></div></header>
${corps}
</body></html>`;

const cible = sortie.endsWith('.pdf') ? sortie.replace(/\.pdf$/, '.html') : sortie;
await writeFile(cible, html);
if (sortie.endsWith('.pdf')) {
  const require = createRequire(import.meta.url);
  let pw; for (const m of ['playwright', '/opt/node22/lib/node_modules/playwright']) { try { pw = require(m); break; } catch {} }
  if (!pw) throw new Error('Playwright introuvable : npm install && npx playwright install chromium');
  const b = await pw.chromium.launch(process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM } : {});
  const p = await b.newPage();
  await p.goto(pathToFileURL(path.resolve(cible)).href);
  await p.pdf({ path: sortie, format: 'A4', preferCSSPageSize: true, printBackground: true });
  await b.close();
}
console.log(sortie);
