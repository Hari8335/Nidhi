"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useAuth } from "@/components/auth/auth-provider";
import { api } from "@/lib/api/client";
import { formatLkr } from "@/lib/validation/wallet";
import { formatGoldGrams } from "@/lib/validation/gold";
import { transactionsPath } from "@/lib/transactions/presentation";
import type { Holdings, TransactionPage } from "@/types/transactions";
import type { WalletSummary } from "@/types/wallet";
import { TransactionList } from "@/components/transactions/transaction-list";
import { Alert, Button, LoadingState } from "@/components/ui/primitives";

export function HoldingsSummary({ refreshKey }: { refreshKey?: string }) {
  const { session } = useAuth();
  const verified = session.status === "authenticated" && session.user.isEmailVerified;
  const [data, setData] = useState<{ holding: Holdings; wallet: WalletSummary; history: TransactionPage } | null>(null);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  const [loadedKey, setLoadedKey] = useState<string | undefined>(undefined);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    if (!verified) return;
    let active = true;
    Promise.all([api.get<Holdings>("/customer/holdings"), api.get<WalletSummary>("/customer/wallet"), api.get<TransactionPage>(transactionsPath(1, 5))])
      .then(([holding, wallet, history]) => { if (active) { setData({ holding, wallet, history }); setError(""); setLoadedKey(refreshKey); } })
      .catch(e => { if (active) { setError(e instanceof Error ? e.message : "Unable to load holdings."); setLoadedKey(refreshKey); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [verified, refreshKey, retry]);
  if (!verified) return <Alert tone="info">Verify your email to view simulated holdings and transaction receipts. <Link href="/verify-email">Verify email</Link></Alert>;
  function refresh() { setError(""); setLoading(true); setRetry(x => x + 1); }
  return <section aria-label="Your simulated holdings" className="holdings-summary">
    <h2>Your simulated holding</h2>
    {loading || loadedKey !== refreshKey ? <LoadingState text="Loading persisted holdings…" /> : error ? <Alert>{error}</Alert> : data && <>
      <div className="dashboard-hero-card"><p className="metric-label">Simulated gold</p><p className="holding-quantity">{formatGoldGrams(data.holding.quantityGrams)} g</p><p>Simulated wallet: {formatLkr(data.wallet.balanceLkr)}</p>
        {data.holding.indicativePortfolioValueLkr !== null ? <><p>Current indicative simulated value: {formatLkr(data.holding.indicativePortfolioValueLkr)}</p><p>At LKR {data.holding.activePricePerGramLkr?.toFixed(4)} / g. This is a current simulation estimate.</p></> : <p>Current indicative value unavailable: {data.holding.activePricePerGramLkr === null ? "no simulated price has been published." : "the simulated price is stale."}</p>}
        {data.holding.priceVersionId && <p className="break-all">Current price version: {data.holding.priceVersionId}</p>}
      </div>
      <h3>Recent activity</h3><TransactionList items={data.history.items} />
    </>}
    <div className="history-pagination"><Button className="button-secondary" onClick={refresh} disabled={loading}>Refresh holdings</Button><Link href="/transactions" className="button button-secondary">Full transaction history</Link></div>
  </section>;
}
