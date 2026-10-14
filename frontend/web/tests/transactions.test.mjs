import test from "node:test";
import assert from "node:assert/strict";
import { transactionLabel, transactionsPath, receiptFields } from "../src/lib/transactions/presentation.ts";
import { formatLkr } from "../src/lib/validation/wallet.ts";
import { formatGoldGrams } from "../src/lib/validation/gold.ts";
import { createApiClient, ApiError } from "../src/lib/api/client.ts";
const funding = { transactionId: "019cc111-1111-7111-8111-111111111111", type: "WALLET_FUNDING", status: "COMPLETED", amountLkr: 1250, postOperationWalletBalanceLkr: 1500, createdAtUtc: "2026-10-15T01:02:03.456Z" };
const purchase = { ...funding, type: "GOLD_PURCHASE", priceVersionId: "old-price", appliedPricePerGramLkr: 30000, goldQuantityGrams: 0.00333333, postOperationGoldHoldingGrams: 0.12345678, conversionResidualLkr: 0.0001 };
test("labels exact domain types as simulated customer operations", () => {
  assert.equal(transactionLabel("WALLET_FUNDING"), "Simulated wallet funding");
  assert.equal(transactionLabel("GOLD_PURCHASE"), "Simulated gold purchase");
});
test("money and exact grams use shared formatters including zero", () => {
  assert.equal(formatLkr(1250), "LKR 1,250.00"); assert.equal(formatGoldGrams(0), "0.00000000");
  assert.equal(formatGoldGrams(0.12345678), "0.12345678");
});
test("funding receipt exposes original balance and complete reference and UTC timestamp", () => {
  const fields = Object.fromEntries(receiptFields(funding));
  assert.equal(fields["Wallet balance after operation"], "LKR 1,500.00");
  assert.equal(fields["Transaction reference"], funding.transactionId);
  assert.equal(fields["Completed at (UTC)"], funding.createdAtUtc);
  assert.equal(fields["Applied simulated price"], undefined);
});
test("purchase detail uses original price, version, grams and post-holding snapshot", () => {
  const fields = Object.fromEntries(receiptFields(purchase));
  assert.equal(fields["Applied simulated price"], "LKR 30,000.0000 / g");
  assert.equal(fields["Price version"], "old-price");
  assert.equal(fields["Simulated gold credited"], "0.00333333 g");
  assert.equal(fields["Simulated holding after operation"], "0.12345678 g");
  assert.equal(fields["Conversion rounding residual (LKR)"], "0.000100000000");
});
test("constructs only documented pagination with defaults and maximum", () => {
  assert.equal(transactionsPath(), "/customer/transactions?page=1&pageSize=20");
  assert.equal(transactionsPath(3,100), "/customer/transactions?page=3&pageSize=100");
  for (const [p,s] of [[0,20],[1,101],[1,0],[1.5,20]]) assert.throws(()=>transactionsPath(p,s), RangeError);
});
test("history GET preserves empty pagination DTO and includes credentials without CSRF", async () => {
  const empty = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };
  const client = createApiClient("/api/v1", async (url, init) => {
    assert.equal(url, "/api/v1/customer/transactions?page=1&pageSize=20");
    assert.equal(init.credentials, "include"); assert.equal(init.headers, undefined); return Response.json(empty);
  });
  assert.deepEqual(await client.get(transactionsPath()), empty);
});
test("detail GET maps receipt without substituting any current-price value", async () => {
  const client = createApiClient("/api/v1", async () => Response.json(purchase));
  assert.deepEqual(receiptFields(await client.get("/customer/transactions/"+purchase.transactionId)), receiptFields(purchase));
});
test("not-found and authorization errors retain safe Problem Details messages", async () => {
  for (const [status,code,detail] of [[404,"RESOURCE_NOT_FOUND","Transaction not found."],[403,"EMAIL_NOT_VERIFIED","Verify email."]]) {
    const client = createApiClient("/api/v1", async () => Response.json({code,detail},{status}));
    await assert.rejects(client.get("/customer/transactions/missing"), e => e instanceof ApiError && e.status === status && (status !== 404 || e.message === detail));
  }
});
