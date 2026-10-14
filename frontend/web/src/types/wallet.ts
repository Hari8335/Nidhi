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

export type TransactionSummary = {
  transactionId: string;
  type: string;
  amountLkr: number;
  goldQuantityGrams: number | null;
  postOperationWalletBalanceLkr: number;
  createdAtUtc: string;
};

export type DashboardData = {
  walletBalanceLkr: number;
  goldHoldingGrams: number;
  recentTransactions: TransactionSummary[];
};
