// Wallet and simulated funding client validation rules

export const WALLET_LIMITS = {
  MIN_FUNDING_LKR: 100.00,
  MAX_FUNDING_LKR: 1_000_000.00,
  MAX_BALANCE_CAP_LKR: 5_000_000.00,
  MAX_DAILY_OPERATIONS: 20,
} as const;

export type FundingValidationError =
  | "REQUIRED"
  | "INVALID_FORMAT"
  | "BELOW_MINIMUM"
  | "ABOVE_MAXIMUM"
  | "EXCEEDS_WALLET_CAP"
  | "DAILY_LIMIT_REACHED";

export function validateFundingInput(
  rawAmount: string | number,
  currentBalance = 0,
  remainingDailyOperations: number = WALLET_LIMITS.MAX_DAILY_OPERATIONS
): { isValid: boolean; amount?: number; error?: FundingValidationError; message?: string } {
  if (remainingDailyOperations <= 0) {
    return {
      isValid: false,
      error: "DAILY_LIMIT_REACHED",
      message: `You have reached the limit of ${WALLET_LIMITS.MAX_DAILY_OPERATIONS} funding operations for today. Please try again tomorrow.`,
    };
  }

  const str = typeof rawAmount === "number" ? rawAmount.toString() : rawAmount.trim();
  if (!str) {
    return { isValid: false, error: "REQUIRED", message: "Please enter an amount to add." };
  }

  // Check numeric format with at most 2 decimal places
  if (!/^\d+(\.\d{1,2})?$/.test(str)) {
    return {
      isValid: false,
      error: "INVALID_FORMAT",
      message: "Please enter a valid amount with at most 2 decimal places.",
    };
  }

  const amount = Number(str);
  if (Number.isNaN(amount) || !Number.isFinite(amount)) {
    return { isValid: false, error: "INVALID_FORMAT", message: "Please enter a valid number." };
  }

  if (amount < WALLET_LIMITS.MIN_FUNDING_LKR) {
    return {
      isValid: false,
      error: "BELOW_MINIMUM",
      message: `Minimum funding amount is LKR ${WALLET_LIMITS.MIN_FUNDING_LKR.toFixed(2)}.`,
    };
  }

  if (amount > WALLET_LIMITS.MAX_FUNDING_LKR) {
    return {
      isValid: false,
      error: "ABOVE_MAXIMUM",
      message: `Maximum funding amount is LKR ${WALLET_LIMITS.MAX_FUNDING_LKR.toLocaleString("en-US", { minimumFractionDigits: 2 })}.`,
    };
  }

  if (currentBalance + amount > WALLET_LIMITS.MAX_BALANCE_CAP_LKR) {
    const maxAddable = Math.max(0, WALLET_LIMITS.MAX_BALANCE_CAP_LKR - currentBalance);
    return {
      isValid: false,
      error: "EXCEEDS_WALLET_CAP",
      message: `This amount would exceed the maximum wallet balance cap of LKR ${WALLET_LIMITS.MAX_BALANCE_CAP_LKR.toLocaleString("en-US", { minimumFractionDigits: 2 })}. You can add up to LKR ${maxAddable.toLocaleString("en-US", { minimumFractionDigits: 2 })}.`,
    };
  }

  return { isValid: true, amount };
}

export function formatLkr(amount: number): string {
  return `LKR ${amount.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}
