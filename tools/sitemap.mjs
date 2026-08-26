// Reconcile sitemap.xml against the routes that actually exist.
//
//   node tools/sitemap.mjs            report: what is missing, what is orphaned
//   node tools/sitemap.mjs --check    same, but exit 1 if anything is missing (CI gate)
//   node tools/sitemap.mjs --write    add the missing entries
//
// WHY THIS EXISTS. sitemap.xml is hand-maintained, and the failure mode is silence:
// a page ships, nobody edits the XML, and it is simply never indexed. That is on the
// build-blocked list in Operating/articles-publishing.md in as many words - "a 17th
// article is invisible until edited" - and it bit on 2026-08-26, when /for-players
// turned out to have been missing: a real page, titled, no noindex, linked from three
// places, serving 200. It surfaced only because Mike happened to ask whether the
// sitemap needed updating.
//
// IT RECONCILES, IT DOES NOT REGENERATE. Existing entries are never rewritten. Their
// lastmod and priority are hand-tuned (1.0 home, 0.9 down to 0.2 for legal pages) and
// regenerating would flatten SEO signals nobody asked it to touch. This only ever adds
// what is absent, and reports - without deleting - anything present that no longer has
// a page behind it.

import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs';
import { execSync } from 'node:child_process';
import { join, relative, dirname } from 'node:path';

const ROOT = new URL('../src/PegboardWebSite/', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const PAGES = join(ROOT, 'Pages');
const SITEMAP = join(ROOT, 'wwwroot', 'sitemap.xml');
const BASE = 'https://www.epegboard.com';

const mode = process.argv.includes('--write') ? 'write'
  : process.argv.includes('--check') ? 'check' : 'report';

/** Every .cshtml under Pages/, recursively. */
function pageFiles(dir) {
  const out = [];
  for (const e of readdirSync(dir)) {
    const p = join(dir, e);
    if (statSync(p).isDirectory()) out.push(...pageFiles(p));
    else if (e.endsWith('.cshtml') && !e.startsWith('_')) out.push(p);
  }
  return out;
}

/**
 * The route a page serves.
 *
 * Two forms exist in this repo and a generator that knows only the first drops nine
 * URLs: `@page "/explicit"` (most of the guides and feature pages) and a bare `@page`,
 * which Razor routes by convention from the file path - Pages/Privacy.cshtml serves
 * /privacy, Pages/Guides/Index.cshtml serves /guides.
 *
 * Returns null for a page with no @page at all (a partial or a layout), and for
 * anything opting out with the marker below.
 */
function routeOf(file) {
  const src = readFileSync(file, 'utf8');
  if (/@\*\s*sitemap:ignore[\s\S]*?\*@/.test(src)) return null;   // opt-out; a reason after the marker is allowed
  const explicit = src.match(/^﻿?@page\s+"([^"]+)"/m);
  if (explicit) return explicit[1];
  if (!/^﻿?@page\s*$/m.test(src)) return null;         // not a routable page
  let rel = relative(PAGES, file).replace(/\\/g, '/').replace(/\.cshtml$/, '');
  if (/\/?Index$/.test(rel)) rel = dirname(rel) === '.' ? '' : dirname(rel);
  return '/' + rel.toLowerCase();
}

/** Priority for a route we have never seen before. Existing entries keep theirs. */
function defaultPriority(route) {
  if (route === '/') return '1.0';
  if (route.startsWith('/guides/')) return '0.8';
  if (route === '/guides') return '0.9';
  if (/^\/(privacy|terms|license|cookies)/.test(route)) return '0.2';
  return '0.7';
}

/** When the page last changed, so lastmod is honest rather than "today". */
function lastmod(file) {
  try {
    const d = execSync(`git log -1 --format=%cs -- "${file}"`, { cwd: ROOT, encoding: 'utf8' }).trim();
    if (/^\d{4}-\d{2}-\d{2}$/.test(d)) return d;
  } catch { /* not in git, or git unavailable */ }
  return new Date(statSync(file).mtime).toISOString().slice(0, 10);
}

// ---------------------------------------------------------------------------
const routes = new Map();                       // route -> file
for (const f of pageFiles(PAGES)) {
  const r = routeOf(f);
  if (r !== null) routes.set(r, f);
}

let xml = readFileSync(SITEMAP, 'utf8');
const inSitemap = new Set(
  [...xml.matchAll(/<loc>([^<]+)<\/loc>/g)].map(m => m[1].replace(BASE, '') || '/')
);

const missing = [...routes.keys()].filter(r => !inSitemap.has(r)).sort();
const orphans = [...inSitemap].filter(r => !routes.has(r)).sort();

console.log(`routes: ${routes.size}   sitemap entries: ${inSitemap.size}`);

if (orphans.length) {
  console.log('\nIn the sitemap with no page behind it (NOT removed - check before deleting,');
  console.log('a route may be served by middleware or a redirect rather than a .cshtml):');
  for (const o of orphans) console.log('   ' + o);
}

if (!missing.length) {
  console.log('\nNothing missing. Every route is in the sitemap.');
  process.exit(0);
}

console.log(`\nMISSING from the sitemap (${missing.length}):`);
for (const r of missing) console.log(`   ${r}   [${routes.get(r).split(/[\\/]/).pop()}]`);

if (mode === 'check') {
  console.log('\n--check: failing. Run with --write, or add @* sitemap:ignore *@ to the page.');
  process.exit(1);
}
if (mode === 'report') {
  console.log('\n(report only - pass --write to add them)');
  process.exit(0);
}

const block = missing.map(r =>
  `  <url>\n    <loc>${BASE}${r === '/' ? '/' : r}</loc>\n` +
  `    <lastmod>${lastmod(routes.get(r))}</lastmod>\n` +
  `    <priority>${defaultPriority(r)}</priority>\n  </url>`).join('\n');

xml = xml.replace('</urlset>', block + '\n</urlset>');
writeFileSync(SITEMAP, xml);
console.log(`\nAdded ${missing.length} entries. Existing entries untouched.`);
