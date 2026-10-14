"use client";
import { useEffect, useRef, useState } from "react";
import { api } from "@/lib/api/client";
import { transactionsPath } from "@/lib/transactions/presentation";
import type { TransactionPage } from "@/types/transactions";
import { Alert, Button, LoadingState } from "@/components/ui/primitives";
import { TransactionList } from "./transaction-list";

export function TransactionsPage() {
  const [page, setPage] = useState(1);
  const [retry, setRetry] = useState(0);
  const [data, setData] = useState<TransactionPage | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    let active = true;
    api.get<TransactionPage>(transactionsPath(page)).then(result => { if (active) setData(result); })
      .catch(e => { if (active) setError(e instanceof Error ? e.message : "Unable to load transactions."); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, retry]);
  function navigate(next: number) { setLoading(true); setError(""); setData(null); setPage(next); heading.current?.focus(); }
  function reload() { setLoading(true); setError(""); setData(null); setRetry(x => x + 1); }
  return <div className="dashboard-root">
    <div className="page-heading"><p className="eyebrow">YOUR ACTIVITY</p><h1 tabIndex={-1} ref={heading}>Transactions</h1><p>Your completed simulated operations, newest first. Receipts preserve the original values.</p></div>
    <Button className="button-secondary" disabled={loading} onClick={reload}>Refresh transactions</Button>
    {loading ? <LoadingState text="Loading your transactions…" /> : error ? <div className="card"><Alert>{error}</Alert><Button onClick={reload}>Retry transactions</Button></div> : data && <>
      <p role="status">{data.totalCount} completed {data.totalCount === 1 ? "transaction" : "transactions"}{data.totalPages > 0 && ` · Page ${data.page} of ${data.totalPages}`}</p>
      <TransactionList items={data.items} />
      {data.items.length === 0 && data.totalCount > 0 && <p>There are no transactions on this page. Return to the first page.</p>}
      {data.totalPages > 1 && <nav className="history-pagination" aria-label="Transaction pages"><Button disabled={page <= 1} onClick={() => navigate(page - 1)}>Previous page</Button><Button disabled={page >= data.totalPages} onClick={() => navigate(page + 1)}>Next page</Button>{page > 1 && <Button onClick={() => navigate(1)}>First page</Button>}</nav>}
    </>}
  </div>;
}
