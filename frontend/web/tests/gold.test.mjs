import test from "node:test";
import assert from "node:assert/strict";
import { formatGoldGrams, truncateGoldPreview, validatePurchaseAmount } from "../src/lib/validation/gold.ts";
test("validates purchase amount boundaries and wallet balance", () => {
  assert.equal(validatePurchaseAmount("99.99", 1000).error, "BELOW_MINIMUM");
  assert.equal(validatePurchaseAmount("100.00", 1000).isValid, true);
  assert.equal(validatePurchaseAmount("1000000.01", 2000000).error, "ABOVE_MAXIMUM");
  assert.equal(validatePurchaseAmount("100.001", 1000).error, "INVALID_FORMAT");
  assert.equal(validatePurchaseAmount("800", 500).error, "INSUFFICIENT_FUNDS");
});
test("previews by truncating to eight decimals and formats exact precision", () => {
  assert.equal(truncateGoldPreview(12345.67899, 10000), 1.23456789);
  assert.equal(truncateGoldPreview(100, 30000), 0.00333333);
  assert.equal(formatGoldGrams(1.2), "1.20000000");
});
import { createPurchaseFlow } from "../src/lib/gold/purchase-flow.ts";
import { createApiClient } from "../src/lib/api/client.ts";
const priceA = { priceVersionId: "price-a", pricePerGramLkr: 30000, publishedAtUtc: "2026-10-14T00:00:00Z", isFresh: true, isPurchasingEnabled: true };
const priceB = { ...priceA, priceVersionId: "price-b", pricePerGramLkr: 40000 };
const authoritativeReceipt = { transactionId: "transaction-server", type: "GOLD_PURCHASE", status: "COMPLETED", amountLkr: 100, priceVersionId: "price-b", appliedPricePerGramLkr: 40000, goldQuantityGrams: 0.0025, conversionResidualLkr: 0, postOperationWalletBalanceLkr: 900, postOperationGoldHoldingGrams: 0.12345678, createdAtUtc: "2026-10-14T01:02:03Z" };
function harness() {
  let current = priceA, failure = null, outcome = authoritativeReceipt, keys = 0;
  const calls = [];
  const client = createApiClient("/api/v1", async (url, init) => {
    if (url.endsWith("/auth/antiforgery")) return Response.json({ token: "test-csrf" });
    if (url.endsWith("/customer/dashboard")) return Response.json({ walletBalanceLkr: 1000, goldHoldingGrams: 0.12 });
    if (url.endsWith("/customer/gold-price")) {
      if (current === null) return Response.json({ code: "PRICE_UNAVAILABLE" }, { status: 503 });
      return Response.json(current);
    }
    assert.ok(url.endsWith("/customer/gold-purchases"));
    calls.push({ body: JSON.parse(init.body), key: init.headers["Idempotency-Key"] });
    if (failure === "transport") throw new TypeError("connection lost");
    if (failure) return Response.json({ code: failure }, { status: failure === "PRICE_CHANGED" ? 409 : 503 });
    return Response.json(outcome, { status: 201 });
  });
  const flow = createPurchaseFlow(client, () => `intent-${++keys}`);
  return { flow, calls, price(value) { current = value; }, fail(value) { failure = value; }, receipt(value) { outcome = value; } };
}
test("PRICE_CHANGED abandons A, displays B and new estimate, and requires explicit review and confirmation with a new key", async () => {
  const h = harness(); await h.flow.refresh();
  assert.equal(h.flow.getSnapshot().price.priceVersionId, "price-a");
  h.flow.review(100); h.fail("PRICE_CHANGED"); h.price(priceB);
  await h.flow.submit();
  assert.deepEqual(h.calls, [{ body: { amountLkr: 100, priceVersionId: "price-a" }, key: "intent-1" }]);
  const state = h.flow.getSnapshot();
  assert.equal(state.price.priceVersionId, "price-b");
  assert.equal(formatGoldGrams(truncateGoldPreview(100, state.price.pricePerGramLkr)), "0.00250000");
  assert.match(state.error, /explicitly confirm again/);
  assert.equal(state.reconfirm, true); assert.equal(state.reviewing, false); assert.equal(state.receipt, null);
  h.fail(null); await h.flow.submit(); assert.equal(h.calls.length, 1);
  h.flow.review(100); assert.equal(h.calls.length, 1);
  await h.flow.submit();
  assert.deepEqual(h.calls[1], { body: { amountLkr: 100, priceVersionId: "price-b" }, key: "intent-2" });
  assert.equal(h.flow.getSnapshot().receipt.transactionId, "transaction-server");
});
test("same-intent transport retry preserves amount, version and idempotency key with no automatic retry", async () => {
  const h = harness(); await h.flow.refresh(); h.flow.review(100); h.fail("transport"); await h.flow.submit();
  assert.equal(h.calls.length, 1); assert.equal(h.flow.getSnapshot().reviewing, true);
  h.fail(null); await h.flow.submit(); assert.equal(h.calls.length, 2); assert.deepEqual(h.calls[0], h.calls[1]);
});
test("PRICE_UNAVAILABLE load clears price and blocks purchase with a refresh recovery path", async () => {
  const h = harness(); await h.flow.refresh(); h.price(null); await h.flow.refresh();
  assert.equal(h.flow.getSnapshot().price, null); assert.match(h.flow.getSnapshot().error, /unavailable.*Refresh/);
  h.flow.review(100); await h.flow.submit(); assert.equal(h.calls.length, 0);
  h.price(priceB); await h.flow.refresh(); assert.equal(h.flow.getSnapshot().price.priceVersionId, "price-b");
});
test("PRICE_UNAVAILABLE purchase response removes displayed price and blocks fallback purchases", async () => {
  const h = harness(); await h.flow.refresh(); h.flow.review(100); h.fail("PRICE_UNAVAILABLE"); await h.flow.submit();
  assert.equal(h.flow.getSnapshot().price, null); h.flow.review(100); await h.flow.submit(); assert.equal(h.calls.length, 1);
});
test("stale persisted price explains blocked state and cannot be purchased", async () => {
  const h = harness(); h.price({ ...priceA, isFresh: false, isPurchasingEnabled: false }); await h.flow.refresh();
  assert.match(h.flow.getSnapshot().error, /stale.*blocked/); assert.equal(h.flow.getSnapshot().price.priceVersionId, "price-a");
  h.flow.review(100); await h.flow.submit(); assert.equal(h.calls.length, 0);
});
test("PRICE_STALE purchase rejection blocks further submissions without a fallback", async () => {
  const h = harness(); await h.flow.refresh(); h.flow.review(100); h.fail("PRICE_STALE"); await h.flow.submit();
  assert.equal(h.flow.getSnapshot().price.isFresh, false); assert.equal(h.flow.getSnapshot().price.isPurchasingEnabled, false);
  assert.match(h.flow.getSnapshot().error, /stale/); h.flow.review(100); await h.flow.submit(); assert.equal(h.calls.length, 1);
});
test("success uses every authoritative receipt field and balances rather than preview state", async () => {
  const h = harness(); await h.flow.refresh(); h.flow.review(100);
  const receipt = { ...authoritativeReceipt, priceVersionId: "price-a", appliedPricePerGramLkr: 30000, goldQuantityGrams: 0.00333333 };
  h.receipt(receipt); await h.flow.submit();
  assert.deepEqual(h.flow.getSnapshot().receipt, receipt);
  assert.deepEqual(h.flow.getSnapshot().balances, { walletBalanceLkr: receipt.postOperationWalletBalanceLkr, goldHoldingGrams: receipt.postOperationGoldHoldingGrams });
  assert.equal(h.flow.getSnapshot().reviewing, false);
});
test("exact eight-decimal gold display retains receipt and holding precision", () => {
  assert.equal(formatGoldGrams(0.12345678), "0.12345678");
  assert.equal(formatGoldGrams(0.00333333), "0.00333333");
});
test("changed amount abandons prior intent and requires a new key", async () => {
  const h = harness(); await h.flow.refresh(); h.flow.review(100); h.fail("transport"); await h.flow.submit();
  h.flow.edit(); h.flow.review(200); h.fail(null); await h.flow.submit();
  assert.equal(h.calls[1].key, "intent-2"); assert.equal(h.calls[1].body.amountLkr, 200);
});
test("manual price refresh invalidates reviewed old version without automatic submission", async () => {
  const h = harness(); await h.flow.refresh(); h.flow.review(100); h.price(priceB); await h.flow.refresh();
  assert.equal(h.flow.getSnapshot().reviewing, false); assert.equal(h.flow.getSnapshot().reconfirm, true);
  await h.flow.submit(); assert.equal(h.calls.length, 0);
});
