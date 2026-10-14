"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { useAuth } from "@/components/auth/auth-provider";
import { Alert, Button, Card, LoadingState, StatusBadge } from "@/components/ui/primitives";
import { WalletFundingModal } from "@/components/wallet/wallet-funding-modal";
import { api, ApiError } from "@/lib/api/client";
import { formatLkr } from "@/lib/validation/wallet";
import type { DashboardData } from "@/types/wallet";

export function CustomerDashboard() {
  const { session } = useAuth();
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [fundingModalOpen, setFundingModalOpen] = useState(false);

  const fetchDashboard = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.get<DashboardData>("/customer/dashboard");
      setData(res);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Unable to load your dashboard data. Please check your connection and retry.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (session.status === "authenticated") {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      void fetchDashboard();
    }
  }, [session.status, fetchDashboard]);

  if (session.status !== "authenticated") return null;
  const user = session.user;

  function handleFundingSuccess() {
    // Re-fetch dashboard data to sync balance and transaction history
    void fetchDashboard();
  }

  return (
    <div className="dashboard-root">
      <div className="page-heading">
        <p className="eyebrow">YOUR SAVING SPACE</p>
        <h1>Welcome{user.displayName ? `, ${user.displayName}` : ""}</h1>
        <p>A considered, steady path to micro-savings in gold.</p>
      </div>

      {!user.isEmailVerified && (
        <Alert tone="info">
          Your email is not verified yet.{" "}
          <Link href="/verify-email" className="font-semibold underline">
            Verify your email
          </Link>{" "}
          to unlock simulated wallet funding and gold saving.
        </Alert>
      )}

      {loading ? (
        <LoadingState text="Loading your savings space…" />
      ) : error ? (
        <Card className="error-card">
          <Alert tone="error">{error}</Alert>
          <div className="mt-4">
            <Button className="button-secondary" onClick={() => void fetchDashboard()}>
              Retry loading data
            </Button>
          </div>
        </Card>
      ) : data ? (
        <>
          {/* Main Hero Summary Card */}
          <section className="dashboard-hero-card">
            <div className="hero-metrics-grid">
              {/* Gold Holdings */}
              <div className="metric-box gold-metric">
                <div className="metric-header">
                  <span className="metric-label">Simulated Gold Holdings</span>
                  <StatusBadge>24K 99.9%</StatusBadge>
                </div>
                <div className="metric-value-row">
                  <span className="metric-large-number">
                    {data.goldHoldingGrams.toFixed(8)}
                  </span>
                  <span className="metric-unit">grams</span>
                </div>
                <p className="metric-subtext">Estimated value: LKR 0.00</p>
              </div>

              {/* Wallet Balance */}
              <div className="metric-box wallet-metric">
                <div className="metric-header">
                  <span className="metric-label">Simulated Wallet Balance</span>
                  <span className="simulation-tag">Simulated LKR</span>
                </div>
                <div className="metric-value-row">
                  <span className="metric-large-number">
                    {formatLkr(data.walletBalanceLkr)}
                  </span>
                </div>
                <p className="metric-subtext">Available for simulated gold purchases</p>
              </div>
            </div>

            {/* Quick Actions */}
            <div className="dashboard-cta-row">
              <Button
                className="button-primary"
                onClick={() => setFundingModalOpen(true)}
              >
                + Add Simulated Funds
              </Button>
              <Button
                className="button-secondary"
                disabled
                title="Gold purchasing available in Session 8"
              >
                Buy Gold <small className="soon-badge">Session 8</small>
              </Button>
              <Link href="/wallet" className="view-wallet-link">
                View Wallet Details →
              </Link>
            </div>
          </section>

          {/* Goal Teaser Card */}
          <Card className="goal-teaser-card">
            <div className="goal-header">
              <div>
                <p className="eyebrow">SAVINGS HABIT</p>
                <h2>Start with a gold milestone</h2>
                <p>Consistent, small contributions add up to tangible gold grams over time.</p>
              </div>
              <StatusBadge>Goal tracking soon</StatusBadge>
            </div>
            <div className="goal-meter-placeholder">
              <div className="goal-bar">
                <div className="goal-fill" style={{ width: "0%" }} />
              </div>
              <div className="goal-labels">
                <span>0.0000g accumulated</span>
                <span>Next milestone: 1.0000g</span>
              </div>
            </div>
          </Card>

          {/* Recent Transactions Section */}
          <section className="transactions-section">
            <div className="section-header">
              <h2>Recent Activity</h2>
              <span className="text-muted-foreground text-sm">
                Immutable financial transactions
              </span>
            </div>

            {data.recentTransactions.length === 0 ? (
              <Card className="empty-transactions-card">
                <div className="empty-icon">🪙</div>
                <h3>No simulated transactions yet</h3>
                <p>
                  Add simulated funds to your wallet to kickstart your saving journey and see
                  your audit receipts here.
                </p>
                <Button
                  className="button-primary mt-4"
                  onClick={() => setFundingModalOpen(true)}
                >
                  Add First Funds
                </Button>
              </Card>
            ) : (
              <Card className="transactions-card">
                <div className="transactions-table-wrapper">
                  <table className="transactions-table">
                    <thead>
                      <tr>
                        <th>Operation</th>
                        <th>Amount</th>
                        <th>Post-Balance</th>
                        <th>Date & Time</th>
                        <th>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.recentTransactions.map((tx) => (
                        <tr key={tx.transactionId}>
                          <td>
                            <div className="tx-type-cell">
                              <span className="tx-icon">↓</span>
                              <div>
                                <span className="font-semibold block">
                                  {tx.type === "WALLET_FUNDING" ? "Simulated Funding" : tx.type}
                                </span>
                                <span className="text-xs text-muted-foreground font-mono">
                                  {tx.transactionId.slice(0, 8)}…
                                </span>
                              </div>
                            </div>
                          </td>
                          <td className="tx-amount-cell positive">
                            +{formatLkr(tx.amountLkr)}
                          </td>
                          <td className="tx-balance-cell">
                            {formatLkr(tx.postOperationWalletBalanceLkr)}
                          </td>
                          <td className="tx-date-cell">
                            {new Date(tx.createdAtUtc).toLocaleString()}
                          </td>
                          <td>
                            <StatusBadge>Completed</StatusBadge>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </Card>
            )}
          </section>

          <WalletFundingModal
            isOpen={fundingModalOpen}
            onClose={() => setFundingModalOpen(false)}
            onSuccess={handleFundingSuccess}
            currentBalance={data.walletBalanceLkr}
            isEmailVerified={user.isEmailVerified}
          />
        </>
      ) : null}
    </div>
  );
}
