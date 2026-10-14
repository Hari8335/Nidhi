"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import { useAuth } from "@/components/auth/auth-provider";
import { Brand } from "@/components/shared/brand";
import { Alert, Button, LoadingState, StatusBadge } from "@/components/ui/primitives";
import { api, ApiError } from "@/lib/api/client";
import { roleHome } from "@/lib/auth/routing";
import type { Role } from "@/types/auth";

export function ApplicationShell({ role, children }: { role: Role; children: ReactNode }) {
  const { session, clear, refresh } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [more, setMore] = useState(false);
  const home = session.status === "authenticated" ? roleHome(session.user.roles) : null;
  const expectedHome = role === "ADMIN" ? "/admin" : "/dashboard";
  useEffect(() => {
    if (session.status === "anonymous") router.replace("/login");
    else if (home && home !== expectedHome) router.replace(home);
  }, [session.status, home, expectedHome, router]);
  if (session.status === "error") return <main id="main-content" className="session-state"><Alert>{session.message}</Alert><Button onClick={() => void refresh()}>Retry session check</Button><Link href="/login">Go to sign in</Link></main>;
  if (session.status === "authenticated" && !home) return <main id="main-content" className="session-state"><Alert>Your account does not have access to this application.</Alert><Link href="/login">Go to sign in</Link></main>;
  if (session.status !== "authenticated" || home !== expectedHome) return <main id="main-content"><LoadingState /></main>;
  async function logout() {
    if (busy) return;
    setBusy(true); setError("");
    try { await api.post<void>("/auth/logout"); clear(); router.replace("/login"); }
    catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) { clear(); router.replace("/login"); }
      else setError(failure instanceof ApiError ? failure.message : "Unable to sign out. Please try again.");
    } finally { setBusy(false); }
  }
  const admin = role === "ADMIN";
  const future = admin ? ["Customers", "Transactions", "Ledger", "Gold Prices", "Audit Logs"] : ["Savings Goal"];
  const nav = <><Link href={expectedHome} aria-current={pathname === expectedHome ? "page" : undefined}>{admin ? "Overview" : "Dashboard"}</Link>{!admin && <><Link href="/wallet" aria-current={pathname === "/wallet" ? "page" : undefined}>Wallet</Link><Link href="/gold" aria-current={pathname === "/gold" ? "page" : undefined}>Gold</Link><Link href="/transactions" aria-current={pathname.startsWith("/transactions") ? "page" : undefined}>Transactions</Link></>}{future.map(label => <span key={label} aria-disabled="true">{label}<small>Soon</small></span>)}{!admin && <Link href="/profile" aria-current={pathname === "/profile" ? "page" : undefined}>Profile</Link>}</>;
  return <div className={`application ${admin ? "admin-application" : "customer-application"}`}>
    <aside className="sidebar"><Brand /><p className="eyebrow">{admin ? "ADMINISTRATION" : "YOUR SAVING SPACE"}</p><nav aria-label={admin ? "Administration" : "Customer"}>{nav}</nav><p className="sidebar-note">A considered start.<br />One small step at a time.</p></aside>
    <div className="application-body"><header className="application-header"><div className="mobile-brand"><Brand /></div><StatusBadge>{admin ? "Administrator" : "Customer account"}</StatusBadge><div className="account-summary"><span>{session.user.displayName || session.user.email}</span><Button className="button-secondary" onClick={() => void logout()} busy={busy}>Sign out</Button></div></header>
      {admin && <details className="admin-mobile-nav"><summary>Administration menu</summary><nav aria-label="Mobile administration">{nav}</nav></details>}
      <main id="main-content" className="application-content">{error && <Alert>{error}</Alert>}{children}<p className="simulation-note">Simulation only. No real money, gold custody, or monetary value.</p></main>
    </div>
    {!admin && <><nav className="bottom-nav" aria-label="Mobile customer"><Link href="/dashboard" aria-current={pathname === "/dashboard" ? "page" : undefined}>Home</Link><Link href="/wallet" aria-current={pathname === "/wallet" ? "page" : undefined}>Wallet</Link><Link href="/gold" aria-current={pathname === "/gold" ? "page" : undefined}>Gold</Link><Link href="/transactions" aria-current={pathname.startsWith("/transactions") ? "page" : undefined}>Activity</Link><button type="button" aria-expanded={more} aria-controls="mobile-more" onClick={() => setMore(!more)}>More</button></nav>{more && <nav id="mobile-more" className="mobile-more" aria-label="More destinations"><Link href="/profile" onClick={() => setMore(false)}>Profile</Link><span aria-disabled="true">Savings Goal · Coming soon</span><Button className="button-secondary" onClick={() => setMore(false)}>Close menu</Button></nav>}</>}
  </div>;
}
