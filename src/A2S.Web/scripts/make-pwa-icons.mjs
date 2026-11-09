// Render public/favicon.svg into the PNG icons a PWA manifest needs, using the repo's Playwright chromium.
// Run with cwd = the A2S.Web folder so '@playwright/test' resolves.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';

const web = process.cwd();
const svg = fs.readFileSync(path.join(web, 'public/favicon.svg'), 'utf8');
const out = path.join(web, 'public/icons');
fs.mkdirSync(out, { recursive: true });

const BG = '#09090b'; // --color-background hsl(240 10% 4%)

const targets = [
  { file: 'icon-192.png', size: 192, pad: 0.1, bg: BG },
  { file: 'icon-512.png', size: 512, pad: 0.1, bg: BG },
  { file: 'icon-maskable-512.png', size: 512, pad: 0.22, bg: BG }, // safe zone: inner 80%
  { file: 'apple-touch-icon.png', size: 180, pad: 0.12, bg: BG },
];

const browser = await chromium.launch();
try {
  for (const t of targets) {
    const page = await browser.newPage({ viewport: { width: t.size, height: t.size }, deviceScaleFactor: 1 });
    const inner = Math.round(t.size * (1 - 2 * t.pad));
    const html = `<!doctype html><html><body style="margin:0;background:${t.bg};width:${t.size}px;height:${t.size}px;display:flex;align-items:center;justify-content:center">
      <div style="width:${inner}px;height:${inner}px">${svg.replace('<svg ', '<svg width="100%" height="100%" ')}</div></body></html>`;
    await page.setContent(html);
    await page.screenshot({ path: path.join(out, t.file), type: 'png', omitBackground: false });
    await page.close();
    console.log('wrote', t.file);
  }
} finally {
  await browser.close();
}
