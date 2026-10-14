"use client";
import { useState, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode } from "react";

export function Button({ busy, children, className = "", disabled, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { busy?: boolean }) {
  return <button className={`button ${className}`} disabled={disabled || busy} aria-busy={busy || undefined} {...props}>{busy && <span className="spinner" aria-hidden="true" />}{children}</button>;
}
export function Input(props: InputHTMLAttributes<HTMLInputElement>) { return <input {...props} className={`input ${props.className ?? ""}`} />; }
export function PasswordInput(props: InputHTMLAttributes<HTMLInputElement>) {
  const [visible, setVisible] = useState(false);
  return <div className="password-control"><Input {...props} type={visible ? "text" : "password"} /><button type="button" className="password-toggle" aria-label={`${visible ? "Hide" : "Show"} ${props.id === "confirmPassword" ? "confirmation password" : "password"}`} aria-pressed={visible} onClick={() => setVisible(!visible)}>{visible ? "Hide" : "Show"}</button></div>;
}
export function FormField({ id, label, hint, error, children }: { id: string; label: string; hint?: string; error?: string; children: ReactNode }) {
  return <div className="form-field"><label htmlFor={id}>{label}</label>{children}{hint && <p className="field-hint" id={`${id}-hint`}>{hint}</p>}{error && <p className="field-error" id={`${id}-error`}>{error}</p>}</div>;
}
export function Alert({ children, tone = "error" }: { children: ReactNode; tone?: "error" | "success" | "info" }) {
  return <div className={`alert alert-${tone}`} role={tone === "error" ? "alert" : "status"}>{children}</div>;
}
export function Card({ children, className = "" }: { children: ReactNode; className?: string }) { return <section className={`card ${className}`}>{children}</section>; }
export function StatusBadge({ children }: { children: ReactNode }) { return <span className="badge">{children}</span>; }
export function LoadingState({ text = "Checking your session…" }: { text?: string }) { return <div className="loading-state" role="status"><span className="spinner" aria-hidden="true" />{text}</div>; }
