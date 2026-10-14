import type { GoldPrice, GoldPurchaseReceipt } from "@/types/gold";

type Dashboard = { walletBalanceLkr: number; goldHoldingGrams: number };
type Client = { get<T>(path: string): Promise<T>; post<T>(path: string, body: unknown, headers: Record<string, string>): Promise<T> };
type Intent = { amountLkr: number; priceVersionId: string; key: string };
type State = { price: GoldPrice | null; balances: Dashboard | null; receipt: GoldPurchaseReceipt | null; error: string; busy: boolean; reviewing: boolean; reconfirm: boolean };
const priceMessages: Record<string, string> = {
  PRICE_UNAVAILABLE: "The simulated gold price is unavailable. Refresh to try again; purchases are disabled.",
  PRICE_STALE: "The simulated gold price is stale. Purchases are blocked until a fresh price is published.",
  PRICE_CHANGED: "Price updated. Review the new price and estimated quantity, then explicitly confirm again.",
};
function codeOf(error: unknown) { return (error as { problem?: { code?: string } } | null)?.problem?.code; }

// The page and Node behavior tests use this same purchase-intent lifecycle.
export function createPurchaseFlow(client: Client, newKey: () => string = () => crypto.randomUUID()) {
  let state: State = { price: null, balances: null, receipt: null, error: "", busy: false, reviewing: false, reconfirm: false };
  let intent: Intent | null = null;
  const listeners = new Set<() => void>();
  const update = (change: Partial<State>) => { state = { ...state, ...change }; listeners.forEach(listener => listener()); };
  async function refresh(preserveWarning = false) {
    try {
      const [price, balances] = await Promise.all([client.get<GoldPrice>("/customer/gold-price"), client.get<Dashboard>("/customer/dashboard")]);
      const changed = intent !== null && intent.priceVersionId !== price.priceVersionId;
      if (changed) intent = null;
      update({ price, balances, ...(changed ? { reviewing: false, reconfirm: true } : {}),
        error: !price.isFresh || !price.isPurchasingEnabled ? priceMessages.PRICE_STALE : preserveWarning || changed ? priceMessages.PRICE_CHANGED : "" });
    } catch (error) {
      intent = null;
      update({ price: null, reviewing: false, error: priceMessages[codeOf(error) ?? ""] ?? "Unable to load current price and balances. Refresh to try again." });
    }
  }
  return {
    subscribe(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener); }; },
    getSnapshot: () => state,
    refresh,
    edit() { if (!state.busy) { intent = null; update({ reviewing: false, receipt: null }); } },
    cancel() { if (!state.busy) { intent = null; update({ reviewing: false }); } },
    review(amount: number) {
      if (state.busy || !state.price?.isFresh || !state.price.isPurchasingEnabled || !state.balances || !Number.isFinite(amount) || amount < 100 || amount > 1000000 || amount > state.balances.walletBalanceLkr || Math.round(amount * 100) / 100 !== amount) return;
      if (!intent || intent.amountLkr !== amount || intent.priceVersionId !== state.price.priceVersionId)
        intent = { amountLkr: amount, priceVersionId: state.price.priceVersionId, key: newKey() };
      update({ reviewing: true });
    },
    async submit() {
      if (state.busy || !state.reviewing || !intent || !state.price?.isFresh || !state.price.isPurchasingEnabled) return;
      const submitted = intent;
      update({ busy: true, error: "" });
      try {
        const receipt = await client.post<GoldPurchaseReceipt>("/customer/gold-purchases", { amountLkr: submitted.amountLkr, priceVersionId: submitted.priceVersionId }, { "Idempotency-Key": submitted.key });
        intent = null;
        // Final values are authoritative receipt snapshots, never the preview calculation.
        update({ receipt, reviewing: false, reconfirm: false, balances: { walletBalanceLkr: receipt.postOperationWalletBalanceLkr, goldHoldingGrams: receipt.postOperationGoldHoldingGrams } });
      } catch (error) {
        const code = codeOf(error);
        if (code === "PRICE_CHANGED") {
          intent = null;
          update({ price: null, reviewing: false, reconfirm: true, error: priceMessages.PRICE_CHANGED });
          await refresh(true);
        } else if (code === "PRICE_UNAVAILABLE" || code === "PRICE_STALE") {
          intent = null;
          update({ reviewing: false, price: code === "PRICE_UNAVAILABLE" ? null : state.price && { ...state.price, isFresh: false, isPurchasingEnabled: false }, error: priceMessages[code] });
        } else {
          // An uncertain transport outcome retains the exact request and key for explicit retry.
          update({ error: error instanceof Error ? error.message : "Unable to complete the purchase. Retry the same purchase." });
        }
      } finally { update({ busy: false }); }
    },
  };
}
