"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { useAuth } from "@/components/auth/auth-provider";
import { Alert, Button, Card, LoadingState, StatusBadge } from "@/components/ui/primitives";
import { WalletFundingModal } from "@/components/wallet/wallet-funding-modal";
import { api, ApiError } from "@/lib/api/client";
import { formatLkr, WALLET_LIMITS } from "@/lib/validation/wallet";
import type { WalletSummary } from "@/types/wallet";

export function WalletView() {
  const { session } = useAuth();
  const [wallet, setWallet] = useState<WalletSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [fundingModalOpen, setFundingModalOpen] = useState(false);

  const fetchWallet = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.get<WalletSummary>("/customer/wallet");
      setWallet(res);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Unable to load your wallet data. Please check your connection and retry.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (session.status === "authenticated") {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      void fetchWallet();
    }
  }, [session.status, fetchWallet]);

  if (session.status !== "authenticated") return null;
  const user = session.user;

  function handleFundingSuccess() {
    void fetchWallet();
  }

  const balance = wallet ? wallet.balanceLkr : 0;
  const cap = wallet ? wallet.maxBalanceCapLkr : WALLET_LIMITS.MAX_BALANCE_CAP_LKR;
  const capPercentage = Math.min(100, (balance / cap) * 100);
  const remainingToday = wallet ? wallet.remainingFundingOperationsToday : WALLET_LIMITS.MAX_DAILY_OPERATIONS;
  const usedToday = wallet ? wallet.successfulFundingCountToday : 0;

  return (
    <div className="wallet-root">
      <div className="page-heading">
        <p className="eyebrow">YOUR FINANCES</p>
        <h1>Simulated Wallet</h1>
        <p>Manage your simulated LKR balance for testing gold micro-savings.</p>
      </div>

      {!user.isEmailVerified && (
        <Alert tone="info">
          Your email is not verified yet.{" "}
          <Link href="/verify-email" className="font-semibold underline">
            Verify your email
          </Link>{" "}
          to enable simulated wallet funding operations.
        </Alert>
      )}

      {loading ? (
        <LoadingState text="Loading your wallet details…" />
      ) : error ? (
        <Card className="error-card">
          <Alert tone="error">{error}</Alert>
          <div className="mt-4">
            <Button className="button-secondary" onClick={() => void fetchWallet()}>
              Retry loading wallet
            </Button>
          </div>
        </Card>
      ) : wallet ? (
        <>
          {/* Wallet Balance Hero Card */}
          <section className="wallet-hero-card">
            <div className="wallet-hero-header">
              <div>
                <span className="wallet-hero-label">Available Balance</span>
                <div className="wallet-balance-row">
                  <span className="wallet-balance-amount">{formatLkr(wallet.balanceLkr)}</span>
                  <span className="badge">Simulated LKR</span>
                </div>
              </div>
              <Button
                className="button-primary"
                onClick={() => setFundingModalOpen(true)}
              >
                + Add Simulated Funds
              </Button>
            </div>

            {/* Cap Progress */}
            <div className="wallet-cap-section">
              <div className="cap-meta-row">
                <span>Wallet Balance Cap</span>
                <span className="font-semibold">
                  {formatLkr(balance)} / {formatLkr(cap)} ({capPercentage.toFixed(1)}%)
                </span>
              </div>
              <div className="progress-bar-track">
                <div
                  className="progress-bar-fill"
                  style={{ width: `${capPercentage}%` }}
                />
              </div>
            </div>

            {/* Daily limit badge row */}
            <div className="daily-limits-row">
              <div className="limit-pill">
                <span className="limit-pill-dot" />
                <span>
                  <strong>{remainingToday}</strong> of {wallet.maxDailyFundingOperations} fundings remaining today
                </span>
              </div>
              <div className="limit-pill">
                <span>Per transaction: <strong>LKR 100.00 – LKR 1,000,000.00</strong></span>
              </div>
            </div>
          </section>

          {/* Guidelines & Rules Grid */}
          <div className="wallet-cards-grid">
            <Card className="simulation-disclosure-card">
              <p className="eyebrow">SIMULATION DISCLOSURE</p>
              <h3>Purely Educational & Simulated</h3>
              <p>
                Nidhi is currently in a prototype / simulation phase. Adding simulated funds does
                <strong> not charge any bank card or fiat account</strong>, and balances cannot be
                withdrawn for real money. Every operation creates an auditable double-entry ledger record
                mirroring real financial engineering principles.
              </p>
              <div className="mt-4">
                <StatusBadge>Double-Entry Ledger Verified</StatusBadge>
              </div>
            </Card>

            <Card className="limits-card">
              <p className="eyebrow">APPROVED POLICIES</p>
              <h3>Operational Limits</h3>
              <dl className="limits-list">
                <div>
                  <dt>Minimum per operation</dt>
                  <dd>LKR 100.00</dd>
                </div>
                <div>
                  <dt>Maximum per operation</dt>
                  <dd>LKR 1,000,000.00</dd>
                </div>
                <div>
                  <dt>Maximum wallet balance</dt>
                  <dd>LKR 5,000,000.00</dd>
                </div>
                <div>
                  <dt>Daily operation cap</dt>
                  <dd>20 successful operations / UTC day</dd>
                </div>
                <div>
                  <dt>Operations today</dt>
                  <dd>{usedToday} completed</dd>
                </div>
              </dl>
            </Card>
          </div>
        </>
      ) : null}
      {wallet && (
          <WalletFundingModal
            isOpen={fundingModalOpen}
            onClose={() => setFundingModalOpen(false)}
            onSuccess={handleFundingSuccess}
            currentBalance={wallet.balanceLkr}
            walletSummary={wallet}
            isEmailVerified={user.isEmailVerified}
          />
      )}
    </div>
  );
}
