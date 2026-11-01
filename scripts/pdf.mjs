// Imprime rapport.html en PDF (A4) et, sur demande, une image PNG par page.
// Usage : node pdf.mjs <dossier du rapport> [--png <dossier>] [--pages 1,2,5]
import path from 'node:path';
import { mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

const require = createRequire(import.meta.url);
function playwright() {
  for (const m of ['playwright', '/opt/node22/lib/node_modules/playwright']) { try { return require(m); } catch {} }
  throw new Error('Playwright introuvable : npm i -D playwright');
}
const args = process.argv.slice(2);
const dossier = args[0];
if (!dossier) { console.error('usage : node pdf.mjs <dossier> [--png <dossier>] [--pages 1,2]'); process.exit(2); }
const iPng = args.indexOf('--png');
const png = iPng > 0 ? args[iPng + 1] : null;
const iPages = args.indexOf('--pages');
const quelles = iPages > 0 ? args[iPages + 1].split(',').map(Number) : null;

const browser = await playwright().chromium.launch(process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM } : {});
const page = await browser.newPage({ viewport: { width: 900, height: 1200 }, deviceScaleFactor: 2 });
await page.goto(pathToFileURL(path.resolve(dossier, 'rapport.html')).href, { waitUntil: 'load' });
await page.evaluate(() => document.fonts.ready);
// Contrôle : aucun contenu ne doit déborder de sa page.
const debords = await page.evaluate(() => [...document.querySelectorAll('.page .corps')].map((c, i) => c.scrollHeight > c.clientHeight + 1 ? i + 2 : 0).filter(Boolean));
if (debords.length) console.warn(`Contenu coupé en bas des pages : ${debords.join(', ')}`);
await page.pdf({ path: path.join(dossier, 'rapport.pdf'), format: 'A4', printBackground: true, preferCSSPageSize: true });
if (png) {
  await mkdir(png, { recursive: true });
  const pages = await page.$$('.page');
  for (let i = 0; i < pages.length; i++) {
    if (quelles && !quelles.includes(i + 1)) continue;
    await pages[i].screenshot({ path: path.join(png, `page-${String(i + 1).padStart(2, '0')}.png`) });
  }
}
console.log(`${path.join(dossier, 'rapport.pdf')}${debords.length ? ` (débordements : ${debords.join(', ')})` : ''}`);
await browser.close();
process.exit(debords.length ? 1 : 0);
