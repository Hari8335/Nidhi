"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState, type FormEvent } from "react";
import { Alert, Button, FormField, Input, PasswordInput } from "@/components/ui/primitives";
import { api, ApiError } from "@/lib/api/client";
import { roleHome } from "@/lib/auth/routing";
import { confirmationError, passwordError, passwordGuidance } from "@/lib/validation/auth";
import { useAuth } from "./auth-provider";
import type { LoginResponse, MessageResponse, RegistrationResponse } from "@/types/auth";

type Mode = "login" | "register" | "verify-email" | "forgot-password" | "reset-password";
const copy: Record<Mode, { title: string; intro: string; action: string }> = {
  login: { title: "Welcome back", intro: "Sign in to your Nidhi account.", action: "Sign in" },
  register: { title: "Start with a small step", intro: "Create your account. Build your saving habit.", action: "Create account" },
  "verify-email": { title: "Verify your email", intro: "Use the account ID and verification code from your verification message.", action: "Verify email" },
  "forgot-password": { title: "Forgot your password?", intro: "Enter your email and we’ll help you get back in.", action: "Send reset instructions" },
  "reset-password": { title: "Choose a new password", intro: "Use the account ID and reset code from your password reset message.", action: "Reset password" },
};
export function AuthForm({ mode }: { mode: Mode }) {
  const router = useRouter();
  const { refresh, clear } = useAuth();
  const lock = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [fields, setFields] = useState<Record<string, string>>({});
  const [success, setSuccess] = useState("");
  const tokenFlow = mode === "verify-email" || mode === "reset-password";
  const newPassword = mode === "register" || mode === "reset-password";
  const hasPassword = newPassword || mode === "login";
  function described(id: string, hint?: string) { return [hint && `${id}-hint`, fields[id] && `${id}-error`].filter(Boolean).join(" ") || undefined; }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (lock.current) return;
    const form = event.currentTarget;
    const data = new FormData(form);
    const value = (name: string) => String(data.get(name) ?? "");
    const validation: Record<string, string> = {};
    if (newPassword) { const invalid = passwordError(value("password")); if (invalid) validation.password = invalid; }
    if (mode === "reset-password") { const invalid = confirmationError(value("password"), value("confirmPassword")); if (invalid) validation.confirmPassword = invalid; }
    setFields(validation); setError("");
    if (Object.keys(validation).length) {
      setError("Please check the highlighted fields.");
      form.querySelector<HTMLInputElement>(`[name="${Object.keys(validation)[0]}"]`)?.focus();
      return;
    }
    lock.current = true; setBusy(true);
    try {
      if (mode === "login") {
        const result = await api.post<LoginResponse>("/auth/login", { email: value("email").trim(), password: value("password") });
        form.reset();
        const user = await refresh();
        const destination = user ? roleHome(user.roles) : roleHome([result.role]);
        if (!destination) throw new Error("unsupported-role");
        router.replace(destination);
      } else if (mode === "register") {
        await api.post<RegistrationResponse>("/auth/register", { email: value("email").trim(), password: value("password"), displayName: value("displayName").trim() || null });
        form.reset(); setSuccess("Your account is ready. Check your verification message before using future simulated financial features.");
      } else if (mode === "forgot-password") {
        await api.post<MessageResponse>("/auth/forgot-password", { email: value("email").trim() });
        form.reset(); setSuccess("If an account exists for this email, password reset instructions have been sent.");
      } else if (mode === "verify-email") {
        await api.post<MessageResponse>("/auth/verify-email", { userId: value("userId").trim(), token: value("token").trim() });
        form.reset(); await refresh(); setSuccess("Email verified. You’re ready to sign in.");
      } else {
        await api.post<MessageResponse>("/auth/reset-password", { userId: value("userId").trim(), token: value("token").trim(), newPassword: value("password") });
        form.reset(); clear(); setSuccess("Your password has been reset. Sign in with your new password.");
      }
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : "We could not complete your request. Please try again.");
      if (failure instanceof ApiError && failure.problem.code === "VALIDATION_ERROR") {
        const mapped: Record<string, string> = {};
        for (const [key, messages] of Object.entries(failure.problem.errors ?? {})) {
          const field = key.replace(/^\$\./, "").toLowerCase();
          const name = ({ email: "email", password: "password", newpassword: "password", displayname: "displayName", userid: "userId", token: "token" } as Record<string, string>)[field];
          if (name) mapped[name] = messages.join(" ");
        }
        setFields(mapped);
      }
      if (failure instanceof ApiError && failure.problem.code === "EMAIL_ALREADY_EXISTS") setFields({ email: "This email is already registered." });
    } finally { lock.current = false; setBusy(false); }
  }
  return <>
    <div className="auth-heading"><p className="eyebrow">YOUR NIDHI ACCOUNT</p><h1>{copy[mode].title}</h1><p>{copy[mode].intro}</p></div>
    {success ? <div className="success-state"><span className="success-ring" aria-hidden="true">✓</span><Alert tone="success">{success}</Alert>
      {mode === "register" ? <Link className="button" href="/verify-email">Continue to verification</Link> : mode === "forgot-password" ? <Link className="button" href="/reset-password">I have a reset code</Link> : <Link className="button" href="/login">Continue to sign in</Link>}
      {(mode === "register" || mode === "forgot-password") && <p className="small muted"><Link href="/login">Back to sign in</Link></p>}
    </div> : <form onSubmit={submit} className="auth-form" aria-busy={busy}>
      {error && <Alert>{error}</Alert>}
      {mode === "register" && <FormField id="displayName" label="Display name (optional)" error={fields.displayName}><Input id="displayName" name="displayName" autoComplete="nickname" maxLength={100} aria-invalid={!!fields.displayName} aria-describedby={described("displayName")} /></FormField>}
      {!tokenFlow && <FormField id="email" label="Email address" error={fields.email}><Input id="email" name="email" type="email" autoComplete="email" required maxLength={256} aria-invalid={!!fields.email} aria-describedby={described("email")} /></FormField>}
      {tokenFlow && <>
        <FormField id="userId" label="Account ID" hint="The userId supplied with your code." error={fields.userId}><Input id="userId" name="userId" required autoComplete="off" spellCheck={false} maxLength={36} pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" aria-invalid={!!fields.userId} aria-describedby={described("userId", "hint")} /></FormField>
        <FormField id="token" label={mode === "verify-email" ? "Verification code" : "Reset code"} hint="Codes expire after one hour and can only be used once." error={fields.token}><Input id="token" name="token" type="password" required maxLength={4096} autoComplete="off" spellCheck={false} aria-invalid={!!fields.token} aria-describedby={described("token", "hint")} /></FormField>
        {process.env.NODE_ENV === "development" && <p className="development-note">Local development: copy userId and token from the private email pickup JSON file. Never share that file or your code.</p>}
      </>}
      {hasPassword && <FormField id="password" label={mode === "reset-password" ? "New password" : "Password"} hint={newPassword ? passwordGuidance : undefined} error={fields.password}><PasswordInput id="password" name="password" required minLength={newPassword ? 12 : undefined} maxLength={128} autoComplete={newPassword ? "new-password" : "current-password"} aria-invalid={!!fields.password} aria-describedby={described("password", newPassword ? "hint" : undefined)} /></FormField>}
      {mode === "reset-password" && <FormField id="confirmPassword" label="Confirm new password" error={fields.confirmPassword}><PasswordInput id="confirmPassword" name="confirmPassword" required maxLength={128} autoComplete="new-password" aria-invalid={!!fields.confirmPassword} aria-describedby={described("confirmPassword")} /></FormField>}
      {mode === "login" && <Link className="forgot-link small" href="/forgot-password">Forgot password?</Link>}
      <Button type="submit" busy={busy}>{busy ? "Please wait…" : copy[mode].action}</Button>
      <div className="auth-links">{mode === "login" ? <p>New to Nidhi? <Link href="/register">Create an account</Link></p> : <p><Link href="/login">Back to sign in</Link></p>}{mode === "login" && <Link href="/verify-email">Verify your email</Link>}{mode === "forgot-password" && <Link href="/reset-password">Already have a reset code?</Link>}</div>
    </form>}
  </>;
}
