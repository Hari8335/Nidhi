// UI-only checks use intercepted API responses; no real email or account changes.
import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
const { chromium } = await import(process.env.NIDHI_PLAYWRIGHT_MODULE);
const origin = process.env.NIDHI_PUBLIC_ORIGIN || 'http://localhost:3000';
assert.ok(['localhost', '127.0.0.1'].includes(new URL(origin).hostname));
const browser = await chromium.launch({ executablePath: process.env.NIDHI_CHROME_PATH, headless: true });
const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
const page = await context.newPage();
const posts = [];
const errors = [];
page.on('pageerror', e => errors.push(e.message));
const token = 'private+/=browser-test-token';
const userId = '018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b';
await page.route('**/api/v1/**', async route => {
  const request = route.request();
  assert.ok(!request.url().includes(token) && !request.url().includes(encodeURIComponent(token)));
  const path = new URL(request.url()).pathname;
  if (request.method() === 'POST') posts.push({ path, body: request.postDataJSON() });
  let status = 200, body = { message: 'If this email is awaiting verification, a new verification email has been sent.' };
  if (path.endsWith('/me')) { status = 401; body = { code: 'AUTHENTICATION_REQUIRED' }; }
  if (path.endsWith('/antiforgery')) body = { token: 'mock-csrf' };
  if (path.endsWith('/register')) { status = 409; body = { code: 'EMAIL_VERIFICATION_REQUIRED' }; }
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
});
try {
  await page.goto(origin + '/verify-email');
  await page.getByRole('heading', { name: 'Need a new email?' }).waitFor();
  assert.equal(await page.getByLabel('Account ID', { exact: true }).count(), 0);
  await page.getByLabel('Email address', { exact: true }).fill('pending@example.test');
  await page.getByRole('button', { name: 'Resend verification email' }).click();
  await page.getByRole('button', { name: /Resend available in/ }).waitFor();
  assert.ok(await page.getByRole('button', { name: /Resend available in/ }).isDisabled());
  assert.deepEqual(posts.at(-1).body, { email: 'pending@example.test' });
  await page.goto(origin + '/register');
  await page.getByLabel('Email address', { exact: true }).fill('pending@example.test');
  await page.getByLabel('Password', { exact: true }).fill('LocalTest!12345');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await page.getByRole('link', { name: 'Continue verification / resend email' }).waitFor();
  await page.goto(`${origin}/verify-email#userId=${userId}&token=${encodeURIComponent(token)}`);
  const verify = page.getByRole('button', { name: 'Verify email', exact: true });
  await verify.waitFor();
  assert.equal(new URL(page.url()).hash, '');
  assert.equal(posts.filter(x => x.path.endsWith('/verify-email')).length, 0);
  await verify.click();
  await page.getByRole('heading', { name: 'Email verified', exact: true }).waitFor();
  assert.deepEqual(posts.at(-1).body, { userId, token });
  await page.goto(`${origin}/reset-password#userId=${userId}&token=${encodeURIComponent(token)}`);
  await page.getByLabel('New password', { exact: true }).waitFor();
  await page.waitForFunction(() => !window.location.hash);
  assert.equal(await page.getByLabel('Account ID', { exact: true }).count(), 0);
  await page.getByLabel('New password', { exact: true }).fill('ChangedTest!12345');
  await page.getByLabel('Confirm new password', { exact: true }).fill('ChangedTest!12345');
  await page.getByRole('button', { name: 'Reset password', exact: true }).click();
  await page.getByRole('link', { name: 'Continue to sign in' }).waitFor();
  assert.deepEqual(posts.at(-1).body, { userId, token, newPassword: 'ChangedTest!12345' });
  await page.goto(origin + '/verify-email');
  await page.getByRole('heading', { name: 'Need a new email?' }).waitFor();
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
  await mkdir('/private/tmp/nidhi-verification-review', { recursive: true });
  await page.screenshot({ path: '/private/tmp/nidhi-verification-review/mobile.png', fullPage: true });
  assert.deepEqual(errors, []);
  console.log('Browser checks passed: resend/cooldown, pending signup, explicit link confirmation, token privacy, reset link, mobile layout.');
} finally { await browser.close(); }
