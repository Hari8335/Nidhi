import Link from "next/link";
import { Brand } from "@/components/shared/brand";
import { PageContainer } from "@/components/layout/page-container";
import { GoldSavingsVisual } from "@/components/shared/gold-savings-visual";

export default function Home() {
  return (
    <div className="landing-page">
      <PageContainer>
        <header className="public-header">
          <Brand />
          <nav className="landing-nav" aria-label="Main Navigation">
            <a href="#how-it-works" className="nav-link">How it works</a>
            <a href="#why-gold" className="nav-link">Why gold saving</a>
            <a href="#simulation" className="nav-link">Simulation</a>
          </nav>
          <div className="header-actions">
            <Link href="/login" className="sign-in-link">Sign in ↗</Link>
            <Link href="/register" className="button button-header">Start saving</Link>
          </div>
        </header>

        <main id="main-content">
          {/* Hero Section */}
          <section className="hero-section" aria-labelledby="hero-heading">
            <div className="hero-grid">
              <div className="hero-copy">
                <div className="hero-badge">
                  <span className="badge-pulse" />
                  <span>SIMULATED DIGITAL GOLD MICRO-SAVINGS</span>
                </div>

                <h1 id="hero-heading" className="hero-title">
                  Build a saving habit, measured in gold.
                </h1>

                <p className="hero-lead">
                  Nidhi lets you save small LKR amounts as simulated fractional gold.
                  Build a consistent savings habit with transparent pricing,
                  simulated rates, and clear progress tracking.
                </p>

                <div className="hero-actions">
                  <Link className="button button-hero" href="/register">
                    Create an account
                  </Link>
                  <Link className="button button-secondary button-hero-secondary" href="/login">
                    Sign in
                  </Link>
                </div>

                {/* Proof Metrics */}
                <div className="hero-metrics">
                  <div className="metric-box">
                    <span className="metric-num">LKR 100</span>
                    <span className="metric-sub">Minimum simulated save</span>
                  </div>
                  <div className="metric-divider" />
                  <div className="metric-box">
                    <span className="metric-num">Fractional</span>
                    <span className="metric-sub">8-decimal precision</span>
                  </div>
                  <div className="metric-divider" />
                  <div className="metric-box">
                    <span className="metric-num">Goal tracking</span>
                    <span className="metric-sub">Progress & receipts</span>
                  </div>
                </div>
              </div>

              <div className="hero-visual-column">
                <GoldSavingsVisual />
              </div>
            </div>
          </section>

          {/* How It Works Section */}
          <section id="how-it-works" className="feature-section" aria-labelledby="how-heading">
            <div className="section-header">
              <p className="eyebrow">SAVINGS ARCHITECTURE</p>
              <h2 id="how-heading">How digital gold micro-savings works</h2>
              <p className="section-subtitle">
                A disciplined habit designed for everyday stability, not speculative trading.
              </p>
            </div>

            <div className="steps-grid">
              <div className="step-card">
                <div className="step-number">01</div>
                <h3>Add simulated funds</h3>
                <p>
                  Start from as little as LKR 100 into your simulated wallet,
                  building your saving habit one step at a time.
                </p>
              </div>

              <div className="step-card">
                <div className="step-number">02</div>
                <h3>Save into simulated gold</h3>
                <p>
                  Choose when to allocate wallet funds to simulated fractional gold
                  at the current displayed simulated price with 8-decimal precision.
                </p>
              </div>

              <div className="step-card">
                <div className="step-number">03</div>
                <h3>Track holdings and goals</h3>
                <p>
                  Monitor your simulated gold quantity, indicative portfolio value,
                  auditable transaction history, and savings goal progress.
                </p>
              </div>
            </div>
          </section>

          {/* Why Gold / Principles Section */}
          <section id="why-gold" className="why-section" aria-labelledby="why-heading">
            <div className="why-grid">
              <div className="why-copy">
                <p className="eyebrow">THE NIDHI DIFFERENCE</p>
                <h2 id="why-heading">Disciplined micro-savings. Clear simulated tracking.</h2>
                <p className="why-description">
                  We believe true financial calm comes from disciplined, repeated micro-habits.
                  No trading charts, no margin calls, no speculative noise.
                  Focused on steady saving discipline and transparent simulated gold tracking.
                </p>
                <ul className="principles-list">
                  <li>
                    <span className="check-icon">✓</span>
                    <div>
                      <strong>Transparent simulated valuation</strong>
                      <p>View your holdings in exact simulated gold grams and indicative LKR value.</p>
                    </div>
                  </li>
                  <li>
                    <span className="check-icon">✓</span>
                    <div>
                      <strong>Clear savings goals</strong>
                      <p>Set a personal gold target and track your accumulation progress toward it.</p>
                    </div>
                  </li>
                  <li>
                    <span className="check-icon">✓</span>
                    <div>
                      <strong>Audit-grade ledger precision</strong>
                      <p>Every simulated transaction and wallet operation is immutably recorded.</p>
                    </div>
                  </li>
                </ul>
              </div>

              <div className="why-card-container">
                <div className="philosophy-card">
                  <div className="philosophy-header">
                    <span className="philosophy-tag">SAVING HABIT</span>
                    <span className="philosophy-num">LKR 100</span>
                  </div>
                  <blockquote className="philosophy-quote">
                    &ldquo;A saving habit starts with small amounts. Consistent tracking and clear goals build lasting financial discipline.&rdquo;
                  </blockquote>
                  <div className="philosophy-footer">
                    <div className="gold-bar-graphic" />
                    <span>Nidhi Micro-Savings Platform</span>
                  </div>
                </div>
              </div>
            </div>
          </section>

          {/* Simulation Disclaimer Section */}
          <section id="simulation" className="simulation-section" aria-labelledby="sim-heading">
            <div className="simulation-box">
              <div className="simulation-badge">SIMULATION NOTICE</div>
              <h2 id="sim-heading" className="simulation-title">Educational & Portfolio Simulation</h2>
              <p className="simulation-text">
                Nidhi is a portfolio software engineering demonstration and micro-savings simulation.
                All deposits, gold balances, transactions, and rates are strictly simulated.
                No real money, physical gold custody, or financial advice is provided.
              </p>
              <div className="simulation-footer">
                <Link href="/register" className="button button-small">Explore demo account</Link>
                <Link href="/login" className="nav-link">Access existing account →</Link>
              </div>
            </div>
          </section>
        </main>

        <footer className="landing-footer">
          <div className="footer-top">
            <Brand />
            <div className="footer-links">
              <Link href="/login">Sign in</Link>
              <Link href="/register">Create account</Link>
              <Link href="/verify-email">Verify email</Link>
              <Link href="/forgot-password">Password recovery</Link>
            </div>
          </div>
          <div className="footer-bottom">
            <p>© {new Date().getFullYear()} Nidhi Platform. Simulated Digital Gold Micro-Savings.</p>
            <p className="footer-fine">Built with precision. Strict transactional integrity.</p>
          </div>
        </footer>
      </PageContainer>
    </div>
  );
}
