export type PageResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };
export type Customer = { id: string; email: string | null; displayName: string | null; isEmailVerified: boolean; createdAtUtc: string; walletBalanceLkr?: string | null; goldHoldingGrams?: string | null };
export type Price = { priceVersionId: string; pricePerGramLkr: string; publishedAtUtc: string; publishedByAdminId?: string; reason?: string; isFresh?: boolean };
export type Dashboard = { totalCustomersCount: number; totalCompletedTransactionsCount: number; activeGoldPrice: Price | null };
export type Transaction = { transactionId: string; customerId: string; type: "WALLET_FUNDING" | "GOLD_PURCHASE"; status: string; amountLkr: string; priceVersionId: string | null; appliedPricePerGramLkr: string | null; goldQuantityGrams: string | null; conversionResidualLkr: string | null; postOperationWalletBalanceLkr: string; postOperationGoldHoldingGrams: string | null; createdAtUtc: string };
export type Posting = { entryId: string; accountNumber: string; accountName: string; unit: string; direction: string; amount: string; createdAtUtc: string };
export type Ledger = { transactionId: string; lkrBook: { isBalanced: boolean; entries: Posting[] }; goldBook: { isBalanced: boolean; entries: Posting[] } };
export type Audit = { id: string; actorId: string; actorRole: string; action: string; entityType: string; entityId: string; details: Record<string, unknown>; timestampUtc: string };
const decimals = ["pricePerGramLkr", "walletBalanceLkr", "goldHoldingGrams", "amountLkr", "appliedPricePerGramLkr", "goldQuantityGrams", "conversionResidualLkr", "postOperationWalletBalanceLkr", "postOperationGoldHoldingGrams", "amount"];
export function parseAdminJson<T>(text: string): T {
  return JSON.parse(text.replace(/("(?:\\.|[^"\\])*")(\s*:\s*)(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|"(?:\\.|[^"\\])*"/g,
    (token, key, separator, value) => key && decimals.includes(JSON.parse(key)) ? `${key}${separator}${JSON.stringify(value)}` : token)) as T;
}
export function validatePrice(value: string): string | undefined {
  if (!/^\d{1,14}(\.\d{1,4})?$/.test(value.trim())) return "Enter LKR per gram with up to 14 integer and 4 decimal places.";
  if (!/[1-9]/.test(value)) return "Enter a price greater than zero.";
}
export function priceRequest(value: string, reason: string) { return { pricePerGramLkr: value.trim(), reason: reason.trim() }; }
export function adminPath(resource: string, page: number, filters: Record<string, string> = {}) {
  const query = new URLSearchParams({ page: String(page), pageSize: "20" });
  for (const [key, value] of Object.entries(filters)) if (value.trim()) query.set(key, value.trim());
  return `/admin/${resource}?${query}`;
}
export function transactionLabel(type: Transaction["type"]) { return type === "GOLD_PURCHASE" ? "Simulated gold purchase" : "Simulated wallet funding"; }
export function unitLabel(unit: string) { return unit === "LKR" ? "LKR" : "GRAMS"; }
export function exact(value: string | null | undefined, places: number) {
  if (value == null) return "Unavailable";
  const [whole, fraction = ""] = value.split(".");
  return `${whole.replace(/\B(?=(\d{3})+(?!\d))/g, ",")}.${fraction.replace(/0+$/, "").padEnd(places, "0")}`;
}
export const adminNavigation = [["Dashboard", "/admin"], ["Customers", "/admin/customers"], ["Transactions", "/admin/transactions"], ["Ledger", "/admin/ledger"], ["Gold Prices", "/admin/prices"], ["Audit Logs", "/admin/audit-events"]] as const;
