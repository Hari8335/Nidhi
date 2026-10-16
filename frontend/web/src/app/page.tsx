import type { Metadata } from "next";
import Link from "next/link";
import { Brand } from "@/components/shared/brand";
import { PageContainer } from "@/components/layout/page-container";
import { GoldSavingsVisual } from "@/components/shared/gold-savings-visual";

export const metadata: Metadata = {
  title: "Nidhi — Simulated Gold Micro-Savings",
  description: "Build a saving habit with simulated LKR and fractional gold. Track simulated holdings, transactions and savings goals in one clear experience.",
  openGraph: {
    type: "website", siteName: "Nidhi", title: "Nidhi — Simulated Gold Micro-Savings",
    description: "A simulated micro-savings experience. Allocate LKR toward fractional gold and follow your savings progress.",
  },
  twitter: {
    card: "summary", title: "Nidhi — Simulated Gold Micro-Savings",
    description: "Explore simulated fractional gold saving, clear transaction records and goals measured in grams.",
  },
};

export default function Home() {
  return (
    <div className="landing-page">
      <PageContainer>
        <header className="public-header">
          <Brand />
          <nav className="landing-nav" aria-label="Main navigation">
            <a href="#how-it-works" className="nav-link">How it works</a>
            <a href="#features" className="nav-link">Features</a>
            <a href="#simulation" className="nav-link">Simulation</a>
          </nav>
          <div className="header-actions">
            <Link href="/login" className="sign-in-link">Sign in</Link>
            <Link href="/register" className="button button-header">Create account</Link>
          </div>
        </header>

        <main id="main-content" tabIndex={-1}>
          <section className="hero-section" aria-labelledby="hero-heading">
            <div className="hero-grid">
              <div className="hero-copy">
                <p className="hero-badge"><span className="badge-pulse" aria-hidden="true" />Simulated gold micro-savings</p>
                <h1 id="hero-heading" className="hero-title">Build a saving habit, measured in gold.</h1>
                <p className="hero-lead">
                  Start small with simulated LKR. Allocate your wallet balance to simulated
                  fractional gold, then follow your holdings and savings goals in grams.
                </p>
                <div className="hero-actions">
                  <Link className="button button-hero" href="/register">Create account</Link>
                  <Link className="button button-secondary button-hero-secondary" href="/login">Sign in</Link>
                </div>
                <p className="hero-disclosure">A simulation from start to finish. Wallet balances, gold purchases and holdings are simulated. No real money or physical gold is handled.</p>
                <div className="hero-metrics" aria-label="Saving essentials">
                  <div className="metric-box"><span className="metric-num">LKR 100</span><span className="metric-sub">Minimum simulated purchase</span></div>
                  <div className="metric-box"><span className="metric-num">Measured in grams</span><span className="metric-sub">Holdings & goal progress</span></div>
                </div>
              </div>
              <div className="hero-visual-column"><GoldSavingsVisual /></div>
            </div>
          </section>

          <section id="how-it-works" className="feature-section" aria-labelledby="how-heading">
            <div className="section-header">
              <p className="eyebrow">One step at a time</p>
              <h2 id="how-heading">How Nidhi works</h2>
              <p className="section-subtitle">Three simple steps, with you in control of each action.</p>
            </div>
            <ol className="steps-grid">
              <li className="step-card"><span className="step-number" aria-hidden="true">01</span><h3>Add simulated LKR</h3><p>Add simulated funds to your wallet, starting at LKR 100. No payment or bank transfer is involved.</p></li>
              <li className="step-card"><span className="step-number" aria-hidden="true">02</span><h3>Allocate to simulated gold</h3><p>Choose an amount from your wallet. Review the displayed simulated price and fractional gold quantity before confirming.</p></li>
              <li className="step-card"><span className="step-number" aria-hidden="true">03</span><h3>Follow your progress</h3><p>View your simulated holding, revisit transactions and track a savings goal measured in grams.</p></li>
            </ol>
          </section>

          <section id="features" className="why-section" aria-labelledby="features-heading">
            <div className="why-grid">
              <div className="why-copy">
                <p className="eyebrow">A clear view of every step</p>
                <h2 id="features-heading">Small amounts. A complete saving story.</h2>
                <p className="why-description">Explore the fractional gold saving workflow with a wallet, holdings and records that stay connected.</p>
                <ul className="principles-list">
                  <li><span className="check-icon" aria-hidden="true">✓</span><div><h3>Simulated wallet & gold purchases</h3><p>See your available simulated LKR and decide how much to allocate.</p></div></li>
                  <li><span className="check-icon" aria-hidden="true">✓</span><div><h3>Gold holdings, measured precisely</h3><p>Track simulated quantities to eight decimal places and an indicative simulated LKR value.</p></div></li>
                  <li><span className="check-icon" aria-hidden="true">✓</span><div><h3>Transaction history & clear receipts</h3><p>Revisit funding and purchases, including the amount, applied simulated price and gold quantity on each purchase receipt.</p></div></li>
                </ul>
              </div>
              <aside className="philosophy-card goal-story" aria-labelledby="goal-heading">
                <p className="eyebrow">Savings goals</p>
                <h3 id="goal-heading">Give your progress a direction.</h3>
                <p>Set a simulated gold target in grams, with an optional target date. Your current total simulated holding shows how far you have come.</p>
                <div className="goal-story-track" aria-hidden="true"><span /><span /><span /></div>
                <p className="small muted">A goal tracks your total holding. It does not reserve gold or make automatic purchases. You choose each saving step.</p>
              </aside>
            </div>
          </section>

          <section id="simulation" className="simulation-section" aria-labelledby="sim-heading">
            <div className="simulation-box">
              <p className="simulation-badge">Transparent by design</p>
              <h2 id="sim-heading" className="simulation-title">A clear simulation. Deliberate decisions.</h2>
              <p className="simulation-text">All wallet balances, gold purchases, holdings and prices in Nidhi are simulated. No real money or physical gold is handled, and simulated holdings have no real cash value.</p>
              <ul className="transparency-list">
                <li><h3>Review before confirming</h3><p>See the amount, simulated price and gold quantity before you make a purchase.</p></li>
                <li><h3>A changed price needs your approval</h3><p>If the simulated price changes, review and confirm the updated purchase.</p></li>
                <li><h3>A record you can revisit</h3><p>Purchase receipts retain the original applied price and quantity so each step remains clear.</p></li>
              </ul>
            </div>
          </section>

          <section className="final-cta" aria-labelledby="start-heading">
            <div><p className="eyebrow">Your next small step</p><h2 id="start-heading">Explore a more intentional saving habit.</h2><p className="muted">Create an account to try simulated gold micro-savings.</p></div>
            <div className="hero-actions"><Link href="/register" className="button">Create your account</Link><Link href="/login" className="button button-secondary">Sign in</Link></div>
          </section>
        </main>

        <footer className="landing-footer">
          <div className="footer-top"><Brand /><nav className="footer-links" aria-label="Footer navigation"><a href="#how-it-works">How it works</a><a href="#simulation">Simulation</a><Link href="/login">Sign in</Link><Link href="/register">Create account</Link></nav></div>
          <div className="footer-bottom"><p>© {new Date().getFullYear()} Nidhi</p><p>Simulated gold micro-savings. No real money or physical gold.</p></div>
        </footer>
      </PageContainer>
    </div>
  );
}
