"use client";
import Link from "next/link";
import { HoldingsSummary } from "./holdings-summary";
import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { api } from "@/lib/api/client";
import { createPurchaseFlow } from "@/lib/gold/purchase-flow";
import { formatLkr } from "@/lib/validation/wallet";
import { formatGoldGrams, formatGoldPrice, truncateGoldPreview, validatePurchaseAmount } from "@/lib/validation/gold";
import { formatDateTime } from "@/lib/format/date";
import { Alert, Button, FormField, Input, LoadingState } from "@/components/ui/primitives";

export function GoldPage() {
  const [flow] = useState(() => createPurchaseFlow(api));
  const state = useSyncExternalStore(flow.subscribe, flow.getSnapshot, flow.getSnapshot);
  const [amount, setAmount] = useState("");
  const changeHeading = useRef<HTMLHeadingElement>(null);
  const confirmation = useRef<HTMLHeadingElement>(null);
  const success = useRef<HTMLHeadingElement>(null);
  const amountField = useRef<HTMLInputElement>(null);
  useEffect(() => { void flow.refresh(); }, [flow]);
  const { price, balances, receipt, error, busy, loading, reviewing, reconfirm, priceChange } = state;
  useEffect(() => { if (priceChange) changeHeading.current?.focus(); else if (reviewing) confirmation.current?.focus(); else if (receipt) success.current?.focus(); }, [priceChange, reviewing, receipt]);
  const valid = balances !== null && validatePurchaseAmount(amount, balances.walletBalanceLkr).isValid;
  const estimate = price && valid ? truncateGoldPreview(Number(amount), price.pricePerGramLkr) : null;
  const enabled = Boolean(price?.isFresh && price.isPurchasingEnabled && valid && !busy && !loading);
  function cancel() { flow.cancel(); requestAnimationFrame(() => amountField.current?.focus()); }
  return <div className="dashboard-root min-w-0">
    <div className="page-heading"><p className="eyebrow">SIMULATED GOLD</p><h1>Simulated gold</h1><p>Convert simulated LKR into fractional simulated gold. No real money or gold custody.</p></div>
    <HoldingsSummary refreshKey={receipt?.transactionId} />
    {error && !priceChange && <Alert>{error}</Alert>}
    {priceChange && <section className="card price-change" aria-labelledby="price-change-title" aria-busy={loading}>
      <h2 id="price-change-title" tabIndex={-1} ref={changeHeading}>Gold price changed</h2>
      <p role="alert">The simulated gold price changed before your purchase was completed. <strong>No purchase was made.</strong></p>
      <dl className="receipt-fields">
        <div><dt>Previous price</dt><dd>{formatGoldPrice(priceChange.previousPrice.pricePerGramLkr)} / g</dd></div>
        <div><dt>Current price</dt><dd>{price ? `${formatGoldPrice(price.pricePerGramLkr)} / g` : loading ? "Loading current price…" : "Currently unavailable"}</dd></div>
        <div><dt>Amount</dt><dd>{formatLkr(priceChange.amountLkr)}</dd></div>
        <div><dt>Updated estimate</dt><dd>{price?.isPurchasingEnabled ? `${formatGoldGrams(truncateGoldPreview(priceChange.amountLkr, price.pricePerGramLkr))} g` : "Unavailable until a fresh price is published"}</dd></div>
      </dl>
      {price && <p>Published <time dateTime={price.publishedAtUtc}>{formatDateTime(price.publishedAtUtc)}</time> · {price.isFresh ? "Fresh" : "Temporarily stale"}</p>}
      {!loading && (!price || !price.isPurchasingEnabled) && <Alert>{error}</Alert>}
      <div className="history-pagination"><Button className="button-secondary" disabled={busy} onClick={cancel}>Cancel</Button><Button disabled={!enabled} onClick={flow.reviewUpdated}>Review new price</Button></div>
    </section>}
    {price && !priceChange && <section className="card gold-purchase-card">
      <p className="metric-label">Current simulated price</p><h2>{formatGoldPrice(price.pricePerGramLkr)} / g</h2>
      <p>Published <time dateTime={price.publishedAtUtc}>{formatDateTime(price.publishedAtUtc)}</time> · {price.isFresh ? "Fresh" : "Temporarily stale"}</p>
      {balances && <p>Available: {formatLkr(balances.walletBalanceLkr)}</p>}
      <form onSubmit={e => { e.preventDefault(); flow.review(Number(amount)); }} aria-busy={busy}>
        <FormField id="gold-amount" label="Amount to allocate (LKR)" hint="LKR 100.00–1,000,000.00, with up to two decimal places, within your available balance." error={amount !== "" && !valid ? "Enter an amount within these limits and your available balance." : undefined}>
          <Input ref={amountField} id="gold-amount" name="amount" inputMode="decimal" autoComplete="off" required value={amount} disabled={busy || reviewing || loading} aria-invalid={amount !== "" && !valid} aria-describedby={`gold-amount-hint${amount && !valid ? " gold-amount-error" : ""}`} onChange={e => { flow.edit(); setAmount(e.target.value); }} placeholder="100.00" />
        </FormField>
        <p>Estimated quantity: {estimate === null ? "Enter a valid amount" : `${formatGoldGrams(estimate)} g`}. Estimates are rounded down to eight decimal places. Final quantity will be confirmed when you complete the purchase.</p>
        {!reviewing && <Button type="submit" disabled={!enabled}>Review purchase</Button>}
      </form>
      {reviewing && <div role="group" aria-label="Confirm simulated gold purchase" className="gold-confirmation">
        <h3 tabIndex={-1} ref={confirmation}>Review your {reconfirm ? "updated " : ""}purchase</h3>
        <p>Confirm {formatLkr(Number(amount))} at {formatGoldPrice(price.pricePerGramLkr)} / g for approximately {formatGoldGrams(estimate ?? 0)} g?</p>
        <Button onClick={() => void flow.submit()} disabled={!enabled} busy={busy}>{busy ? "Purchasing…" : reconfirm ? "Confirm updated purchase" : "Confirm purchase"}</Button>
        <Button className="button-secondary" disabled={busy} onClick={cancel}>Change amount</Button>
      </div>}
    </section>}
    {receipt && <section role="status" className="card">
      <h2 tabIndex={-1} ref={success}>Purchase successful</h2>
      <p>{formatLkr(receipt.amountLkr)} → {formatGoldGrams(receipt.goldQuantityGrams)} g at {receipt.appliedPricePerGramLkr.toFixed(4)} LKR / g</p>
      <p>Wallet: {formatLkr(receipt.postOperationWalletBalanceLkr)} · Holding: {formatGoldGrams(receipt.postOperationGoldHoldingGrams)} g</p>
      <p>Completed at <time dateTime={receipt.createdAtUtc}>{formatDateTime(receipt.createdAtUtc)}</time></p>
      <Link href={`/transactions/${receipt.transactionId}`}>View full receipt</Link>
    </section>}
    {loading && !priceChange && <LoadingState text="Loading simulated gold price…" />}
    <Button className="button-secondary" disabled={busy || loading || state.uncertain} onClick={() => void flow.refresh()}>Refresh price</Button>
  </div>;
}
