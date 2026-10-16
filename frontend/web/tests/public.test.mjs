import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, access } from 'node:fs/promises';
import robots from '../src/app/robots.ts';

const read = path => readFile(new URL('../src/' + path, import.meta.url), 'utf8');
const page = await read('app/page.tsx');
const visual = await read('components/shared/gold-savings-visual.tsx');
const auth = await read('components/auth/auth-form.tsx');

test('public actions use implemented account routes and every anchor exists', async () => {
  const links = [...page.matchAll(/href="([^"]+)"/g)].map(match => match[1]);
  assert.ok(links.filter(link => link === '/register').length >= 3);
  assert.ok(links.includes('/login'));
  for (const link of links) {
    if (link.startsWith('#')) assert.ok(page.includes(`id="${link.slice(1)}"`), link);
    else await access(new URL(`../src/app/(auth)${link}/page.tsx`, import.meta.url));
  }
  assert.ok(!links.includes('#'));
});
test('hero discloses simulated wallet, purchases and holdings before product explanations', () => {
  const hero = page.slice(page.indexOf('<section className="hero-section"'), page.indexOf('<section id="how-it-works"'));
  assert.match(hero, /Wallet balances, gold purchases and holdings are simulated/);
  assert.match(hero, /No real money or physical gold is handled/);
  assert.match(hero, /simulated LKR/);
});
test('how it works describes the three implemented actions in order', () => {
  const steps = page.slice(page.indexOf('<ol'), page.indexOf('</ol>'));
  assert.equal((steps.match(/<li /g) || []).length, 3);
  assert.ok(steps.indexOf('Add simulated LKR') < steps.indexOf('Allocate to simulated gold'));
  assert.ok(steps.indexOf('Allocate to simulated gold') < steps.indexOf('Follow your progress'));
});
test('goals use total holding and do not promise reservations or automation', () => {
  assert.match(page, /current total simulated holding/);
  assert.match(page, /does not reserve gold or make automatic purchases/);
  assert.match(page, /optional target date/);
});
test('public and auth copy contain no unsupported claims or invented social proof', async () => {
  const copy = [page, visual, auth, await read('app/(auth)/layout.tsx')].join('\n');
  assert.doesNotMatch(copy, /24K|99\.9%|bank.grade|military.grade|trusted by|testimonials?|beat inflation|guaranteed returns|grow your wealth|enduring gold|Audit-grade|ledger precision|Lorem ipsum|Coming soon|future simulated financial|Session (?:8|9|10|11)|href="#"/i);
  assert.doesNotMatch(page, /<blockquote|star.rating|partner.logo|press.logo/i);
  assert.match(visual, /DEMO/);
  assert.match(visual, /SIMULATION PREVIEW/);
  assert.match(visual, /Demo goal:/);
});
test('public metadata is factual and has no invented canonical or deployment domain', () => {
  assert.match(page, /Nidhi — Simulated Gold Micro-Savings/);
  assert.match(page, /openGraph:/);
  assert.match(page, /twitter:/);
  assert.doesNotMatch(page, /metadataBase|canonical|https?:\/\//);
});
test('robots covers every private route family; private layouts use noindex', async () => {
  const rules = robots().rules;
  for (const route of ['/api/', '/admin', '/dashboard', '/wallet', '/gold', '/transactions', '/goals', '/profile', '/verify-email', '/reset-password']) assert.ok(rules.disallow.includes(route));
  assert.equal(robots().sitemap, undefined);
  for (const layout of ['(auth)', '(customer)', 'admin']) assert.match(await read(`app/${layout}/layout.tsx`), /index: false, follow: false/);
});
test('auth links remain accessible and 404 offers implemented recovery routes', async () => {
  assert.doesNotMatch(await read('app/(auth)/layout.tsx'), /aside[^>]*aria-hidden/);
  const missing = await read('app/not-found.tsx');
  assert.match(missing, /id="main-content"/);
  assert.match(missing, /href="\/"/);
  assert.match(missing, /href="\/login"/);
});
