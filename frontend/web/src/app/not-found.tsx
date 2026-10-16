import Link from "next/link";
import { Brand } from "@/components/shared/brand";
import { PageContainer } from "@/components/layout/page-container";

export default function NotFound() {
  return <PageContainer><header className="public-header"><Brand /></header><main id="main-content" tabIndex={-1} className="not-found-page"><p className="eyebrow">404 · Page not found</p><h1>Let’s find your way back.</h1><p className="muted">This page could not be found. Return to Nidhi or sign in to continue.</p><div className="hero-actions"><Link className="button" href="/">Home</Link><Link className="button button-secondary" href="/login">Sign in</Link></div></main></PageContainer>;
}
