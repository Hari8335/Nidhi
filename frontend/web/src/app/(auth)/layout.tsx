import Link from "next/link";
import { Brand } from "@/components/shared/brand";
import { Card } from "@/components/ui/primitives";
import { AuthSavingsMark } from "@/components/shared/gold-savings-visual";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="auth-split-layout">
      {/* Left Column: Branded Showcase (Desktop) */}
      <aside className="auth-showcase-pane" aria-hidden="true">
        <div className="auth-showcase-content">
          <div className="auth-brand-header">
            <Brand />
            <Link href="/" className="auth-back-link">
              ← Return to overview
            </Link>
          </div>

          <div className="auth-showcase-body">
            <div className="auth-eyebrow-pill">DISCIPLINED MICRO-SAVINGS</div>
            <h2 className="auth-showcase-title">
              Small steps forward.<br />
              Consistent saving habits.
            </h2>
            <p className="auth-showcase-desc">
              Save small LKR amounts into simulated fractional gold.
              Track your simulated holdings, transactions, and savings goals in an educational environment.
            </p>

            <div className="auth-visual-embed">
              <AuthSavingsMark />
            </div>

            <div className="auth-benefits-list">
              <div className="benefit-card">
                <span className="benefit-icon">✦</span>
                <div className="benefit-text">
                  <strong>Simulated Fractional Gold</strong>
                  <p>Save from as little as LKR 100 with 8-decimal precision against the current simulated price.</p>
                </div>
              </div>

              <div className="benefit-card">
                <span className="benefit-icon">✦</span>
                <div className="benefit-text">
                  <strong>Habit & Goal Tracking</strong>
                  <p>Monitor wallet balances, simulated gold holdings, receipts, and personal savings goals.</p>
                </div>
              </div>
            </div>
          </div>

          <div className="auth-showcase-footer">
            <span className="sim-indicator-dot" />
            <span>Simulated micro-savings platform · No real money or gold custody</span>
          </div>
        </div>
      </aside>

      {/* Right Column: Form Pane */}
      <div className="auth-form-pane">
        {/* Mobile Header */}
        <header className="public-header auth-mobile-header">
          <Brand />
          <Link href="/" className="small">Back to home ↗</Link>
        </header>

        <main id="main-content" className="auth-main">
          <Card className="auth-card">
            {children}
          </Card>
          <p className="simulation-note">
            Small steps. A thoughtful start.<br />
            Simulation only — no real money or gold custody.
          </p>
        </main>
      </div>
    </div>
  );
}
