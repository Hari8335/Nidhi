import test from "node:test";
import assert from "node:assert/strict";
import { validateFundingInput, formatLkr } from "../src/lib/validation/wallet.ts";

test("validates minimum funding boundary (LKR 100.00)", () => {
  assert.equal(validateFundingInput("100.00", 0, 20).isValid, true);
  assert.equal(validateFundingInput("100", 0, 20).isValid, true);
  assert.equal(validateFundingInput("99.99", 0, 20).isValid, false);
  assert.equal(validateFundingInput("99.99", 0, 20).error, "BELOW_MINIMUM");
});

test("validates maximum funding boundary (LKR 1,000,000.00)", () => {
  assert.equal(validateFundingInput("1000000.00", 0, 20).isValid, true);
  assert.equal(validateFundingInput("1000000", 0, 20).isValid, true);
  assert.equal(validateFundingInput("1000000.01", 0, 20).isValid, false);
  assert.equal(validateFundingInput("1000000.01", 0, 20).error, "ABOVE_MAXIMUM");
});

test("rejects numbers with more than 2 decimal places", () => {
  assert.equal(validateFundingInput("100.555", 0, 20).isValid, false);
  assert.equal(validateFundingInput("100.555", 0, 20).error, "INVALID_FORMAT");
});

test("enforces wallet balance cap (LKR 5,000,000.00)", () => {
  // Current balance 4,999,900.00 + 100.00 = 5,000,000.00 (allowed)
  assert.equal(validateFundingInput("100.00", 4_999_900.00, 20).isValid, true);

  // Current balance 4,999,900.00 + 100.01 = 5,000,000.01 (rejected)
  const res = validateFundingInput("100.01", 4_999_900.00, 20);
  assert.equal(res.isValid, false);
  assert.equal(res.error, "EXCEEDS_WALLET_CAP");
});

test("enforces daily operations limit", () => {
  const res = validateFundingInput("100.00", 0, 0);
  assert.equal(res.isValid, false);
  assert.equal(res.error, "DAILY_LIMIT_REACHED");
});

test("formats currency nicely", () => {
  assert.equal(formatLkr(1234.5), "LKR 1,234.50");
  assert.equal(formatLkr(5000000), "LKR 5,000,000.00");
});
