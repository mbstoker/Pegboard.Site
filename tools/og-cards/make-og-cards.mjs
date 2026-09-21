#!/usr/bin/env node
/**
 * make-og-cards.mjs - per-page social share cards (1200x630) for the marketing site.
 *
 *   node tools/og-cards/make-og-cards.mjs [slug ...]     (no args = every card)
 *
 * WHY THIS EXISTS ALONGSIDE tools/make-og-image.ps1
 * -------------------------------------------------
 * make-og-image.ps1 builds the SITE-WIDE fallback card (Images/og-image.png) with
 * System.Drawing. It cannot build these: the brand face is Plus Jakarta Sans, which is a
 * Google font and is NOT installed on Windows, so System.Drawing silently falls back to
 * Segoe UI. That is exactly why the existing fallback card is off-palette and off-face
 * against the site it represents. Chromium fetches the real font, so these cards match
 * what a visitor actually sees when they follow the link.
 *
 * Chromium is also the estate's rasteriser of record - it is what
 * .agents/skills/_shared/svg-to-png.mjs uses for article diagrams, for the same reason.
 *
 * ALL TEXT IS REAL TEXT rendered by the browser, never drawn into the source bitmap and
 * never generated - Operating/articles-publishing.md section 2. The only raster input is
 * one of our own product screenshots.
 *
 * PLAYWRIGHT is not a dependency of this repo, which has no package.json and no
 * node_modules, and adding one for a tool that runs a handful of times a year is not
 * worth it. ESM resolves imports relative to THIS FILE, so `cd`-ing into a repo that has
 * playwright does not help. We therefore resolve it explicitly out of a sibling repo that
 * already installs it.
 */

import { createRequire } from 'node:module';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');
const imagesDir = join(repoRoot, 'src', 'PegboardWebSite', 'wwwroot', 'Images');
const imgDir = join(repoRoot, 'src', 'PegboardWebSite', 'wwwroot', 'img');

const W = 1200;
const H = 630;

/**
 * One entry per page that overrides ViewData["OgImage"]. A page NOT listed here keeps the
 * site-wide fallback, which is the right default - a card is worth making for a page people
 * deliberately share or land on from search, not for all thirty of them.
 *
 * headline  - the page's own H1, near enough that the card and the page agree.
 * subline   - what it does, in one line. Must not outrun the product: no "payments" (we
 *             track them, we do not collect them) and no booking - see the comment in
 *             Pages/BadmintonClubManagementSoftware.cshtml and /compare.
 * shot      - one of OUR screenshots. cropTop keeps the informative part when the shot is
 *             taller than the panel, instead of squashing the whole frame into a letterbox.
 */
const CARDS = [
  {
    slug: 'badminton-club-management-software',
    out: join(imagesDir, 'og-badminton-club-management-software.png'),
    headline: 'Badminton club management software',
    subline: 'Balanced games, live ratings, attendance and competitions - free for badminton clubs.',
    shot: join(imgDir, 'site-dashboard.png'),
  },
];

function template({ headline, subline, shotUrl, markUrl }) {
  return `<!doctype html>
<html><head><meta charset="utf-8">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;600;800&display=swap" rel="stylesheet">
<style>
  /* Palette lifted from wwwroot/css/marketing.css :root - navy, navy-2, the muted blue the
     footer and .engine-strip use for body copy on dark. Keep them in step by hand; a card
     that drifts off the site's blues is worse than no card. */
  :root{ --navy:#1f3d5b; --navy-2:#294f76; --on-dark:#bcd2ea; --shuttle:#e0a106; }
  *{box-sizing:border-box;margin:0;padding:0}
  html,body{width:${W}px;height:${H}px}
  body{
    font-family:'Plus Jakarta Sans',sans-serif;
    background:linear-gradient(135deg,var(--navy-2),var(--navy));
    color:#fff; overflow:hidden; position:relative;
  }
  /* The same radial lift the page heroes have, so the card reads as the same surface. */
  body::before{content:"";position:absolute;inset:0;
    background:radial-gradient(900px 420px at 12% -20%,rgba(124,176,224,.28),transparent 70%)}
  .card{position:relative;height:100%;padding:42px 56px 0;display:flex;flex-direction:column}
  .brand{display:flex;align-items:center;gap:11px}
  .brand img{height:38px;width:auto;display:block}
  .brand span{font-size:26px;font-weight:800;letter-spacing:-.02em}
  /* 52px is the largest size at which the longest headline here still sets on ONE line
     inside the 1088px content width. A headline that stacks to three lines pushes the
     screenshot off the card and leaves the right half empty. Check any new headline at
     this size before adding it. */
  h1{margin-top:24px;font-size:52px;line-height:1.06;font-weight:800;letter-spacing:-.028em}
  .sub{margin-top:14px;font-size:22px;line-height:1.35;font-weight:400;color:var(--on-dark)}
  /* Screenshot panel: bleeds off the bottom edge on purpose, so it reads as a real screen
     continuing past the card rather than a thumbnail floating on a background. The height
     is chosen to land BELOW the court cards in site-dashboard.png rather than through them
     - a crop that slices a row of player names in half looks like a broken image. */
  .panel{
    margin-top:auto;height:352px;border-radius:14px 14px 0 0;overflow:hidden;
    border:1px solid rgba(255,255,255,.16);border-bottom:0;background:#fff;
    box-shadow:0 -18px 50px -18px rgba(3,14,30,.55);
  }
  .panel img{display:block;width:100%;height:auto}
  .flag{position:absolute;right:56px;top:50px;display:flex;gap:10px}
  .flag b{font-size:15px;font-weight:600;color:#0f2c49;background:var(--shuttle);
          padding:7px 15px;border-radius:999px;letter-spacing:.01em}
</style></head>
<body><div class="card">
  <div class="brand"><img src="${markUrl}" alt=""><span>ePegboard</span></div>
  <div class="flag"><b>Free for clubs</b></div>
  <h1>${headline}</h1>
  <p class="sub">${subline}</p>
  <div class="panel"><img src="${shotUrl}" alt=""></div>
</div></body></html>`;
}

async function dataUri(file) {
  const bytes = await readFile(file);
  return `data:image/png;base64,${bytes.toString('base64')}`;
}

async function resolvePlaywright() {
  const candidates = [
    join(repoRoot, '..', 'PegboardWeb', 'perf'),
    join(repoRoot, '..', 'Pegboard.MarketingApp', 'dev-verify'),
  ];
  for (const dir of candidates) {
    const pkg = join(dir, 'node_modules', 'playwright', 'package.json');
    if (!existsSync(pkg)) continue;
    const require = createRequire(pathToFileURL(join(dir, 'noop.js')));
    return require('playwright');
  }
  throw new Error(
    'playwright is not installed in any sibling repo. Install it in one of:\n' +
    candidates.map((d) => `  ${d}  (npm install)`).join('\n')
  );
}

async function main() {
  const wanted = process.argv.slice(2);
  const cards = wanted.length ? CARDS.filter((c) => wanted.includes(c.slug)) : CARDS;
  if (!cards.length) {
    throw new Error(`no card matches ${wanted.join(', ')}. Known: ${CARDS.map((c) => c.slug).join(', ')}`);
  }

  const { chromium } = await resolvePlaywright();
  const browser = await chromium.launch();
  // deviceScaleFactor 1: og:image:width/height in _LayoutMarketing.cshtml are hard-coded
  // 1200x630, so the file has to actually BE that. A 2x card would be a lie in the meta.
  const page = await browser.newPage({ viewport: { width: W, height: H }, deviceScaleFactor: 1 });

  // Inlined as data: URIs, NOT file:// - a page created with setContent has an opaque
  // origin, and Chromium refuses file:// subresources from one. They fail silently: the
  // first run of this script produced a card with an empty white panel where the
  // screenshot should have been, and no error anywhere.
  const markUrl = await dataUri(join(imagesDir, 'epegboard-figure-logo.png'));

  for (const card of cards) {
    const shotUrl = await dataUri(card.shot); // throws here if the shot has moved
    await page.setContent(template({ ...card, shotUrl, markUrl }), { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts.ready);

    // If Google Fonts did not answer, the card silently falls back to a system face and
    // stops being ours. Better to fail than to ship an off-brand card nobody re-checks.
    const brandFont = await page.evaluate(() => document.fonts.check('800 54px "Plus Jakarta Sans"'));
    if (!brandFont) throw new Error('Plus Jakarta Sans did not load - check network access to fonts.googleapis.com');

    await page.screenshot({ path: card.out, type: 'png' });
    console.log(`${card.slug} -> ${card.out} (${W}x${H})`);
  }

  await browser.close();
}

main().catch((err) => {
  console.error(err.message);
  process.exit(1);
});
