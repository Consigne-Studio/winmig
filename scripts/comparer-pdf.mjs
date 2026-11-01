// Compare le PDF d'un état migré à son PDF de référence (Crystal Reports ou autre) : nombre de pages,
// texte de chaque page comparé mot à mot (hors mise en page), et écart visuel pixel par pixel, avec une carte des écarts.
// Usage : node scripts/comparer-pdf.mjs <reference.pdf> <nouveau.pdf> [-o <dossier>] [--seuil <pourcentage>] [--dpi <n>]
//   Écrit <dossier>/comparaison.html (autonome) et sort avec le code 0 si les pages et les textes sont identiques
//   et si chaque page diffère de moins de --seuil % de ses pixels (défaut 0,5 %), 1 s'il y a des écarts,
//   2 en cas d'erreur d'usage (option sans valeur, fichier absent, Poppler introuvable, PDF illisible).
// Les pages sont rendues une par une : la mémoire reste celle d'une page, même pour un état de plusieurs centaines de pages.
// Prérequis : pdfinfo, pdftoppm et pdftotext (Poppler) dans le PATH.
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync, mkdirSync, rmSync, existsSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import zlib from 'node:zlib';

const USAGE = 'usage : node scripts/comparer-pdf.mjs <reference.pdf> <nouveau.pdf> [-o <dossier>] [--seuil <pourcentage>] [--dpi <n>]';
const CARTES_MAX = 20; // cartes des écarts incluses dans le HTML, pour qu'il reste léger
class Usage extends Error {}

function lireArguments(argv) {
  const args = [...argv];
  const opt = (n, d) => {
    const i = args.indexOf(n); if (i < 0) return d;
    const v = args[i + 1]; if (v === undefined || v.startsWith('-')) throw new Usage(`l'option ${n} attend une valeur`);
    args.splice(i, 2); return v;
  };
  const sortie = opt('-o', 'comparaison');
  const seuil = parseFloat(String(opt('--seuil', '0.5')).replace(',', '.'));
  const dpi = parseInt(opt('--dpi', '100'), 10);
  if (Number.isNaN(seuil) || seuil < 0) throw new Usage('--seuil attend un pourcentage positif');
  if (Number.isNaN(dpi) || dpi < 20 || dpi > 600) throw new Usage('--dpi attend un nombre entre 20 et 600');
  const inconnue = args.find((a) => a.startsWith('-'));
  if (inconnue) throw new Usage(`option inconnue : ${inconnue}`);
  if (args.length !== 2) throw new Usage('il faut deux PDF : la référence puis le nouveau');
  for (const f of args) if (!existsSync(f) || !statSync(f).isFile()) throw new Usage(`fichier introuvable : ${f}`);
  return { ref: args[0], neuf: args[1], sortie, seuil, dpi };
}

function outil(nom, a) {
  try { return execFileSync(nom, a, { maxBuffer: 1 << 30, stdio: ['ignore', 'pipe', 'pipe'] }); }
  catch (e) {
    if (e.code === 'ENOENT') throw new Usage(`${nom} introuvable : installez Poppler (pdfinfo, pdftoppm, pdftotext).`);
    throw new Usage(`${nom} a échoué sur ${a[a.length - 2] ?? a[a.length - 1]} : ${String(e.stderr || '').trim().split('\n')[0] || 'PDF illisible'}`);
  }
}

function nbPages(pdf) {
  const m = /^Pages:\s+(\d+)/m.exec(outil('pdfinfo', [pdf]).toString('utf8'));
  if (!m) throw new Usage(`nombre de pages illisible : ${pdf}`);
  return +m[1];
}

// Une page en image PPM (P6), lue sans dépendance puis effacée.
function page(pdf, n, dpi, dossier) {
  const base = path.join(dossier, `p${n}`);
  outil('pdftoppm', ['-r', String(dpi), '-f', String(n), '-l', String(n), '-singlefile', pdf, base]);
  const f = `${base}.ppm`;
  const img = lirePpm(readFileSync(f));
  rmSync(f, { force: true });
  return img;
}
function lirePpm(b) {
  let i = 0; const champs = [];
  while (champs.length < 4) {
    while (/\s/.test(String.fromCharCode(b[i]))) i++;
    if (b[i] === 0x23) { while (b[i] !== 0x0a) i++; continue; }
    let t = ''; while (!/\s/.test(String.fromCharCode(b[i]))) t += String.fromCharCode(b[i++]);
    champs.push(t);
  }
  i++;
  return { l: +champs[1], h: +champs[2], px: b.subarray(i) };
}

// Texte d'une page sans -layout : l'ordre de lecture, sans les espaces de mise en page.
function texte(pdf, n) {
  return outil('pdftotext', ['-f', String(n), '-l', String(n), '-enc', 'UTF-8', pdf, '-']).toString('utf8')
    .normalize('NFC').replace(/ | /g, ' ').replace(/[ \t]+/g, ' ').replace(/\s*\n\s*/g, '\n').trim();
}
const mots = (t) => t.split(/\s+/).filter(Boolean);
const memesMots = (a, b) => { const x = mots(a), y = mots(b); return x.length === y.length && x.every((m, k) => m === y[k]); };

// Lignes ajoutées ou retirées, par plus longue sous-suite commune : un décalage d'une ligne ne rend pas tout le reste « différent ».
function ecartsTexte(a, b) {
  const norm = (l) => mots(l).join(' ');
  const la = a.split('\n').map(norm).filter(Boolean), lb = b.split('\n').map(norm).filter(Boolean);
  const n = la.length, m = lb.length;
  if (n * m > 4e6) return [[`${n} lignes`, `${m} lignes`]];
  const t = Array.from({ length: n + 1 }, () => new Uint32Array(m + 1));
  for (let i = n - 1; i >= 0; i--) for (let j = m - 1; j >= 0; j--) t[i][j] = la[i] === lb[j] ? t[i + 1][j + 1] + 1 : Math.max(t[i + 1][j], t[i][j + 1]);
  const r = []; let i = 0, j = 0;
  while (i < n || j < m) {
    if (i < n && j < m && la[i] === lb[j]) { i++; j++; }
    else if (j < m && (i === n || t[i][j + 1] >= t[i + 1][j])) r.push(['—', lb[j++]]);
    else r.push([la[i++], '—']);
  }
  return r;
}

// PNG minimal (RGB, 8 bits) pour la carte des écarts.
const CRC = new Int32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c; });
const crc = (b) => { let c = -1; for (const x of b) c = CRC[(c ^ x) & 0xff] ^ (c >>> 8); return (c ^ -1) >>> 0; };
function png(l, h, rgb) {
  const brut = Buffer.alloc((l * 3 + 1) * h);
  for (let y = 0; y < h; y++) { brut[y * (l * 3 + 1)] = 0; rgb.copy(brut, y * (l * 3 + 1) + 1, y * l * 3, (y + 1) * l * 3); }
  const bloc = (type, d) => { const t = Buffer.from(type); const n = Buffer.alloc(4); n.writeUInt32BE(d.length); const c = Buffer.alloc(4); c.writeUInt32BE(crc(Buffer.concat([t, d]))); return Buffer.concat([n, t, d, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(l, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 2;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), bloc('IHDR', ihdr), bloc('IDAT', zlib.deflateSync(brut)), bloc('IEND', Buffer.alloc(0))]);
}

// Pourcentage de pixels différents et, si demandé, carte : la référence en gris clair, les pixels différents en rouge.
function comparer(a, b, avecCarte) {
  const l = Math.max(a.l, b.l), h = Math.max(a.h, b.h);
  const carte = avecCarte ? Buffer.alloc(l * h * 3, 255) : null;
  let diff = 0, x0 = l, y0 = h, x1 = -1, y1 = -1;
  for (let y = 0; y < h; y++) for (let x = 0; x < l; x++) {
    const pa = x < a.l && y < a.h ? (y * a.l + x) * 3 : -1, pb = x < b.l && y < b.h ? (y * b.l + x) * 3 : -1;
    const ra = pa < 0 ? 255 : a.px[pa], ga = pa < 0 ? 255 : a.px[pa + 1], ba = pa < 0 ? 255 : a.px[pa + 2];
    const rb = pb < 0 ? 255 : b.px[pb], gb = pb < 0 ? 255 : b.px[pb + 1], bb = pb < 0 ? 255 : b.px[pb + 2];
    const ecart = Math.abs(ra - rb) + Math.abs(ga - gb) + Math.abs(ba - bb);
    const o = (y * l + x) * 3;
    if (ecart > 96) {
      diff++; x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
      if (carte) { carte[o] = 214; carte[o + 1] = 40; carte[o + 2] = 40; }
    } else if (carte) { const g = 255 - Math.round((255 - (ra + ga + ba) / 3) * 0.35); carte[o] = carte[o + 1] = carte[o + 2] = g; }
  }
  // Cadre rouge autour de la zone des écarts, pour les voir même quand ils sont minuscules.
  if (carte && x1 >= 0) {
    const m = 8, e = 3, X0 = Math.max(0, x0 - m), Y0 = Math.max(0, y0 - m), X1 = Math.min(l - 1, x1 + m), Y1 = Math.min(h - 1, y1 + m);
    const rouge = (x, y) => { const o = (y * l + x) * 3; carte[o] = 214; carte[o + 1] = 40; carte[o + 2] = 40; };
    for (let k = 0; k < e; k++) {
      for (let x = X0; x <= X1; x++) { rouge(x, Math.max(0, Y0 - k)); rouge(x, Math.min(h - 1, Y1 + k)); }
      for (let y = Y0; y <= Y1; y++) { rouge(Math.max(0, X0 - k), y); rouge(Math.min(l - 1, X1 + k), y); }
    }
  }
  return { pourcent: (100 * diff) / (l * h), carte: carte ? png(l, h, carte) : null, l, h };
}

function principal() {
  const { ref, neuf, sortie, seuil, dpi } = lireArguments(process.argv.slice(2));
  const NA = nbPages(ref), NB = nbPages(neuf);
  const tmp = mkdtempSync(path.join(tmpdir(), 'comparer-pdf-'));
  try {
    const n = Math.max(NA, NB);
    const lignes = []; let ok = NA === NB, cartes = 0, sansCarte = 0;
    for (let p = 1; p <= n; p++) {
      if (p > NA || p > NB) { lignes.push({ p, manque: p > NA ? 'référence' : 'nouveau' }); ok = false; continue; }
      const ta = texte(ref, p), tb = texte(neuf, p);
      const texteOk = memesMots(ta, tb);
      const A = page(ref, p, dpi, tmp), B = page(neuf, p, dpi, tmp);
      let c = comparer(A, B, false);
      const pageOk = texteOk && c.pourcent < seuil;
      if (!pageOk) {
        ok = false;
        if (cartes < CARTES_MAX) { c = comparer(A, B, true); cartes++; } else sansCarte++;
      }
      lignes.push({ p, texteOk, ...c, pageOk, ecarts: texteOk ? [] : ecartsTexte(ta, tb) });
      console.log(`page ${p} : texte ${texteOk ? 'identique' : 'différent'}, ${fr(c.pourcent)} % de pixels différents`);
    }
    for (const x of lignes) if (x.manque) console.log(`page ${x.p} : absente du PDF ${x.manque}`);
    const html = `<!doctype html><html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Comparaison des PDF</title><style>
body{font:14px/1.5 system-ui,sans-serif;color:#1B1E21;background:#F3F4F5;margin:0;padding:24px 16px}main{max-width:1100px;margin:0 auto}
h1{font-size:24px;margin:0 0 4px}.v{display:inline-block;padding:2px 8px;font-weight:700;border:1.5px solid #1B1E21}.ok{background:#2BB673}.ko{background:#E4572E;color:#fff}
table{border-collapse:collapse;width:100%;background:#fff;margin:16px 0}td,th{border-bottom:1px solid #D4D7DA;padding:6px 8px;text-align:left;vertical-align:top;overflow-wrap:anywhere}
figure{margin:16px 0;background:#fff;border:1px solid #9AA0A6;padding:8px}figure img{max-width:100%;height:auto;display:block}
</style></head><body><main><h1>Comparaison des PDF</h1>
<p>Référence : <code>${esc(path.basename(ref))}</code> (${NA} page${NA > 1 ? 's' : ''}) · nouveau : <code>${esc(path.basename(neuf))}</code> (${NB} page${NB > 1 ? 's' : ''}) · texte comparé mot à mot, hors mise en page · seuil ${fr(seuil)} % des pixels · ${dpi} ppp</p>
<p>Verdict : <span class="v ${ok ? 'ok' : 'ko'}">${ok ? 'identique' : 'écarts à valider'}</span></p>
<table><thead><tr><th>Page</th><th>Texte</th><th>Pixels différents</th><th>Page</th></tr></thead><tbody>
${lignes.map((x) => x.manque ? `<tr><td>${x.p}</td><td colspan="3">absente du PDF ${x.manque}</td></tr>` : `<tr><td>${x.p}</td><td>${x.texteOk ? 'identique' : 'différent'}</td><td>${fr(x.pourcent)} %</td><td>${x.pageOk ? 'conforme' : 'à valider'}</td></tr>`).join('\n')}
</tbody></table>
${sansCarte ? `<p>Les cartes des écarts sont limitées aux ${CARTES_MAX} premières pages à valider ; ${sansCarte} autre${sansCarte > 1 ? 's' : ''} page${sansCarte > 1 ? 's' : ''} à valider figure${sansCarte > 1 ? 'nt' : ''} dans le tableau ci-dessus.</p>` : ''}
${lignes.filter((x) => !x.manque && !x.pageOk).map((x) => `<h2>Page ${x.p}</h2>${x.carte ? `<figure><img src="data:image/png;base64,${x.carte.toString('base64')}" width="${x.l}" height="${x.h}" alt="Carte des écarts de la page ${x.p} : en rouge, les pixels qui diffèrent"><figcaption>En rouge : pixels qui diffèrent de la référence, encadrés.</figcaption></figure>` : ''}${x.texteOk ? '' : `<p>Lignes de texte présentes d'un seul côté :</p><table><thead><tr><th>Référence</th><th>Nouveau</th></tr></thead><tbody>${x.ecarts.map(([a, b]) => `<tr><td>${esc(a)}</td><td>${esc(b)}</td></tr>`).join('')}</tbody></table>`}`).join('\n')}
</main></body></html>`;
    mkdirSync(sortie, { recursive: true });
    writeFileSync(path.join(sortie, 'comparaison.html'), html);
    console.log(`${ok ? 'Identique' : 'Écarts à valider'} : ${path.join(sortie, 'comparaison.html')}`);
    return ok ? 0 : 1;
  } finally { rmSync(tmp, { recursive: true, force: true }); }
}

function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
function fr(v) { return v > 0 && v < 0.01 ? '< 0,01' : v.toFixed(2).replace('.', ','); }

try { process.exitCode = principal(); }
catch (e) {
  console.error(e instanceof Usage ? e.message : `erreur : ${e.message}`);
  if (e instanceof Usage) console.error(USAGE);
  process.exitCode = 2;
}
