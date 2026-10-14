import { formatLkr as lkr } from "../validation/wallet.ts";
import { formatGoldGrams } from "../validation/gold.ts";
import type { TransactionReceipt, TransactionType } from "../../types/transactions.ts";

export function transactionLabel(type: TransactionType) {
  return type === "WALLET_FUNDING" ? "Simulated wallet funding" : "Simulated gold purchase";
}
export function transactionsPath(page = 1, pageSize = 20) {
  if (!Number.isSafeInteger(page) || page < 1 || !Number.isInteger(pageSize) || pageSize < 1 || pageSize > 100)
    throw new RangeError("Invalid transaction page.");
  return `/customer/transactions?page=${page}&pageSize=${pageSize}`;
}
export function receiptFields(receipt: TransactionReceipt) {
  const fields = [
    ["Transaction reference", receipt.transactionId],
    ["Operation", transactionLabel(receipt.type)],
    ["Status", "Completed"],
    ["Amount", lkr(receipt.amountLkr)],
    ["Completed at (UTC)", receipt.createdAtUtc],
    ["Wallet balance after operation", lkr(receipt.postOperationWalletBalanceLkr)],
  ];
  if (receipt.type === "GOLD_PURCHASE") fields.push(
    ["Applied simulated price", `LKR ${receipt.appliedPricePerGramLkr.toLocaleString("en-US", { minimumFractionDigits: 4, maximumFractionDigits: 4 })} / g`],
    ["Price version", receipt.priceVersionId],
    ["Simulated gold credited", `${formatGoldGrams(receipt.goldQuantityGrams)} g`],
    ["Simulated holding after operation", `${formatGoldGrams(receipt.postOperationGoldHoldingGrams)} g`],
    ["Conversion rounding residual (LKR)", receipt.conversionResidualLkr.toFixed(12)],
  );
  return fields;
}
