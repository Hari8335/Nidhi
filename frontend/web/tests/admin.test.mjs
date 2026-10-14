import test from 'node:test';
import assert from 'node:assert/strict';
import { adminNavigation, adminPath, exact, parseAdminJson, priceRequest, transactionLabel, unitLabel, validatePrice } from '../src/lib/admin/presentation.ts';
import { createApiClient, ApiError } from '../src/lib/api/client.ts';
import { roleHome } from '../src/lib/auth/routing.ts';
test('admin navigation covers six approved operational routes', () => {
  assert.deepEqual(adminNavigation.map(x => x[1]), ['/admin','/admin/customers','/admin/transactions','/admin/ledger','/admin/prices','/admin/audit-events']);
});
test('price validation accepts exact four places and decimal storage maximum', () => {
  for(const v of ['25000.1234', '99999999999999.9999', '0.0001']) assert.equal(validatePrice(v), undefined);
});
test('price validation rejects zero negative excess scale exponent and overflow', () => {
  for(const v of ['0','0.0000','-1','25000.12345','1.00000','100000000000000','1e4','NaN','']) assert.ok(validatePrice(v));
});
test('publication request preserves decimal precision and contains only price and reason', () => {
  assert.deepEqual(priceRequest(' 99999999999999.9999 ', ' simulation '), {pricePerGramLkr:'99999999999999.9999', reason:'simulation'});
});
test('customer detail mapping preserves exact balances and verified state', () => {
  const c = parseAdminJson('{"id":"a","isEmailVerified":true,"walletBalanceLkr":5000000.00,"goldHoldingGrams":999999999999.99999999}');
  assert.equal(c.goldHoldingGrams,'999999999999.99999999'); assert.equal(c.walletBalanceLkr,'5000000.00'); assert.equal(c.isEmailVerified,true);
});
test('transaction and ledger formatting makes simulated activity and units explicit', () => {
  assert.equal(transactionLabel('WALLET_FUNDING'),'Simulated wallet funding'); assert.equal(transactionLabel('GOLD_PURCHASE'),'Simulated gold purchase');
  assert.equal(unitLabel('LKR'),'LKR'); assert.equal(unitLabel('GOLD_GRAMS'),'GRAMS'); assert.equal(exact('0.00333333',8),'0.00333333'); assert.equal(exact(null,2),'Unavailable');
});
test('price history and publication response retain exact decimals', () => {
  const p = parseAdminJson('{"items":[{"priceVersionId":"a","pricePerGramLkr":99999999999999.9999,"reason":"pricePerGramLkr: 12"}],"totalCount":1}');
  assert.equal(p.items[0].pricePerGramLkr,'99999999999999.9999'); assert.equal(p.totalCount,1); assert.equal(p.items[0].reason,'pricePerGramLkr: 12');
});
test('no-price dashboard is a real null state rather than fallback', () => { assert.equal(parseAdminJson('{"activeGoldPrice":null}').activeGoldPrice,null); });
test('audit evidence maps structured safe metadata and UTC timestamp', () => {
  const a=parseAdminJson('{"details":{"pricePerGramLkr":25000.1234,"source":"operational-command"},"timestampUtc":"2026-10-15T00:00:00Z"}');
  assert.equal(a.details.pricePerGramLkr,'25000.1234'); assert.equal(a.details.source,'operational-command'); assert.ok(a.timestampUtc.endsWith('Z'));
});
test('admin filters are encoded and pagination bounded by caller', () => {
  assert.equal(adminPath('customers',2,{search:'a+b@example.test'}),'/admin/customers?page=2&pageSize=20&search=a%2Bb%40example.test');
  assert.match(adminPath('transactions',1,{type:'GOLD_PURCHASE',customerId:'id'}),/type=GOLD_PURCHASE/);
});
test('publication uses shared CSRF credentials and authoritative response', async () => {
  const calls=[]; const api=createApiClient('/api/v1', async (url,init)=>{calls.push({url,init}); return url.endsWith('antiforgery') ? Response.json({token:'csrf'}) : new Response('{"priceVersionId":"server-id","pricePerGramLkr":25000.1234}',{status:201});});
  const result=await api.post('/admin/prices',priceRequest('25000.1234','simulation'),undefined,parseAdminJson);
  assert.equal(result.priceVersionId,'server-id'); assert.equal(result.pricePerGramLkr,'25000.1234'); assert.equal(calls[1].init.headers['X-CSRF-TOKEN'],'csrf'); assert.equal(calls[1].init.credentials,'include');
});
test('admin Problem Details cannot become empty successful data', async () => {
  const api=createApiClient('/api',async()=>Response.json({code:'FORBIDDEN'},{status:403}));
  await assert.rejects(api.get('/admin/customers',parseAdminJson),e=>e instanceof ApiError && e.status===403);
});
test('role routing keeps customer content out of admin shell', () => {
  assert.equal(roleHome(['ADMIN']),'/admin'); assert.equal(roleHome(['CUSTOMER']),'/dashboard'); assert.equal(roleHome(['ADMIN','CUSTOMER']),null);
});
