import type { GoldPrice, GoldPurchaseReceipt } from "@/types/gold";

type Dashboard = { walletBalanceLkr: number; goldHoldingGrams: number };
type Client = { get<T>(path: string): Promise<T>; post<T>(path: string, body: unknown, headers: Record<string, string>): Promise<T> };
type Intent = { amountLkr: number; price: GoldPrice; key: string };
type PriceChange = { previousPrice: GoldPrice; amountLkr: number };
type State = { price: GoldPrice | null; balances: Dashboard | null; receipt: GoldPurchaseReceipt | null; error: string; busy: boolean; uncertain: boolean; loading: boolean; reviewing: boolean; reconfirm: boolean; priceChange: PriceChange | null };
const priceMessages: Record<string, string> = {
  PRICE_UNAVAILABLE: "The simulated gold price is unavailable. Refresh to try again; purchases are disabled.",
  PRICE_STALE: "The simulated gold price is stale. Purchases are blocked until a fresh price is published.",
  PRICE_CHANGED: "The simulated gold price changed. No purchase was made. Review the new price and explicitly confirm again.",
};
function codeOf(error: unknown) { return (error as { problem?: { code?: string } } | null)?.problem?.code; }

export function createPurchaseFlow(client: Client, newKey: () => string = () => crypto.randomUUID()) {
  let state: State = { price: null, balances: null, receipt: null, error: "", busy: false, uncertain: false, loading: true, reviewing: false, reconfirm: false, priceChange: null };
  let intent: Intent | null = null;
  let revision = 0;
  const listeners = new Set<() => void>();
  const update = (change: Partial<State>) => { state = { ...state, ...change }; listeners.forEach(listener => listener()); };
  async function load() {
    const request = ++revision;
    update({ loading: true });
    try {
      const [price, balances] = await Promise.all([client.get<GoldPrice>("/customer/gold-price"), client.get<Dashboard>("/customer/dashboard")]);
      if (request !== revision) return;
      const changed = intent !== null && intent.price.priceVersionId !== price.priceVersionId;
      const priceChange = changed ? { previousPrice: intent!.price, amountLkr: intent!.amountLkr } : state.priceChange;
      if (changed) intent = null;
      update({ price, balances, priceChange, ...(changed ? { reviewing: false, reconfirm: true } : {}),
        error: !price.isFresh || !price.isPurchasingEnabled ? priceMessages.PRICE_STALE : priceChange ? priceMessages.PRICE_CHANGED : "" });
    } catch (error) {
      if (request !== revision) return;
      // Keep the rejected quote for comparison even if the replacement quote cannot load.
      update({ price: null, balances: null, error: priceMessages[codeOf(error) ?? ""] ?? "Unable to load current price and balances. Refresh to try again." });
    } finally { if (request === revision) update({ loading: false }); }
  }
  function review(amount: number, updated = false) {
    if (state.busy || state.loading || (state.priceChange && !updated) || !state.price?.isFresh || !state.price.isPurchasingEnabled || !state.balances || !Number.isFinite(amount) || amount < 100 || amount > 1000000 || amount > state.balances.walletBalanceLkr || Math.round(amount * 100) / 100 !== amount) return;
    if (!intent || intent.amountLkr !== amount || intent.price.priceVersionId !== state.price.priceVersionId)
      intent = { amountLkr: amount, price: state.price, key: newKey() };
    update({ reviewing: true, priceChange: null, receipt: null, error: "" });
  }
  return {
    subscribe(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener); }; },
    getSnapshot: () => state,
    async refresh() { if (!state.busy && !state.uncertain) await load(); },
    edit() { if (!state.busy) { intent = null; update({ reviewing: false, receipt: null, priceChange: null, reconfirm: false, uncertain: false, error: state.price?.isPurchasingEnabled ? "" : state.error }); } },
    cancel() { if (!state.busy) { intent = null; update({ reviewing: false, priceChange: null, reconfirm: false, uncertain: false, error: state.price?.isPurchasingEnabled ? "" : state.error }); } },
    review,
    reviewUpdated() { if (state.priceChange) review(state.priceChange.amountLkr, true); },
    async submit() {
      if (state.busy || state.loading || !state.reviewing || !intent || !state.price?.isFresh || !state.price.isPurchasingEnabled) return;
      const submitted = intent;
      revision++; // A pending read must never overwrite the authoritative result.
      update({ busy: true, error: "" });
      try {
        const receipt = await client.post<GoldPurchaseReceipt>("/customer/gold-purchases", { amountLkr: submitted.amountLkr, priceVersionId: submitted.price.priceVersionId }, { "Idempotency-Key": submitted.key });
        intent = null;
        update({ receipt, uncertain: false, reviewing: false, reconfirm: false, priceChange: null, balances: { walletBalanceLkr: receipt.postOperationWalletBalanceLkr, goldHoldingGrams: receipt.postOperationGoldHoldingGrams } });
      } catch (error) {
        const code = codeOf(error);
        if (code === "PRICE_CHANGED") {
          intent = null;
          update({ uncertain: false, price: null, reviewing: false, reconfirm: true, priceChange: { previousPrice: submitted.price, amountLkr: submitted.amountLkr }, error: priceMessages.PRICE_CHANGED });
          await load();
        } else if (code === "PRICE_UNAVAILABLE" || code === "PRICE_STALE") {
          intent = null;
          update({ uncertain: false, reviewing: false, price: code === "PRICE_UNAVAILABLE" ? null : state.price && { ...state.price, isFresh: false, isPurchasingEnabled: false }, error: priceMessages[code] });
        } else {
          // An uncertain transport outcome retains the exact request and key for explicit retry.
          const status = (error as { status?: number } | null)?.status;
          const uncertain = status === undefined || status === 0 || status >= 500 || code === "OPERATION_IN_PROGRESS";
          update({ uncertain, error: `${error instanceof Error ? error.message : "Unable to complete the purchase."}${uncertain ? " The outcome is uncertain. Retry this same purchase to recover its receipt safely. Check your transaction history before starting another purchase." : ""}` });
        }
      } finally { update({ busy: false }); }
    },
  };
}
