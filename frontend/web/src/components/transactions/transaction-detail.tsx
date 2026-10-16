"use client";
import Link from "next/link";
import { formatDateTime } from "@/lib/format/date";
import { useEffect, useState } from "react";
import { api } from "@/lib/api/client";
import { receiptFields } from "@/lib/transactions/presentation";
import type { TransactionReceipt } from "@/types/transactions";
import { Alert, Button, LoadingState } from "@/components/ui/primitives";

export function TransactionDetail({ id }: { id: string }) {
  const [receipt, setReceipt] = useState<TransactionReceipt | null>(null);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    let active = true;
    api.get<TransactionReceipt>(`/customer/transactions/${encodeURIComponent(id)}`)
      .then(r => { if (active) setReceipt(r); }).catch(e => { if (active) setError(e instanceof Error ? e.message : "Unable to load receipt."); });
    return () => { active = false; };
  }, [id, retry]);
  return <div className="dashboard-root"><Link href="/transactions" className="button button-secondary">← All transactions</Link>
    <div className="page-heading"><p className="eyebrow">YOUR RECORD</p><h1>Transaction receipt</h1><p>A completed simulated operation. All values below are from its original receipt.</p></div>
    {error ? <div className="card"><Alert>{error}</Alert><Button onClick={() => { setError(""); setRetry(x => x + 1); }}>Retry receipt</Button></div> : receipt ? <section className="card"><h2>Completed</h2><dl className="receipt-fields">{receiptFields(receipt).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{label === "Completed at (UTC)" ? <time dateTime={value} title={value}>{formatDateTime(value)}</time> : value}</dd></div>)}</dl>{receipt.type === "GOLD_PURCHASE" && <p>The conversion residual is a rounding delta, not a fee or refund.</p>}</section> : <LoadingState text="Loading original receipt…" />}
  </div>;
}
