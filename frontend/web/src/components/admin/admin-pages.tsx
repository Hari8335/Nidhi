"use client";
import Link from "next/link";
import { formatDateTime } from "@/lib/format/date";
import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api/client";
import { Alert, Button, LoadingState } from "@/components/ui/primitives";
import { adminPath, exact, parseAdminJson, priceRequest, transactionLabel, unitLabel, validatePrice } from "@/lib/admin/presentation";
import type { Audit, Customer, Dashboard, Ledger, PageResult, Price, Transaction } from "@/lib/admin/presentation";

function useRead<T>(path: string) {
  const [state, setState] = useState<{ path: string; data?: T; error?: string }>({ path: "" });
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    api.get<T>(path, parseAdminJson<T>).then(data => { if (active) setState({ path, data }); })
      .catch(e => { if (active) setState({ path, error: e instanceof Error ? e.message : "Unable to load operational data." }); });
    return () => { active = false; };
  }, [path, revision]);
  return { data: state.path === path ? state.data : undefined, error: state.path === path ? state.error : undefined,
    reload: () => { setState({ path: "" }); setRevision(x => x + 1); } };
}
function Heading({ title, children }: { title: string; children?: React.ReactNode }) {
  const ref = useRef<HTMLHeadingElement>(null);
  useEffect(() => { ref.current?.focus(); }, []);
  return <div className="page-heading"><p className="eyebrow">NIDHI OPERATIONS</p><h1 ref={ref} tabIndex={-1}>{title}</h1><p>{children}</p></div>;
}
function ReadState({ error, reload }: { error?: string; reload: () => void }) {
  return error ? <div className="card"><Alert>{error}</Alert><Button onClick={reload}>Retry</Button></div> : <LoadingState text="Loading operational data…" />;
}
function Fields({ values }: { values: [string, React.ReactNode][] }) {
  return <dl className="admin-fields">{values.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>;
}
function DateText({ value }: { value: string }) { return <time dateTime={value}>{formatDateTime(value)}</time>; }
function PriceFields({ price }: { price: Price | null }) {
  return price ? <Fields values={[["Simulated LKR per gram", exact(price.pricePerGramLkr, 4)], ["Version", price.priceVersionId], ["Published", <DateText key="time" value={price.publishedAtUtc} />], ...(price.isFresh === undefined ? [] : [["Purchasing price", price.isFresh ? "Fresh" : "Stale — new purchases disabled"] as [string, React.ReactNode]])]} /> : <p>No simulated price has been published. Publish the first price to enable customer purchases.</p>;
}
export function AdminDashboardPage() {
  const read = useRead<Dashboard>("/admin/dashboard");
  return <div className="dashboard-root"><Heading title="Operational dashboard">Persisted customer and completed transaction activity.</Heading>{read.data ? <><div className="admin-metrics"><section className="card"><h2>Registered customers</h2><p className="admin-number">{read.data.totalCustomersCount}</p></section><section className="card"><h2>Completed transactions</h2><p className="admin-number">{read.data.totalCompletedTransactionsCount}</p></section></div><section className="card"><h2>Current simulated gold price</h2><PriceFields price={read.data.activeGoldPrice} /><Link href="/admin/prices">Manage price publications →</Link></section><Button onClick={read.reload}>Refresh dashboard</Button></> : <ReadState {...read} />}</div>;
}
function Browser<T>({ title, resource, filters = [], initialFilters = {}, render }: { initialFilters?: Record<string, string>; title: string; resource: string; filters?: { key: string; label: string; options?: string[] }[]; render: (item: T) => React.ReactNode }) {
  const [page, setPage] = useState(1);
  const [query, setQuery] = useState<Record<string, string>>(initialFilters);
  const read = useRead<PageResult<T>>(adminPath(resource, page, query));
  return <div className="dashboard-root"><Heading title={title}>Read-only operational inspection. Newest records first.</Heading>{filters.length > 0 && <form className="admin-filters" onSubmit={e => { e.preventDefault(); const values = Object.fromEntries(new FormData(e.currentTarget).entries()) as Record<string, string>; setPage(1); setQuery(values); }}>{filters.map(f => <label key={f.key}>{f.label}{f.options ? <select name={f.key}><option value="">All types</option>{f.options.map(v => <option key={v}>{v}</option>)}</select> : <input name={f.key} maxLength={256} defaultValue={initialFilters[f.key]} />}</label>)}<Button type="submit">Apply filters</Button></form>}
    {read.data ? <><p role="status">{read.data.totalCount} records · Page {read.data.page}{read.data.totalPages > 0 && ` of ${read.data.totalPages}`}</p><div className="admin-records">{read.data.items.map(render)}</div>{read.data.items.length === 0 && <section className="card"><p>No records match this page and filter.</p></section>}<nav className="history-pagination" aria-label={`${title} pages`}><Button disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous page</Button><Button disabled={page >= read.data.totalPages} onClick={() => setPage(p => p + 1)}>Next page</Button><Button onClick={() => { setPage(1); read.reload(); }}>Refresh first page</Button></nav></> : <ReadState {...read} />}</div>;
}
export function AdminCustomersPage() { return <Browser<Customer> title="Customers" resource="customers" filters={[{ key: "search", label: "Email or customer ID" }]} render={c => <article className="card" key={c.id}><h2><Link href={`/admin/customers/${c.id}`}>{c.displayName || c.email || c.id}</Link></h2><Fields values={[["Customer", c.id], ["Email", c.email], ["Verification", c.isEmailVerified ? "Verified" : "Unverified"], ["Registered", <DateText key="date" value={c.createdAtUtc} />]]} /></article>} />; }
export function AdminCustomerPage({ id }: { id: string }) {
  const read = useRead<Customer>(`/admin/customers/${encodeURIComponent(id)}`);
  return <div className="dashboard-root"><Heading title="Customer detail">Current persisted simulated balances. Inspection only.</Heading><Link href="/admin/customers">← Customers</Link>{read.data ? <section className="card"><Fields values={[["Customer", read.data.id], ["Display name", read.data.displayName || "Not provided"], ["Email", read.data.email], ["Verification", read.data.isEmailVerified ? "Verified" : "Unverified"], ["Registered", <DateText key="date" value={read.data.createdAtUtc} />], ["Simulated wallet LKR", exact(read.data.walletBalanceLkr, 2)], ["Simulated gold grams", exact(read.data.goldHoldingGrams, 8)]]} /><Link href={`/admin/transactions?customerId=${read.data.id}`}>Inspect customer transactions →</Link></section> : <ReadState {...read} />}</div>;
}
function TransactionCard({ item }: { item: Transaction }) { return <article className="card"><h2><Link href={`/admin/transactions/${item.transactionId}`}>{transactionLabel(item.type)}</Link></h2><Fields values={[["Transaction", item.transactionId], ["Customer", <Link key="customer" href={`/admin/customers/${item.customerId}`}>{item.customerId}</Link>], ["Status", item.status], ["Simulated LKR", exact(item.amountLkr, 2)], ...(item.goldQuantityGrams == null ? [] : [["Simulated grams", exact(item.goldQuantityGrams, 8)] as [string, React.ReactNode]]), ["Completed", <DateText key="date" value={item.createdAtUtc} />]]} /><Link href={`/admin/ledger?transactionId=${item.transactionId}`}>Inspect ledger →</Link></article>; }
export function AdminTransactionsPage({ customerId = "" }: { customerId?: string }) { return <Browser<Transaction> initialFilters={{ customerId }} title="Transactions" resource="transactions" filters={[{ key: "customerId", label: "Customer ID" }, { key: "type", label: "Transaction type", options: ["WALLET_FUNDING", "GOLD_PURCHASE"] }]} render={t => <TransactionCard key={t.transactionId} item={t} />} />; }
export function AdminTransactionPage({ id }: { id: string }) {
  const read = useRead<Transaction>(`/admin/transactions/${encodeURIComponent(id)}`);
  const t = read.data;
  return <div className="dashboard-root"><Heading title="Transaction detail">Immutable receipt values from the completed operation.</Heading><Link href="/admin/transactions">← Transactions</Link>{t ? <><TransactionCard item={t} /><section className="card"><h2>Original operation snapshot</h2><Fields values={[["Wallet after operation · LKR", exact(t.postOperationWalletBalanceLkr, 2)], ...(t.type === "GOLD_PURCHASE" ? [["Original price version", t.priceVersionId], ["Applied LKR per gram", exact(t.appliedPricePerGramLkr, 4)], ["Gold holding after operation · grams", exact(t.postOperationGoldHoldingGrams, 8)], ["Conversion residual · LKR", exact(t.conversionResidualLkr, 12)]] as [string, React.ReactNode][] : [])]} /></section></> : <ReadState {...read} />}</div>;
}
export function AdminLedgerPage({ transactionId = "" }: { transactionId?: string }) {
  const [id, setId] = useState(transactionId);
  return <div className="dashboard-root"><Heading title="Ledger inspection">Separate LKR and GRAMS books for one completed transaction.</Heading><form className="admin-filters" onSubmit={e => { e.preventDefault(); setId(String(new FormData(e.currentTarget).get("id") || "").trim()); }}><label>Transaction ID<input name="id" required defaultValue={id} key={id} /></label><Button type="submit">Inspect postings</Button></form>{id ? <LedgerResult id={id} /> : <section className="card"><p>Select a transaction from the transaction browser or enter its reference.</p><Link href="/admin/transactions">Browse transactions →</Link></section>}</div>;
}
function LedgerResult({ id }: { id: string }) {
  const read = useRead<Ledger>(`/admin/transactions/${encodeURIComponent(id)}/ledger`);
  return read.data ? <><p className="admin-reference">Transaction: {read.data.transactionId}</p>{([ ["LKR", read.data.lkrBook], ["GRAMS", read.data.goldBook] ] as const).map(([unit, book]) => <section key={unit} className="card"><h2>{unit} book</h2><p>{book.isBalanced ? "Balanced" : "Unbalanced"} · {book.entries.length} postings</p>{book.entries.length === 0 && <p>No postings in this unit.</p>}{book.entries.map(e => <article className="admin-posting" key={e.entryId}><Fields values={[["Account", e.accountNumber], ["Name", e.accountName], ["Unit", unitLabel(e.unit)], ["Direction", e.direction], ["Positive amount", `${exact(e.amount, unit === "LKR" ? 2 : 8)} ${unit}`], ["Entry", e.entryId], ["Posted", <DateText key="time" value={e.createdAtUtc} />]]} /></article>)}</section>)}</> : <ReadState {...read} />;
}
export function AdminAuditPage() { return <Browser<Audit> title="Audit logs" resource="audit-events" filters={[{ key: "actorId", label: "Actor ID" }, { key: "action", label: "Exact action" }]} render={a => <article className="card" key={a.id}><h2>{a.action}</h2><Fields values={[["Event", a.id], ["Actor", `${a.actorRole} · ${a.actorId}`], ["Target", `${a.entityType} · ${a.entityId}`], ["Recorded", <DateText key="date" value={a.timestampUtc} />], ...Object.entries(a.details).map(([k, v]) => [k, String(v)] as [string, React.ReactNode])]} /></article>} />; }
export function AdminPricesPage() {
  const [page, setPage] = useState(1);
  const history = useRead<PageResult<Price>>(adminPath("prices", page));
  const current = useRead<Dashboard>("/admin/dashboard");
  const [price, setPrice] = useState(""); const [reason, setReason] = useState("");
  const [confirm, setConfirm] = useState(false); const [busy, setBusy] = useState(false);
  const [error, setError] = useState(""); const [published, setPublished] = useState<Price | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const publishLock = useRef(false);
  async function publish() {
    if (publishLock.current) return; publishLock.current = true; setBusy(true); setError("");
    try { const result = await api.post<Price>("/admin/prices", priceRequest(price, reason), undefined, parseAdminJson<Price>); setPublished(result); setConfirm(false); setPrice(""); setReason(""); setPage(1); history.reload(); current.reload(); heading.current?.focus(); }
    catch (e) { setError(e instanceof ApiError && (e.status === 0 || e.status >= 500) ? "Publication could not be confirmed. Refresh price history before retrying; a new version may already have been committed." : e instanceof Error ? e.message : "Unable to publish. Inspect price history before retrying."); }
    finally { publishLock.current = false; setBusy(false); }
  }
  return <div className="dashboard-root"><Heading title="Simulated gold prices">Immutable publications. Every new version requires customer reconfirmation of older displayed prices.</Heading><h2 ref={heading} tabIndex={-1}>Current simulated price</h2><section className="card">{current.data ? <PriceFields price={current.data.activeGoldPrice} /> : <ReadState {...current} />}</section>{published && <section className="card" role="status"><h2>Price published successfully</h2><PriceFields price={published} /></section>}<section className="card"><h2>Publish a simulated price</h2><p>Publication takes effect immediately. Customers using an older version receive PRICE_CHANGED and must reconfirm.</p><form className="admin-price-form" onSubmit={e => { e.preventDefault(); const message = validatePrice(price); if (message || !reason.trim() || reason.trim().length > 500) { setError(message || "Enter a reason of 1–500 characters."); return; } setError(""); setConfirm(true); }}><label htmlFor="price">LKR per gram</label><input id="price" required autoComplete="off" inputMode="decimal" value={price} onChange={e => { setPrice(e.target.value); setConfirm(false); }} disabled={busy} aria-invalid={!!error} aria-describedby={`price-help${error ? " price-error" : ""}`} /><p id="price-help">Positive value, at most four decimal places. Simulation only.</p><label htmlFor="reason">Publication reason</label><textarea id="reason" required maxLength={500} value={reason} onChange={e => { setReason(e.target.value); setConfirm(false); }} disabled={busy} aria-invalid={!!error} aria-describedby={error ? "price-error" : undefined} /><p>Use operational context only. Do not enter passwords, tokens or credentials.</p>{error && <div id="price-error"><Alert>{error}</Alert></div>}{!confirm && <Button type="submit" disabled={busy}>Review publication</Button>}{confirm && <div className="admin-confirm" role="group" aria-label="Confirm price publication"><p>Publish simulated LKR {price} per gram with reason: {reason}?</p><Button type="button" busy={busy} onClick={() => void publish()}>Confirm publication</Button><Button type="button" disabled={busy} className="button-secondary" onClick={() => setConfirm(false)}>Cancel</Button></div>}</form></section><section><h2>Immutable price history</h2>{history.data ? <><p role="status">{history.data.totalCount} publications · Page {page}</p><div className="admin-records">{history.data.items.map(p => <article className="card" key={p.priceVersionId}><PriceFields price={p} /><Fields values={[["Published by admin", p.publishedByAdminId], ["Reason", p.reason]]} /></article>)}</div>{history.data.items.length === 0 && <p>No price publications on this page.</p>}<nav className="history-pagination" aria-label="Price history pages"><Button disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous page</Button><Button disabled={page >= history.data!.totalPages} onClick={() => setPage(p => p + 1)}>Next page</Button><Button onClick={() => { setPage(1); history.reload(); current.reload(); }}>Refresh prices</Button></nav></> : <ReadState {...history} />}</section></div>;
}
