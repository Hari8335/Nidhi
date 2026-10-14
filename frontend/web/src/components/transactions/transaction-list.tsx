import Link from "next/link";
import type { TransactionSummary } from "@/types/transactions";
import { transactionLabel } from "@/lib/transactions/presentation";
import { formatLkr } from "@/lib/validation/wallet";
import { formatGoldGrams } from "@/lib/validation/gold";

export function TransactionList({ items }: { items: TransactionSummary[] }) {
  if (items.length === 0) return <div className="card" role="status"><h2>No transactions yet.</h2><p>Your completed simulated funding and gold purchases will appear here.</p><Link className="button button-secondary" href="/wallet">Add simulated funds</Link></div>;
  return <ul className="history-list" aria-label="Completed transactions">{items.map(tx => <li key={tx.transactionId}>
    <Link href={`/transactions/${tx.transactionId}`} className="history-row">
      <div><strong>{transactionLabel(tx.type)}</strong><time dateTime={tx.createdAtUtc}>{new Date(tx.createdAtUtc).toLocaleString()}</time><span className="history-reference">{tx.transactionId}</span></div>
      <div className="history-amount"><strong>{formatLkr(tx.amountLkr)}</strong>{tx.type === "GOLD_PURCHASE" && tx.goldQuantityGrams !== null && <span>{formatGoldGrams(tx.goldQuantityGrams)} g</span>}<span>Completed · View receipt →</span></div>
    </Link>
  </li>)}</ul>;
}
