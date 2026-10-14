export type TransactionType = "WALLET_FUNDING" | "GOLD_PURCHASE";
export type TransactionSummary = {
  transactionId: string; type: TransactionType; status: "COMPLETED";
  amountLkr: number; goldQuantityGrams: number | null;
  appliedPricePerGramLkr: number | null; postOperationWalletBalanceLkr: number;
  createdAtUtc: string;
};
export type TransactionPage = {
  items: TransactionSummary[]; page: number; pageSize: number; totalCount: number; totalPages: number;
};
export type TransactionReceipt = {
  transactionId: string; type: "WALLET_FUNDING"; status: "COMPLETED";
  amountLkr: number; postOperationWalletBalanceLkr: number; createdAtUtc: string;
} | {
  transactionId: string; type: "GOLD_PURCHASE"; status: "COMPLETED";
  amountLkr: number; postOperationWalletBalanceLkr: number; createdAtUtc: string;
  priceVersionId: string; appliedPricePerGramLkr: number; goldQuantityGrams: number;
  conversionResidualLkr: number; postOperationGoldHoldingGrams: number;
};
export type Holdings = {
  quantityGrams: number; indicativePortfolioValueLkr: number | null;
  activePricePerGramLkr: number | null; priceVersionId: string | null; isPriceFresh: boolean;
};
