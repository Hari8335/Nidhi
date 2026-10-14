import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createApiClient, ApiError, parseProblem } from '../src/lib/api/client.ts';
import { roleHome } from '../src/lib/auth/routing.ts';
import { passwordError, confirmationError } from '../src/lib/validation/auth.ts';

const json = (value, status = 200, headers = {}) => new Response(JSON.stringify(value), { status, headers });
const csrfFailure = () => json({ code: 'CSRF_VALIDATION_FAILED' }, 400);

test('Problem Details retains typed fields and rejects malformed validation messages', () => {
  assert.deepEqual(parseProblem(null), {});
  assert.deepEqual(parseProblem({ title: 'Invalid', status: 400, detail: 'Check fields', code: 'VALIDATION_ERROR', traceId: 'support-id', errors: { email: ['Invalid email'], token: 'invalid' } }), { title: 'Invalid', status: 400, detail: 'Check fields', code: 'VALIDATION_ERROR', traceId: 'support-id', errors: { email: ['Invalid email'] } });
});
test('unexpected errors never display server internals', () => {
  const error = new ApiError(500, { title: 'Database exception', detail: 'private SQL', code: 'INTERNAL_ERROR' });
  assert.doesNotMatch(error.message, /SQL|Database|exception/);
  assert.match(new ApiError(429, { code: 'RATE_LIMITED' }, '42').message, /42 seconds/);
});
test('role routing rejects unknown or ambiguous roles and does not grant CUSTOMER to ADMIN', () => {
  assert.equal(roleHome(['CUSTOMER']), '/dashboard');
  assert.equal(roleHome(['ADMIN']), '/admin');
  for (const roles of [[], ['owner'], ['CUSTOMER', 'ADMIN']]) assert.equal(roleHome(roles), null);
});
test('password validation matches approved length and character requirements', () => {
  const valid = 'Aa1!' + 'x'.repeat(8);
  assert.equal(passwordError(valid), undefined);
  for (const password of ['short', 'a'.repeat(12), 'Aa1!' + 'x'.repeat(125), 'A1!'.repeat(4)]) assert.ok(passwordError(password));
  assert.equal(confirmationError(valid, valid), undefined);
  assert.ok(confirmationError(valid, valid + 'x'));
});
test('all POSTs obtain and reuse memory-only CSRF and include cookies', async () => {
  const calls = [];
  const client = createApiClient('/api/v1', async (url, init) => {
    calls.push({ url, init });
    return url.endsWith('antiforgery') ? json({ token: 'test-csrf' }) : json({ message: 'ok' });
  });
  await client.post('/auth/register', { email: 'example@example.test' });
  await client.post('/auth/forgot-password', {});
  assert.equal(calls.filter(c => c.url.endsWith('antiforgery')).length, 1);
  for (const c of calls) { assert.equal(c.init.credentials, 'include'); assert.equal(c.init.cache, 'no-store'); }
  assert.equal(calls[1].init.headers['X-CSRF-TOKEN'], 'test-csrf');
});
test('concurrent requests share an antiforgery fetch', async () => {
  let tokens = 0;
  const client = createApiClient('', async url => { if (url.endsWith('antiforgery')) { tokens++; return json({ token: 'test' }); } return json({}); });
  await Promise.all([client.post('/a'), client.post('/b')]);
  assert.equal(tokens, 1);
});
for (const transition of ['login', 'logout', 'reset-password']) {
  test(`successful ${transition} invalidates the CSRF token before the next POST`, async () => {
    let tokens = 0;
    const client = createApiClient('', async url => {
      if (url.endsWith('antiforgery')) return json({ token: `test-${++tokens}` });
      return transition === 'logout' && url.endsWith('logout') ? new Response(null, { status: 204 }) : json({});
    });
    await client.post(`/auth/${transition}`);
    await client.post('/another');
    assert.equal(tokens, 2);
  });
}
test('explicit CSRF middleware rejection refetches and retries exactly once', async () => {
  let tokens = 0, posts = 0;
  const client = createApiClient('', async url => {
    if (url.endsWith('antiforgery')) return json({ token: `test-${++tokens}` });
    return ++posts === 1 ? csrfFailure() : json({});
  });
  await client.post('/auth/register');
  assert.equal(tokens, 2); assert.equal(posts, 2);
});
test('repeated CSRF rejection stops after one retry', async () => {
  let posts = 0;
  const client = createApiClient('', async url => { if (url.endsWith('antiforgery')) return json({ token: 'test' }); posts++; return csrfFailure(); });
  await assert.rejects(client.post('/auth/register'), ApiError);
  assert.equal(posts, 2);
});
test('network failure and server error never replay potentially completed mutations', async () => {
  for (const network of [true, false]) {
    let posts = 0;
    const client = createApiClient('', async url => {
      if (url.endsWith('antiforgery')) return json({ token: 'test' });
      posts++;
      if (network) throw new TypeError('network');
      return json({ code: 'INTERNAL_ERROR' }, 500);
    });
    await assert.rejects(client.post('/auth/register'), ApiError);
    assert.equal(posts, 1);
  }
});
test('401 expires CSRF; the next request obtains fresh anonymous evidence', async () => {
  let tokens = 0;
  const client = createApiClient('', async url => {
    if (url.endsWith('antiforgery')) return json({ token: `test-${++tokens}` });
    if (url.endsWith('/me')) return json({ code: 'AUTHENTICATION_REQUIRED' }, 401);
    return json({});
  });
  await client.post('/first');
  await assert.rejects(client.get('/auth/me'), ApiError);
  await client.post('/next');
  assert.equal(tokens, 2);
});
test('failed antiforgery fetch can be retried on a later user action', async () => {
  let tokens = 0;
  const client = createApiClient('', async url => {
    if (url.endsWith('antiforgery')) return ++tokens === 1 ? json({}, 503) : json({ token: 'test' });
    return json({});
  });
  await assert.rejects(client.post('/first'), ApiError);
  await client.post('/second');
  assert.equal(tokens, 2);
});
test('a stale anonymous 401 cannot expire a newer login session', async () => {
  const original = globalThis.window;
  const events = [];
  globalThis.window = { dispatchEvent: event => events.push(event.type) };
  try {
    let finishMe;
    const client = createApiClient('', async url => {
      if (url.endsWith('/me')) return new Promise(resolve => { finishMe = resolve; });
      if (url.endsWith('antiforgery')) return json({ token: 'test' });
      return json({ role: 'CUSTOMER' });
    });
    const pendingMe = client.get('/auth/me');
    await client.post('/auth/login');
    finishMe(json({ code: 'AUTHENTICATION_REQUIRED' }, 401));
    await assert.rejects(pendingMe, ApiError);
    assert.deepEqual(events, []);
  } finally { if (original === undefined) delete globalThis.window; else globalThis.window = original; }
});
test('current-session 401 announces expiry but invalid login does not', async () => {
  const original = globalThis.window;
  const events = [];
  globalThis.window = { dispatchEvent: event => events.push(event.type) };
  try {
    const client = createApiClient('', async url => url.endsWith('/me') ? json({ code: 'AUTHENTICATION_REQUIRED' }, 401) : json({ code: 'INVALID_CREDENTIALS' }, 401));
    await assert.rejects(client.get('/auth/me'), ApiError);
    await assert.rejects(client.get('/invalid-login'), ApiError);
    assert.deepEqual(events, ['nidhi:session-expired']);
  } finally { if (original === undefined) delete globalThis.window; else globalThis.window = original; }
});
