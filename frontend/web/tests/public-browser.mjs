// Read-only public/auth browser checks. Use an existing external Playwright installation.
// NIDHI_PLAYWRIGHT_MODULE: absolute driver module path; no project dependency needed.
import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
if (!process.env.NIDHI_PLAYWRIGHT_MODULE) throw new Error('Set NIDHI_PLAYWRIGHT_MODULE to an existing Playwright module.');
const { chromium } = await import(process.env.NIDHI_PLAYWRIGHT_MODULE);
const origin = process.env.NIDHI_PUBLIC_ORIGIN || 'http://localhost:3000';
assert.ok(['localhost', '127.0.0.1'].includes(new URL(origin).hostname));
const output = process.env.NIDHI_PUBLIC_SCREENSHOTS || '/private/tmp/nidhi-public-review';
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.NIDHI_CHROME_PATH, headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
const page = await context.newPage();
const failures = [];
let expectedMissingPage = false;
page.on('pageerror', error => failures.push(error.message));
page.on('console', message => { if (message.type() === 'error' && !message.text().includes('401 (Unauthorized)') && !(expectedMissingPage && message.text().includes('404 (Not Found)'))) failures.push(message.text()); });
page.on('response', response => {
  if (response.status() >= 400 && !response.url().includes('/auth/me') && !response.url().includes('/missing-public-page')) failures.push(`${response.status()} ${new URL(response.url()).pathname}`);
});
async function visit(route) {
  expectedMissingPage = route === "/missing-public-page";
  await page.goto(origin + route);
  await page.locator('h1').waitFor();
  await page.evaluate(() => document.fonts.ready);
  assert.equal(await page.locator('h1').count(), 1);
}
async function noOverflow(label) {
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Page overflow: ${label}`);
  const clipped = await page.locator('main').evaluate(main => [...main.querySelectorAll('p,h1,h2,h3,a')].filter(el => {
    const style = getComputedStyle(el), r = el.getBoundingClientRect();
    return style.display !== 'none' && r.width > 0 && (r.left < -1 || r.right > innerWidth + 1 || el.scrollWidth > el.clientWidth + 1);
  }).map(el => el.textContent));
  assert.deepEqual(clipped, [], `Text overflow: ${label}`);
}
try {
  for (const width of [320, 390, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const route of ['/', '/login', '/register', '/forgot-password', '/verify-email', '/reset-password', '/missing-public-page']) {
      await visit(route);
      await noOverflow(`${route} ${width}`);
      if (route === '/') {
        await page.getByRole('navigation', { name: 'Main navigation' }).waitFor();
        const holding = page.locator('.floating-holding-card');
        assert.ok(await holding.isVisible());
        assert.match(await holding.innerText(), /DEMO/);
        const loops = await page.locator('.landing-page').evaluate(root => [...root.querySelectorAll('*')].filter(el => getComputedStyle(el).animationIterationCount === 'infinite').length);
        assert.equal(loops, 0);
      } else if (route !== '/missing-public-page') {
        assert.match(await page.locator('meta[name="robots"]').getAttribute('content'), /noindex/);
        assert.equal(await page.locator('input').first().isVisible(), true);
      }
      await page.screenshot({ path: `${output}/${route === '/' ? 'home' : route.slice(1)}-${width}.png`, fullPage: true, animations: 'disabled' });
    }
    console.log(`PASS public, five auth routes and 404 at ${width}px; no page/text overflow`);
  }
  await visit('/');
  for (const label of ['How it works', 'Features', 'Simulation']) {
    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: label, exact: true }).click();
    assert.ok(new URL(page.url()).hash);
  }
  await page.getByRole('link', { name: 'Create your account', exact: true }).click();
  await page.waitForURL('**/register');
  await visit('/');
  await page.locator('.header-actions').getByRole('link', { name: 'Sign in', exact: true }).click();
  await page.waitForURL('**/login');
  await visit('/');
  await page.keyboard.press('Tab');
  assert.equal(await page.evaluate(() => document.activeElement.textContent), 'Skip to content');
  await page.keyboard.press('Enter');
  assert.equal(await page.evaluate(() => document.activeElement.id), 'main-content');
  await page.keyboard.press('Tab');
  assert.equal(await page.evaluate(() => document.activeElement.textContent), 'Create account');
  assert.notEqual(await page.evaluate(() => getComputedStyle(document.activeElement).outlineStyle), 'none');
  await page.keyboard.press('Enter'); await page.waitForURL('**/register');
  await page.getByLabel('Email address', { exact: true }).fill('review@example.test');
  await page.getByLabel('Password', { exact: true }).fill('lowercaseonlypassword');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await page.locator('.alert-error').waitFor();
  assert.equal(await page.getByLabel('Password', { exact: true }).getAttribute('aria-invalid'), 'true');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  for (const route of ['/', '/login']) {
    await visit(route);
    assert.equal(await page.locator('main').evaluate(main => [...main.querySelectorAll('*')].filter(el => getComputedStyle(el).animationName !== 'none').length), 0);
    await page.screenshot({ path: `${output}/reduced-${route === '/' ? 'home' : 'login'}.png`, fullPage: true });
  }
  // 200% browser zoom equivalent: half the CSS viewport while preserving the layout checks.
  await page.setViewportSize({ width: 720, height: 500 });
  await visit('/'); await noOverflow('200% desktop zoom equivalent');
  assert.equal(await page.title(), 'Nidhi — Simulated Gold Micro-Savings');
  assert.match(await page.locator('meta[property="og:title"]').getAttribute('content'), /Simulated/);
  assert.equal(await page.locator('link[rel="canonical"]').count(), 0);
  const robots = await context.request.get(origin + '/robots.txt');
  assert.equal(robots.status(), 200); assert.match(await robots.text(), /Disallow: \/admin/);
  await visit('/missing-public-page');
  await page.getByRole('link', { name: 'Home', exact: true }).click(); await page.waitForURL(origin + '/');
  const ax = await page.locator('main').ariaSnapshot();
  assert.match(ax, /heading "Build a saving habit, measured in gold." \[level=1\]/);
  assert.doesNotMatch(ax, /1\.4825/); // Decorative preview is not read as customer data.
  assert.deepEqual(failures, []);
  console.log('PASS anchors, account CTAs, keyboard/skip/focus, auth validation, reduced motion, zoom equivalent, metadata, robots, accessible tree and clean runtime');
} finally { await browser.close(); }
