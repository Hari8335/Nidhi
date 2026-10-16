"use client";
import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api/client";
import { Alert, Button, FormField, Input } from "@/components/ui/primitives";
import type { MessageResponse } from "@/types/auth";
import { useAuth } from "./auth-provider";
import { useEmailLink } from "./use-email-link";

export function EmailVerificationForm() {
  const link = useEmailLink(true);
  const { refresh } = useAuth();
  const lock = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [verified, setVerified] = useState(false);
  const [seconds, setSeconds] = useState(0);
  useEffect(() => {
    if (!seconds) return;
    const timer = window.setTimeout(() => setSeconds(seconds - 1), 1000);
    return () => window.clearTimeout(timer);
  }, [seconds]);
  async function verify() {
    if (!link || lock.current) return;
    lock.current = true; setBusy(true); setError("");
    try {
      await api.post<MessageResponse>("/auth/verify-email", link);
      await refresh(); setVerified(true);
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : "Unable to verify your email. Please try again.");
    } finally { lock.current = false; setBusy(false); }
  }
  async function resend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (lock.current || seconds) return;
    const email = String(new FormData(event.currentTarget).get("email") ?? "").trim();
    lock.current = true; setBusy(true); setError(""); setNotice("");
    try {
      const result = await api.post<MessageResponse>("/auth/resend-verification", { email });
      setNotice(result.message); setSeconds(60);
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : "Unable to request an email. Please try again.");
    } finally { lock.current = false; setBusy(false); }
  }
  return <>
    <div className="auth-heading"><p className="eyebrow">YOUR NIDHI ACCOUNT</p><h1>{verified ? "Email verified" : "Verify your email"}</h1><p>{verified ? "Your account is ready. Sign in to continue." : link ? "Confirm your email to continue your savings journey." : "Open the Verify email link in your inbox to finish setting up your account."}</p></div>
    {error && <Alert>{error}</Alert>}
    {verified ? <Link href="/login" className="button">Continue to sign in</Link> : <>
      {link && <Button type="button" busy={busy} onClick={verify}>{busy ? "Please wait…" : "Verify email"}</Button>}
      <form onSubmit={resend} className="auth-form mt-6 grid gap-4" aria-busy={busy}>
        <h2>Need a new email?</h2><p className="muted">If your link expired or never arrived, request another below. Check your spam folder too.</p>
        {notice && <Alert tone="success">{notice}</Alert>}
        <FormField id="email" label="Email address"><Input id="email" name="email" type="email" required maxLength={256} autoComplete="email" /></FormField>
        <Button type="submit" disabled={seconds > 0} busy={busy}>{seconds ? `Resend available in ${seconds}s` : "Resend verification email"}</Button>
      </form>
      <p className="small muted mt-6">Already verified? <Link href="/login">Sign in</Link></p>
    </>}
  </>;
}
