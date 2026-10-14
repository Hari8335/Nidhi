"use client";
import { useEffect, useState, useSyncExternalStore } from "react";
import { api } from "@/lib/api/client";
import { createPurchaseFlow } from "@/lib/gold/purchase-flow";
import { formatLkr } from "@/lib/validation/wallet";
import { formatGoldGrams, truncateGoldPreview, validatePurchaseAmount } from "@/lib/validation/gold";

export function GoldPage() {
  const [flow] = useState(() => createPurchaseFlow(api));
  const state = useSyncExternalStore(flow.subscribe, flow.getSnapshot, flow.getSnapshot);
  const [amount, setAmount] = useState("");
  useEffect(() => { void flow.refresh(); }, [flow]);
  const { price, balances, receipt, error, busy, reviewing, reconfirm } = state;
  const valid = validatePurchaseAmount(amount, balances?.walletBalanceLkr ?? 0).isValid;
  const estimate = price ? truncateGoldPreview(Number(amount), price.pricePerGramLkr) : 0;
  const enabled = Boolean(price?.isFresh && price.isPurchasingEnabled && valid && !busy);
  return <div className="dashboard-root min-w-0">
    <div className="page-heading"><p className="eyebrow">SIMULATED GOLD</p><h1>Buy simulated gold</h1><p>Convert simulated LKR into fractional simulated gold. No real money or gold custody.</p></div>
    {error && <div role="alert">{error}</div>}
    {price && <section className="dashboard-hero-card min-w-0">
      <p className="metric-label">Current simulated price</p><h2>{formatLkr(price.pricePerGramLkr)} / g</h2>
      <p>Published {new Date(price.publishedAtUtc).toLocaleString()} · {price.isFresh ? "Fresh" : "Temporarily stale"}</p>
      {balances && <p>Wallet {formatLkr(balances.walletBalanceLkr)} · Holding {formatGoldGrams(balances.goldHoldingGrams)} g</p>}
      <label htmlFor="gold-amount">Amount to allocate (LKR)</label>
      <input id="gold-amount" className="w-full min-w-0" inputMode="decimal" value={amount} disabled={busy || reviewing} aria-invalid={amount !== "" && !valid} aria-describedby="gold-amount-help" onChange={e => { flow.edit(); setAmount(e.target.value); }} placeholder="100.00" />
      <p id="gold-amount-help">{amount && !valid ? "Enter LKR 100.00–1,000,000.00, with at most two decimals and within your wallet balance. " : ""}Estimated quantity: {formatGoldGrams(estimate)} g. Preview is truncated to eight decimals; the backend receipt is final.</p>
      {!reviewing ? <button className="button-primary" disabled={!enabled} onClick={() => flow.review(Number(amount))}>{reconfirm ? "Review updated purchase" : "Review purchase"}</button> :
        <div role="group" aria-label="Confirm simulated gold purchase">
          {reconfirm && <p>Price updated. Confirming creates a new purchase intent at this price.</p>}
          <p>Confirm {formatLkr(Number(amount))} at {formatLkr(price.pricePerGramLkr)} / g for approximately {formatGoldGrams(estimate)} g?</p>
          <p className="break-all">Price version: {price.priceVersionId}</p>
          <button className="button-primary" onClick={() => void flow.submit()} disabled={!enabled}>{busy ? "Purchasing…" : reconfirm ? "Confirm updated purchase" : "Confirm purchase"}</button>
          <button className="button-secondary" disabled={busy} onClick={flow.cancel}>Change amount</button>
        </div>}
    </section>}
    {receipt && <section role="status" className="min-w-0">
      <h2>Purchase successful</h2>
      <p>{formatLkr(receipt.amountLkr)} → {formatGoldGrams(receipt.goldQuantityGrams)} g at {receipt.appliedPricePerGramLkr.toFixed(4)} LKR / g</p>
      <p>Wallet: {formatLkr(receipt.postOperationWalletBalanceLkr)} · Holding: {formatGoldGrams(receipt.postOperationGoldHoldingGrams)} g</p>
      <p className="font-mono break-all">Transaction: {receipt.transactionId}</p>
      <p className="font-mono break-all">Price version: {receipt.priceVersionId}</p>
      <p>Completed at <time dateTime={receipt.createdAtUtc}>{receipt.createdAtUtc} (UTC)</time></p>
    </section>}
    {!price && !error && <p role="status">Loading simulated gold price…</p>}
    <button className="button-secondary" disabled={busy} onClick={() => void flow.refresh()}>Refresh price</button>
  </div>;
}
