"use client";
import Link from "next/link";
import { useAuth } from "./auth-provider";
import { Alert, Card, StatusBadge } from "@/components/ui/primitives";
export function AccountOverview({ profile = false, admin = false }: { profile?: boolean; admin?: boolean }) {
  const { session } = useAuth();
  if (session.status !== "authenticated") return null;
  const user = session.user;
  return <>
    <div className="page-heading"><p className="eyebrow">{admin ? "ADMINISTRATION" : profile ? "YOUR ACCOUNT" : "A SMALL STEP FORWARD"}</p><h1>{profile ? "Your profile" : admin ? "Overview" : `Welcome${user.displayName ? `, ${user.displayName}` : " to Nidhi"}`}</h1><p>{profile ? "Your current account details." : admin ? "Your Nidhi administration workspace." : "Your saving space is taking shape."}</p></div>
    {!user.isEmailVerified && <Alert tone="info">Your email is not verified yet. <Link href="/verify-email">Verify your email</Link> to use simulated wallet funding and gold purchases.</Alert>}
    {!profile && <Card className="foundation-card"><p className="eyebrow">{admin ? "WORKSPACE READY" : "WELCOME TO YOUR NEXT CHAPTER"}</p><h2>{admin ? "A clear view, as we grow." : "Small savings. Steady habits."}</h2><p>{admin ? "Customer, transaction, ledger, gold price, and audit tools will appear here as they are implemented." : "Wallet, gold, transactions, and savings goals will be available as they are implemented."}</p><StatusBadge>Financial features coming later</StatusBadge></Card>}
    <Card><h2>Account details</h2><dl className="account-details">{user.displayName && <div><dt>Display name</dt><dd>{user.displayName}</dd></div>}<div><dt>Email address</dt><dd>{user.email}</dd></div><div><dt>Email status</dt><dd>{user.isEmailVerified ? "✓ Verified" : "Verification pending"}</dd></div><div><dt>Account role</dt><dd>{admin ? "Administrator" : "Customer"}</dd></div></dl></Card>
  </>;
}
