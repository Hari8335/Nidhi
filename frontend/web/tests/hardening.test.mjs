import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { signInDestination } from '../src/lib/auth/routing.ts';
import { formatDateTime } from '../src/lib/format/date.ts';
const source = path => readFile(new URL('../src/' + path, import.meta.url), 'utf8');
test('expired sessions resume only known routes within the authenticated role', () => {
  assert.equal(signInDestination(['CUSTOMER'], '/gold'), '/gold');
  assert.equal(signInDestination(['ADMIN'], '/admin/prices'), '/admin/prices');
  for (const path of ['//evil.example', 'https://evil.example', '/admin', '/gold?token=secret', '/gold/../../admin']) assert.equal(signInDestination(['CUSTOMER'], path), '/dashboard');
  assert.equal(signInDestination(['ADMIN'], '/wallet'), '/admin');
  assert.equal(signInDestination(['CUSTOMER', 'ADMIN'], '/gold'), null);
});
test('timestamps are human readable and explicitly UTC at a calendar boundary', () => {
  assert.equal(formatDateTime('2026-10-15T23:59:00Z'), '15 Oct 2026, 11:59 pm UTC');
  assert.equal(formatDateTime('2026-10-16T00:01:00Z'), '16 Oct 2026, 12:01 am UTC');
});
test('customer activity prioritizes labels and receipts without exposing full UUID text', async () => {
  const list = await source('components/transactions/transaction-list.tsx');
  assert.match(list, /transactionLabel\(tx.type\)/); assert.match(list, /View receipt/);
  assert.doesNotMatch(list, />\{tx.transactionId\}</); assert.match(list, /formatDateTime/);
});
test('normal gold UI omits price version UUIDs and engineering copy while receipts retain them', async () => {
  const gold = await source('components/gold/gold-page.tsx');
  const holding = await source('components/gold/holdings-summary.tsx');
  assert.doesNotMatch(gold + holding, /\{(?:price|receipt|data.holding).priceVersionId\}|backend receipt|Current price version/);
  assert.match(gold, /No purchase was made/); assert.match(gold, /Previous price/); assert.match(gold, /Current price/);
  assert.match(gold, /role="alert"/); assert.match(gold, /changeHeading.current\?\.focus/);
  assert.match(gold, /FormField id="gold-amount"/); assert.match(gold, /Input ref=\{amountField\}/);
});
