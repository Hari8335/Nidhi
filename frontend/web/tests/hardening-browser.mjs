// Live acceptance checks; use only a freshly migrated disposable localhost database.
// NIDHI_BROWSER_CONFIG is an owner-only JSON file, as in browser-smoke.mjs.
// Reuses an external Playwright installation; adds no project dependency.
import assert from 'node:assert/strict';
import { readFile, readdir, mkdir, writeFile } from 'node:fs/promises';
import { randomBytes } from 'node:crypto';
const config = JSON.parse(await readFile(process.env.NIDHI_BROWSER_CONFIG, 'utf8'));
const { chromium } = await import(config.playwrightModule);
const origin = config.baseUrl;
assert.ok(['localhost', '127.0.0.1'].includes(new URL(origin).hostname));
await mkdir(config.outputDirectory, { recursive: true });
const browser = await chromium.launch({ executablePath: config.executablePath, headless: true });
const customerContext = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
const adminContext = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
const page = await customerContext.newPage(), admin = await adminContext.newPage();
const faults = []; let injecting = false, stage = 'start';
for (const p of [page, admin]) {
  p.on('pageerror', e => faults.push(e.name));
  p.on('console', message => {
    if (message.type() === 'warning' && /hydration|key|React/.test(message.text())) faults.push('React warning');
    if (message.type() === 'error' && !/status of (400|401|403|404|409|422|429|503)\b/.test(message.text()) && !(injecting && /ERR_FAILED/.test(message.text()))) faults.push(message.text());
  });
}
const email = `hardening-${randomBytes(8).toString('hex')}@example.test`;
const password = `Aa1!${randomBytes(20).toString('hex')}`, nextPassword = `Bb2!${randomBytes(20).toString('hex')}`;
const log = text => console.log(`PASS ${text}`);
async function visit(p, path) { await p.goto(origin + path); await p.locator('h1').waitFor(); }
async function field(p, label, value) { const input = p.getByLabel(label, { exact: true }); await input.focus(); await input.fill(value); }
async function enter(p, name, scope = p) { const button = scope.getByRole('button', { name, exact: true }); await button.waitFor({ state: 'visible' }); for (let attempt = 0; attempt < 200 && !await button.isEnabled(); attempt++) await p.waitForTimeout(25); assert.ok(await button.isEnabled(), `${name} must become enabled`); await button.focus(); await p.keyboard.press('Enter'); }
async function text(p, value) { await p.getByText(value, { exact: false }).first().waitFor(); }
async function api(context, path, body, headers = {}) {
  if (body === undefined) return context.request.get(origin + '/api/v1' + path);
  const csrf = await context.request.get(origin + '/api/v1/auth/antiforgery');
  assert.equal(csrf.status(), 200);
  return context.request.post(origin + '/api/v1' + path, { data: body, headers: { 'X-CSRF-TOKEN': (await csrf.json()).token, ...headers } });
}
async function json(context, path) { const r = await api(context, path); assert.equal(r.status(), 200); return r.json(); }
async function pickup(purpose) {
  for (let attempt = 0; attempt < 50; attempt++) {
    for (const name of await readdir(config.emailPickupDirectory)) {
      const mail = JSON.parse(await readFile(`${config.emailPickupDirectory}/${name}`, 'utf8'));
      if (mail.email === email && mail.purpose === purpose) return mail;
    }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('Private pickup missing');
}
async function login(p, address, secret, route) {
  await visit(p, '/login'); await field(p, 'Email address', address); await field(p, 'Password', secret); await enter(p, 'Sign in'); await p.waitForURL('**' + route);
}
async function publish(value) {
  await visit(admin, '/admin/prices'); await field(admin, 'LKR per gram', value); await field(admin, 'Publication reason', 'Session 13 local simulation');
  await enter(admin, 'Review publication');
  const response = admin.waitForResponse(r => r.url().endsWith('/api/v1/admin/prices') && r.request().method() === 'POST');
  await enter(admin, 'Confirm publication'); const result = await response; assert.equal(result.status(), 201);
  await text(admin, 'Price published successfully'); return result.json();
}
async function noOverflow(p, label) {
  const measured = await p.evaluate(() => ({ width: innerWidth, page: document.documentElement.scrollWidth,
    clipped: [...document.querySelectorAll('main p, main h1, main h2, main h3, main dd, main input, main button')].filter(el => {
      const r = el.getBoundingClientRect(), s = getComputedStyle(el);
      return r.width && s.visibility !== 'hidden' && s.display !== 'none' && (r.left < -1 || r.right > innerWidth + 1);
    }).map(el => el.tagName) }));
  assert.ok(measured.page <= measured.width, `${label}: page overflow`); assert.deepEqual(measured.clipped, [], `${label}: clipped content`);
  assert.equal(await p.locator('h1').count(), 1);
}
async function screenshot(p, label) { await p.screenshot({ path: `${config.outputDirectory}/${label}.png`, fullPage: true, animations: 'disabled' }); }
const results = {};
try {
  stage = 'new customer and identity';
  await visit(page, '/'); await page.getByRole('link', { name: 'Create your account', exact: true }).focus(); await page.keyboard.press('Enter'); await page.waitForURL('**/register');
  await field(page, 'Display name (optional)', 'Hardening <review>'); await field(page, 'Email address', email); await field(page, 'Password', password); await enter(page, 'Create account');
  await text(page, 'Check your inbox');
  await login(page, email, password, '/dashboard'); await text(page, 'email is not verified');
  assert.equal((await api(customerContext, '/customer/wallet/fundings', { amountLkr: 100 }, { 'Idempotency-Key': crypto.randomUUID() })).status(), 403);
  const verify = await pickup('verification');
  await visit(page, `/verify-email#userId=${verify.userId}&token=${encodeURIComponent(verify.token)}`); await enter(page, 'Verify email'); await text(page, 'Email verified');
  await visit(page, '/dashboard'); await text(page, 'Welcome');
  await visit(page, '/transactions'); await text(page, 'No transactions yet.');
  await visit(page, '/goals'); await text(page, 'Create a savings goal');
  await visit(page, '/wallet'); await text(page, 'LKR 0.00');
  const cookie = (await customerContext.cookies()).find(c => c.name === '.Nidhi.Session');
  assert.ok(cookie.httpOnly && cookie.sameSite === 'Strict' && cookie.expires === -1);
  await page.reload(); await text(page, 'LKR 0.00');
  await visit(page, '/gold'); await text(page, 'price is unavailable');
  assert.equal(await page.getByLabel('Amount to allocate (LKR)', { exact: true }).count(), 0);
  log('registration, unverified denial, private email verification, customer login, zero/empty states, reload, cookie flags and no-price blocking');

  stage = 'minimum funding and uncertain funding retry';
  await visit(page, '/wallet'); await enter(page, '+ Add Simulated Funds');
  const dialog = page.getByRole('dialog'); await dialog.waitFor(); assert.equal(await dialog.evaluate(el => el.matches(':modal')), true);
  await field(page, 'Amount to Add (LKR)', '99.99'); assert.equal(await dialog.getByRole('button', { name: /Confirm & Add/ }).isDisabled(), true);
  await field(page, 'Amount to Add (LKR)', '100.00'); await enter(page, 'Confirm & Add LKR 100.00', dialog); await text(page, 'Funds Added Successfully');
  assert.equal(await page.evaluate(() => document.activeElement.id), 'funding-modal-title');
  await screenshot(page, 'funding-receipt'); await enter(page, 'Done', dialog);
  await text(page, 'LKR 100.00'); assert.equal(await page.evaluate(() => document.activeElement.textContent.trim()), '+ Add Simulated Funds');
  await enter(page, '+ Add Simulated Funds'); await field(page, 'Amount to Add (LKR)', '1000.00');
  const fundingCalls = []; let loseFunding = true;
  await page.route('**/api/v1/customer/wallet/fundings', async route => {
    fundingCalls.push({ key: route.request().headers()['idempotency-key'], body: route.request().postDataJSON() });
    if (loseFunding) { loseFunding = false; await route.fetch(); await route.abort(); } else await route.continue();
  });
  injecting = true; await enter(page, 'Confirm & Add LKR 1,000.00', dialog); await text(page, 'outcome is uncertain');
  assert.equal(await page.getByLabel('Amount to Add (LKR)', { exact: true }).isDisabled(), true);
  await enter(page, 'Retry Safe Funding', dialog); await text(page, 'Funds Added Successfully');
  assert.deepEqual(fundingCalls[0], fundingCalls[1]); assert.equal((await json(customerContext, '/customer/transactions')).totalCount, 2);
  await page.unroute('**/api/v1/customer/wallet/fundings'); injecting = false; await enter(page, 'Done', dialog);
  log('minimum funding, inline limits, native modal focus/return, lost committed funding response, same-key replay and one credit');

  stage = 'goal and first price';
  await visit(page, '/goals'); await field(page, 'Target simulated gold (grams)', '0.00500000'); await field(page, 'Target date (optional)', '2026-12-31'); await enter(page, 'Create savings goal'); await text(page, 'Savings goal created.');
  await login(admin, config.adminEmail, config.adminPassword, '/admin');
  assert.equal((await api(adminContext, '/customer/wallet/fundings', { amountLkr: 100 }, { 'Idempotency-Key': crypto.randomUUID() })).status(), 403);
  const priceA = await publish('25000.0000');
  await visit(page, '/gold'); await text(page, 'Current simulated price'); await field(page, 'Amount to allocate (LKR)', '100.00');
  const input = page.getByLabel('Amount to allocate (LKR)', { exact: true });
  assert.ok(await input.evaluate(el => { const s = getComputedStyle(el); return s.borderStyle !== 'none' && parseFloat(s.height) >= 44 && s.backgroundColor !== 'rgba(0, 0, 0, 0)'; }));
  assert.equal(await page.getByText(priceA.priceVersionId, { exact: false }).count(), 0);
  await enter(page, 'Review purchase');
  const priceB = await publish('26000.0000');
  const before = { wallet: await json(customerContext, '/customer/wallet'), holding: await json(customerContext, '/customer/holdings'), history: await json(customerContext, '/customer/transactions') };
  const purchases = [];
  page.on('request', r => { if (r.url().endsWith('/api/v1/customer/gold-purchases') && r.method() === 'POST') purchases.push({ key: r.headers()['idempotency-key'], body: r.postDataJSON() }); });
  const rejectedResponse = page.waitForResponse(r => r.url().endsWith('/api/v1/customer/gold-purchases') && r.status() === 409);
  await enter(page, 'Confirm purchase'); const rejected = await rejectedResponse;
  assert.equal((await rejected.json()).code, 'PRICE_CHANGED');
  await page.getByRole('heading', { name: 'Gold price changed', exact: true }).waitFor(); await text(page, 'LKR 26,000.0000 / g');
  const change = page.locator('.price-change'); await text(page, 'No purchase was made.');
  assert.match(await change.innerText(), /LKR 25,000.0000 \/ g/); assert.match(await change.innerText(), /0.00384615 g/); assert.match(await change.innerText(), /LKR 100.00/);
  assert.equal(await page.evaluate(() => document.activeElement.id), 'price-change-title');
  assert.equal(purchases.length, 1); assert.equal(purchases[0].body.priceVersionId, priceA.priceVersionId);
  assert.equal((await json(customerContext, '/customer/wallet')).balanceLkr, before.wallet.balanceLkr);
  assert.equal((await json(customerContext, '/customer/holdings')).quantityGrams, before.holding.quantityGrams);
  assert.equal((await json(customerContext, '/customer/transactions')).totalCount, before.history.totalCount);
  await screenshot(page, 'price-changed-desktop');
  await page.setViewportSize({ width: 320, height: 800 }); await noOverflow(page, 'PRICE_CHANGED 320'); await screenshot(page, 'price-changed-320');
  await enter(page, 'Review new price'); assert.equal(purchases.length, 1); await page.getByRole('heading', { name: 'Review your updated purchase', exact: true }).waitFor();
  const successResponse = page.waitForResponse(r => r.url().endsWith('/api/v1/customer/gold-purchases') && r.status() === 201);
  await enter(page, 'Confirm updated purchase'); const receipt = await (await successResponse).json(); await text(page, 'Purchase successful');
  assert.equal(purchases.length, 2); assert.notEqual(purchases[0].key, purchases[1].key); assert.equal(purchases[1].body.priceVersionId, priceB.priceVersionId);
  assert.equal(receipt.priceVersionId, priceB.priceVersionId); assert.equal(receipt.goldQuantityGrams, 0.00384615);
  const after = await json(customerContext, '/customer/transactions'); assert.equal(after.totalCount, before.history.totalCount + 1); assert.equal(after.items.filter(t => t.type === 'GOLD_PURCHASE').length, 1);
  results.priceChanged = { rejectedStatus: 409, priorPrice: priceA.pricePerGramLkr, currentPrice: priceB.pricePerGramLkr, amount: receipt.amountLkr, grams: receipt.goldQuantityGrams, requests: purchases.length, newKey: true, noMutationOnRejection: true };
  log('live Price A → Admin Price B → HTTP 409, unchanged persisted API balances/history, explicit accessible comparison, keyboard review/reconfirmation, new key and exactly one purchase');

  stage = 'lost purchase response';
  await page.setViewportSize({ width: 1440, height: 1000 }); await page.reload(); await text(page, 'Current simulated price');
  await field(page, 'Amount to allocate (LKR)', '100'); await enter(page, 'Review purchase'); let losePurchase = true;
  await page.route('**/api/v1/customer/gold-purchases', async route => { if (losePurchase) { losePurchase = false; await route.fetch(); await route.abort(); } else await route.continue(); });
  injecting = true; await enter(page, 'Confirm purchase'); await text(page, 'We could not connect.');
  const replayResponse = page.waitForResponse(r => r.url().endsWith('/api/v1/customer/gold-purchases') && r.status() === 200);
  await enter(page, 'Confirm purchase'); const replay = await (await replayResponse).json(); await text(page, 'Purchase successful');
  assert.deepEqual(purchases[2], purchases[3]); assert.equal((await json(customerContext, '/customer/transactions')).items.filter(t => t.type === 'GOLD_PURCHASE').length, 2);
  await page.unroute('**/api/v1/customer/gold-purchases'); injecting = false;
  await visit(page, '/goals'); await text(page, 'reached');
  assert.equal(await page.getByRole('progressbar').getAttribute('aria-valuenow'), '100');
  await visit(page, '/dashboard'); await text(page, 'Welcome'); await text(page, '0.00769230');
  assert.equal(await page.locator('.history-reference').count(), 0);
  await visit(page, `/transactions/${replay.transactionId}`); await text(page, replay.transactionId); await text(page, priceB.priceVersionId);
  await page.reload(); await text(page, replay.transactionId); await page.goBack(); await page.goForward(); await text(page, replay.transactionId);
  assert.equal(purchases.length, 4);
  log('lost committed purchase response uses identical key/content, immutable replay, exact holdings, achieved/capped goal, receipt refresh/back/forward without resubmission');

  stage = 'responsive and semantics';
  const customer = await json(customerContext, '/auth/me');
  const publicRoutes = ['/', '/login', '/register', '/verify-email', '/forgot-password', '/reset-password', '/missing-hardening-page'];
  const customerRoutes = ['/dashboard', '/wallet', '/gold', '/transactions', '/goals', '/profile', `/transactions/${replay.transactionId}`];
  const adminRoutes = ['/admin', '/admin/customers', `/admin/customers/${customer.userId}`, '/admin/transactions', `/admin/transactions/${replay.transactionId}`, `/admin/ledger?transactionId=${replay.transactionId}`, '/admin/prices', '/admin/audit-events'];
  for (const width of [320, 390, 768, 1024, 1440]) {
    for (const p of [page, admin]) await p.setViewportSize({ width, height: 1000 });
    for (const route of [...publicRoutes, ...customerRoutes]) { await visit(page, route); await page.waitForFunction(() => document.querySelectorAll('main .loading-state').length === 0); await noOverflow(page, `${route} ${width}`); if (width === 320 || width === 1440) await screenshot(page, `${route.split('/')[1] || 'home'}-${width}`); }
    for (const route of adminRoutes) { await visit(admin, route); await admin.waitForFunction(() => document.querySelectorAll('main .loading-state').length === 0); await noOverflow(admin, `${route} ${width}`); }
    log(`all public/auth/customer/admin routes and details at ${width}px without page/content overflow`);
  }
  await visit(page, '/wallet'); await enter(page, '+ Add Simulated Funds');
  const first = dialog.getByRole('button', { name: 'Close dialog' }); await first.focus(); await page.keyboard.press('Shift+Tab');
  assert.equal(await page.evaluate(() => document.querySelector('dialog').contains(document.activeElement)), true);
  await page.keyboard.press('Escape'); await dialog.waitFor({ state: 'hidden' });
  for (const [p, routes] of [[page, ['/', '/login', '/gold']], [admin, ['/admin/prices']]]) {
    await p.emulateMedia({ reducedMotion: 'reduce' });
    for (const route of routes) { await visit(p, route); await p.waitForFunction(() => document.querySelectorAll('main .loading-state').length === 0); assert.equal(await p.locator('main').evaluate(el => [...el.querySelectorAll('*')].filter(x => getComputedStyle(x).animationName !== 'none').length), 0); }
  }
  const ax = await page.locator('main').ariaSnapshot(); assert.match(ax, /Amount to allocate/);
  const header = await customerContext.request.get(origin + '/login');
  assert.equal(header.headers()['x-frame-options'], 'DENY'); assert.match(header.headers()['content-security-policy'], /frame-ancestors 'none'/);
  assert.equal(header.headers()['x-powered-by'], undefined);
  log('native modal keyboard containment/Escape, reduced motion across experiences, accessible form names and production frontend headers');

  stage = 'unavailable data recovery and session expiry';
  await visit(page, '/gold'); injecting = true;
  await page.route('**/api/v1/customer/gold-price', r => r.abort()); await enter(page, 'Refresh price'); await text(page, 'Unable to load current price and balances');
  assert.equal(await page.locator('.gold-purchase-card').count(), 0);
  await page.unroute('**/api/v1/customer/gold-price'); await enter(page, 'Refresh price'); await text(page, 'Current simulated price'); injecting = false;
  await admin.route('**/api/v1/admin/customers**', r => r.abort()); injecting = true; await visit(admin, '/admin/customers'); await text(admin, 'We could not connect.'); await admin.unroute('**/api/v1/admin/customers**'); await enter(admin, 'Retry'); await text(admin, email); injecting = false;
  await visit(page, '/gold'); await customerContext.clearCookies(); await enter(page, 'Refresh price'); await page.waitForURL('**/login?**'); await text(page, 'Your session has ended.'); assert.match(page.url(), /returnTo=%2Fgold/);
  assert.equal(await page.locator('.holding-quantity').count(), 0);
  await field(page, 'Email address', email); await field(page, 'Password', password); await enter(page, 'Sign in'); await page.waitForURL('**/gold'); await text(page, 'Current simulated price');
  await visit(admin, '/admin'); await adminContext.clearCookies(); await enter(admin, 'Refresh dashboard'); await admin.waitForURL('**/login?**'); await text(admin, 'Your session has ended.');
  await login(admin, config.adminEmail, config.adminPassword, '/admin');
  await visit(page, '/admin'); await page.waitForURL('**/dashboard'); await visit(admin, '/wallet'); await admin.waitForURL('**/admin');
  log('customer/admin API failure and retry, 401 clears protected content, expiry notice and intended-route resume, CUSTOMER/ADMIN shell isolation');

  stage = 'password recovery and token/session revocation';
  const copiedCookies = await customerContext.cookies();
  await visit(page, '/forgot-password'); await field(page, 'Email address', email); await enter(page, 'Send reset instructions'); await text(page, 'If an account exists');
  const reset = await pickup('password-reset');
  await visit(page, `/reset-password#userId=${reset.userId}&token=${encodeURIComponent(reset.token)}`);
  await field(page, 'New password', nextPassword); await field(page, 'Confirm new password', nextPassword); await enter(page, 'Reset password'); await text(page, 'Your password has been reset.');
  const copied = await browser.newContext(); await copied.addCookies(copiedCookies); assert.equal((await api(copied, '/auth/me')).status(), 401); await copied.close();
  await login(page, email, nextPassword, '/dashboard');
  const reused = await api(customerContext, '/auth/reset-password', { userId: reset.userId, token: reset.token, newPassword: password }); assert.equal(reused.status(), 400); assert.equal((await reused.json()).code, 'INVALID_TOKEN');
  await enter(page, 'Sign out'); await page.waitForURL(url => url.pathname === '/login'); assert.equal((await api(customerContext, '/auth/me')).status(), 401);
  assert.deepEqual(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length })), { local: 0, session: 0 });
  assert.deepEqual(faults, []);
  await writeFile(`${config.outputDirectory}/results.json`, JSON.stringify({ ...results, responsive: [320, 390, 768, 1024, 1440], consoleErrors: faults, journeys: 'passed' }, null, 2));
  log('password recovery/reset, copied-session revocation, reused-token rejection, new-password login, logout, empty browser storage and clean runtime');
  console.log('ALL LIVE HARDENING BROWSER CHECKS PASSED');
} catch (e) {
  // Never print request bodies, cookies or identity-email credentials.
  console.error(`FAILED ${stage}: ${e.name}. ${String(e.message).replace(/hardening-[^\s@]+@example\.test/g, '[test account]').slice(0, 500)}`);
  await screenshot(page, 'failure').catch(() => {}); process.exitCode = 1;
} finally { await browser.close(); }
