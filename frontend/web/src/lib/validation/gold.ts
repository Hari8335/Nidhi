export function truncateGoldPreview(amountLkr: number, pricePerGramLkr: number): number {
  if (!Number.isFinite(amountLkr) || !Number.isFinite(pricePerGramLkr) || pricePerGramLkr <= 0) return 0;
  return Math.trunc((amountLkr / pricePerGramLkr) * 1e8) / 1e8;
}
export function validatePurchaseAmount(value: string, walletBalance: number) {
  if (!/^\d+(\.\d{1,2})?$/.test(value)) return { isValid: false, error: "INVALID_FORMAT" };
  const amount = Number(value);
  if (amount < 100) return { isValid: false, error: "BELOW_MINIMUM" };
  if (amount > 1_000_000) return { isValid: false, error: "ABOVE_MAXIMUM" };
  if (amount > walletBalance) return { isValid: false, error: "INSUFFICIENT_FUNDS" };
  return { isValid: true as const };
}
export function formatGoldGrams(value: number) { return value.toFixed(8); }

export function formatGoldPrice(value: number) {
  return `LKR ${value.toLocaleString("en-US", { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`;
}
