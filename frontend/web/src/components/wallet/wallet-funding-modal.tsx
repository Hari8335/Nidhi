"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { formatDateTime } from "@/lib/format/date";
import { Alert, Button, Card, FormField, Input, StatusBadge } from "@/components/ui/primitives";
import { api, ApiError } from "@/lib/api/client";
import { formatLkr, validateFundingInput, WALLET_LIMITS } from "@/lib/validation/wallet";
import type { FundingReceipt, WalletSummary } from "@/types/wallet";

type WalletFundingModalProps = {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (receipt: FundingReceipt) => void;
  currentBalance?: number;
  walletSummary?: WalletSummary | null;
  isEmailVerified: boolean;
};

const PRESET_AMOUNTS = [500, 1000, 5000, 10000, 50000];

export function WalletFundingModal({
  isOpen,
  onClose,
  onSuccess,
  currentBalance = 0,
  walletSummary,
  isEmailVerified,
}: WalletFundingModalProps) {
  const [amountInput, setAmountInput] = useState("1000");
  const [idempotencyKey, setIdempotencyKey] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [receipt, setReceipt] = useState<FundingReceipt | null>(null);
  const [canRetrySameKey, setCanRetrySameKey] = useState(false);

  const dialog = useRef<HTMLDialogElement>(null);
  const lock = useRef(false);
  const resultHeading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    if (!isOpen) return;
    const element = dialog.current;
    const previous = document.activeElement as HTMLElement | null;
    element?.showModal();
    return () => { element?.close(); if (previous?.isConnected) previous.focus(); else document.querySelector<HTMLElement>("[data-funding-trigger]")?.focus(); };
  }, [isOpen]);
  useEffect(() => { if (receipt) resultHeading.current?.focus(); }, [receipt]);

  // Initialize fresh idempotency key when opening the modal
  useEffect(() => {
    if (isOpen) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setIdempotencyKey(crypto.randomUUID());
      setError(null);
      setReceipt(null);
      setCanRetrySameKey(false);
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const remainingDaily = walletSummary ? walletSummary.remainingFundingOperationsToday : WALLET_LIMITS.MAX_DAILY_OPERATIONS;
  const validation = validateFundingInput(amountInput, currentBalance, remainingDaily);

  const parsedAmount = validation.isValid && validation.amount ? validation.amount : 0;
  const projectedBalance = currentBalance + parsedAmount;

  async function handleFund(useExistingKey = false) {
    if (lock.current) return;
    if (!isEmailVerified) {
      setError("Please verify your email address before adding simulated funds.");
      return;
    }

    if (!validation.isValid || !validation.amount) {
      setError(validation.message ?? "Please enter a valid amount.");
      return;
    }

    const keyToUse = useExistingKey && idempotencyKey ? idempotencyKey : idempotencyKey || crypto.randomUUID();
    if (!useExistingKey) {
      setIdempotencyKey(keyToUse);
    }

    lock.current = true; setBusy(true);
    setError(null);

    try {
      const res = await api.post<FundingReceipt>(
        "/customer/wallet/fundings",
        { amountLkr: validation.amount },
        { "Idempotency-Key": keyToUse }
      );
      setReceipt(res);
      setCanRetrySameKey(false);
      onSuccess(res);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
        // If status indicates uncertain network failure (0) or 5xx, keep the same key for safe retry
        if (err.status === 0 || err.status >= 500) {
          setCanRetrySameKey(true);
        }
      } else {
        setError("An unexpected error occurred. You can safely retry this operation.");
        setCanRetrySameKey(true);
      }
    } finally {
      lock.current = false; setBusy(false);
    }
  }

  function handleReset() {
    setIdempotencyKey(crypto.randomUUID());
    setError(null);
    setReceipt(null);
    setCanRetrySameKey(false);
  }

  return (
    <dialog ref={dialog} className="modal-backdrop" aria-labelledby="funding-modal-title" onCancel={e => { if (lock.current) e.preventDefault(); else onClose(); }} onKeyDown={e => {
      if (e.key !== "Tab") return;
      const controls = Array.from(e.currentTarget.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), a[href]')).filter(el => el.getClientRects().length > 0);
      const first = controls[0], last = controls[controls.length - 1];
      if (!first) { e.preventDefault(); return; }
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    }}>
      <div className="modal-container">
        <Card className="funding-modal-card">
          <div className="modal-header">
            <div>
              <p className="eyebrow">SIMULATED FUNDING</p>
              <h2 id="funding-modal-title" ref={resultHeading} tabIndex={-1}>
                {receipt ? "Funds Added Successfully" : "Add Simulated Funds"}
              </h2>
            </div>
            <button
              type="button"
              className="modal-close-button"
              onClick={onClose}
              disabled={busy}
              aria-label="Close dialog"
            >
              ✕
            </button>
          </div>

          {!isEmailVerified && (
            <Alert tone="info">
              Your email must be verified before simulated financial actions are permitted.{" "}
              <Link href="/verify-email" className="underline font-semibold">
                Verify your email now
              </Link>
            </Alert>
          )}

          {receipt ? (
            <div className="receipt-view">
              <div className="receipt-icon-wrapper">
                <span className="receipt-check-icon">✓</span>
              </div>

              <div className="receipt-amount-display">
                <span className="receipt-plus">+</span>
                <span className="receipt-amount">{formatLkr(receipt.amountLkr)}</span>
                <span className="receipt-tag">Simulated Credit</span>
              </div>

              <dl className="receipt-details">
                <div>
                  <dt>Transaction ID</dt>
                  <dd className="monospace">{receipt.transactionId}</dd>
                </div>
                <div>
                  <dt>Operation</dt>
                  <dd>Simulated Wallet Funding</dd>
                </div>
                <div>
                  <dt>Status</dt>
                  <dd>
                    <StatusBadge>{receipt.status}</StatusBadge>
                  </dd>
                </div>
                <div>
                  <dt>New Wallet Balance</dt>
                  <dd className="font-semibold text-gold">
                    {formatLkr(receipt.postOperationWalletBalanceLkr)}
                  </dd>
                </div>
                <div>
                  <dt>Date & Time</dt>
                  <dd>{formatDateTime(receipt.createdAtUtc)}</dd>
                </div>
              </dl>

              <div className="receipt-actions">
                <Button className="button-primary" onClick={onClose}>
                  Done
                </Button>
                <Button className="button-secondary" onClick={handleReset}>
                  Add More Funds
                </Button>
              </div>
            </div>
          ) : (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void handleFund(canRetrySameKey);
              }}
              className="funding-form"
            >
              <div className="simulation-notice-banner">
                <span className="badge">Simulation Mode</span>
                <p>
                  No real payment or bank transfer takes place. Funds are for testing the gold micro-savings flow.
                </p>
              </div>

              {error && (
                <Alert tone="error">
                  {error}
                  {canRetrySameKey && (
                    <div className="mt-2 text-xs">
                      The outcome is uncertain. Retry this same funding request to recover its receipt safely. Check your transaction history before starting another request.
                    </div>
                  )}
                </Alert>
              )}

              <FormField
                id="funding-amount"
                label="Amount to Add (LKR)"
                hint="Minimum LKR 100.00 · Maximum LKR 1,000,000.00"
                error={amountInput && !validation.isValid ? validation.message : undefined}
              >
                <div className="relative">
                  <Input
                    id="funding-amount"
                    type="text"
                    inputMode="decimal"
                    value={amountInput}
                    onChange={(e) => {
                      setAmountInput(e.target.value);
                      if (canRetrySameKey) setCanRetrySameKey(false);
                    }}
                    placeholder="1000.00"
                    disabled={busy || canRetrySameKey || !isEmailVerified}
                    required
                    autoComplete="off"
                    aria-invalid={!!amountInput && !validation.isValid}
                    aria-describedby={`funding-amount-hint${amountInput && !validation.isValid ? " funding-amount-error" : ""}`}
                    autoFocus
                  />
                </div>
              </FormField>

              <div className="preset-amounts-group">
                <span className="preset-label">Quick select:</span>
                <div className="preset-chips">
                  {PRESET_AMOUNTS.map((val) => (
                    <button
                      key={val}
                      type="button"
                      className={`preset-chip ${amountInput === String(val) ? "active" : ""}`}
                      onClick={() => {
                        setAmountInput(String(val));
                        if (canRetrySameKey) setCanRetrySameKey(false);
                      }}
                      disabled={busy || canRetrySameKey || !isEmailVerified}
                    >
                      +{val >= 1000 ? `${val / 1000}k` : val}
                    </button>
                  ))}
                </div>
              </div>

              <div className="balance-projection-card">
                <div className="projection-row">
                  <span>Current balance:</span>
                  <span>{formatLkr(currentBalance)}</span>
                </div>
                <div className="projection-row projection-highlight">
                  <span>Projected balance:</span>
                  <span>{formatLkr(projectedBalance)}</span>
                </div>
                <div className="projection-meta">
                  <span>Cap: {formatLkr(WALLET_LIMITS.MAX_BALANCE_CAP_LKR)}</span>
                  <span>•</span>
                  <span>
                    {remainingDaily} of {WALLET_LIMITS.MAX_DAILY_OPERATIONS} fundings left today
                  </span>
                </div>
              </div>

              <div className="modal-actions">
                <Button
                  type="button"
                  className="button-secondary"
                  onClick={onClose}
                  disabled={busy}
                >
                  Cancel
                </Button>
                <Button
                  type="submit"
                  className="button-primary"
                  busy={busy}
                  disabled={!isEmailVerified || !validation.isValid || remainingDaily <= 0}
                >
                  {canRetrySameKey ? "Retry Safe Funding" : `Confirm & Add ${parsedAmount > 0 ? formatLkr(parsedAmount) : "Funds"}`}
                </Button>
              </div>
            </form>
          )}
        </Card>
      </div>
    </dialog>
  );
}
