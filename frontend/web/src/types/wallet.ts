// Wallet and financial types mirroring the API contract (API_CONTRACT.md §4.4–4.5)

export type WalletSummary = {
  balanceLkr: number;
  maxBalanceCapLkr: number;
  minFundingAmountLkr: number;
  maxFundingAmountLkr: number;
  successfulFundingCountToday: number;
  maxDailyFundingOperations: number;
  remainingFundingOperationsToday: number;
};

export type FundingReceipt = {
  transactionId: string;
  type: string;
  status: string;
  amountLkr: number;
  postOperationWalletBalanceLkr: number;
  createdAtUtc: string;
};

import type { TransactionSummary } from "./transactions";
export type { TransactionSummary } from "./transactions";

export type DashboardData = {
  walletBalanceLkr: number;
  goldHoldingGrams: number;
  recentTransactions: TransactionSummary[];
  activeGoal: import("../lib/goals/presentation").SavingsGoal | null;
};
