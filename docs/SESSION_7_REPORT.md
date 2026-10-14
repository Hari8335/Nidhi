# Session 7 — Customer Dashboard + Simulated Wallet Funding

## 1. Overview & Scope

Session 7 establishes the first complete vertical slice in Nidhi: from database persistence and double-entry ledger postings up through ASP.NET Core API endpoints, concurrency locking, idempotency guarantees, Next.js frontend UI, and end-to-end integration tests.

In accordance with product principles:
- **Simulation disclosure**: All funded balances are simulated Sri Lankan Rupees (LKR) for demonstration and educational testing of micro-savings flows. No real money or payment gateway integration is involved.
- **Strict single backend**: All financial business logic, limit checks, idempotency tracking, and ledger entries reside exclusively in ASP.NET Core and PostgreSQL.
- **Financial engineering integrity**: Money and balance calculations strictly use exact decimal arithmetic (`decimal(18,2)`) with zero floating-point operations.

---

## 2. Architecture & Backend Implementation

### 2.1 Domain Layer
- **`Wallet` entity** (`Nidhi.Domain.Entities.Wallet`):
  - Enriched with `ApplyFunding(decimal amount, DateTime utcNow)` to mutate wallet balance atomically and update the timestamp within the domain model.
  - Concurrency token `xmin` (PostgreSQL system row version) mapped and checked on updates.
- **`FinancialTransaction` entity**:
  - Immutably captures completed funding operations: `TransactionId`, `CustomerId`, `Type` (`WALLET_FUNDING`), `Status` (`COMPLETED`), `AmountLkr`, `PostOperationWalletBalanceLkr`, `IdempotencyRecordId`, and `CreatedAtUtc`.
- **`LedgerAccount` & `LedgerEntry` entities**:
  - Double-entry bookkeeping accounts:
    - `1001-LKR-SYS-FUNDING` (Equity: System Simulated Funding Source).
    - `2001-LKR-CUST-{customerId}` (Liability: Customer Simulated Wallet).
  - Paired entries guarantee debit/credit balance equality per transaction.
- **`IdempotencyRecord` entity**:
  - Tracks client idempotency keys, canonical SHA-256 request payload hash, execution status (`IN_PROGRESS` → `COMPLETED`), response transaction reference, and timestamps.

### 2.2 Application Layer
- **Contracts (`Nidhi.Application.Wallet.WalletContracts`)**:
  - `FundWalletRequest`: `[Required] decimal AmountLkr`.
  - `FundingReceiptResponse`: `TransactionId`, `Type`, `Status`, `AmountLkr`, `PostOperationWalletBalanceLkr`, `CreatedAtUtc`.
  - `WalletSummaryResponse`: `BalanceLkr`, `MaxBalanceCapLkr`, `MinFundingAmountLkr`, `MaxFundingAmountLkr`, `SuccessfulFundingCountToday`, `MaxDailyFundingOperations`, `RemainingFundingOperationsToday`.
  - `DashboardResponse`: `WalletBalanceLkr`, `GoldHoldingGrams`, `RecentTransactions`.
  - `WalletFailure`: Typed exception carrying HTTP status and stable machine-readable error codes (`VALIDATION_ERROR`, `WALLET_LIMIT_EXCEEDED`, `DAILY_FUNDING_LIMIT_EXCEEDED`, `IDEMPOTENCY_CONFLICT`, `OPERATION_IN_PROGRESS`).

### 2.3 Infrastructure & Concurrency Control
- **`WalletService` (`Nidhi.Infrastructure.Wallet.WalletService`)**:
  - **Transaction & Lock Ordering**:
    1. Begins a PostgreSQL transaction.
    2. Immediately acquires a row-level lock on the customer's wallet row using:
       `SELECT id, customer_id, balance_lkr, xmin, created_at_utc, updated_at_utc FROM wallets WHERE customer_id = @id FOR UPDATE`.
    3. Resolves existing idempotency state *inside* the lock.
  - **Idempotent Replay Ordering**:
    - If an idempotency record is already `COMPLETED`:
      - Compares the canonical SHA-256 request hash (`WALLET_FUNDING|amountLkr={amount:F2}`) to detect payload conflicts.
      - If hashes match, commits the read-only transaction and returns the original receipt reconstructed from the immutable `financial_transactions` record.
      - **Crucial Invariant**: This replay check runs *before* mutable balance cap or daily operation checks, ensuring that state changes (e.g. customer later reaching the 5,000,000.00 LKR cap or 20 daily operations) never reject a committed replay.
  - **Pre-Commit Failure & Rollback**:
    - Inserts `IN_PROGRESS` within the PostgreSQL transaction.
    - If validation, balance cap (LKR 5,000,000.00), daily operations limit (20 per UTC day), or database errors occur, the transaction rolls back completely.
    - The `IN_PROGRESS` record is discarded, leaving the idempotency key retryable for future attempts.
  - **Daily Limit Concurrency**:
    - Because the customer's wallet row is locked `FOR UPDATE` before reading today's completed transaction count, simultaneous requests for the same customer are strictly serialized at the database lock level. Two racing requests cannot exceed the daily limit of 20 operations.
  - **Money Scale Validation & Normalization**:
    - Explicitly validates that amounts have no non-zero fractional cents (`amount * 100m == decimal.Truncate(amount * 100m)`). Sub-cent amounts (e.g., `100.001`, `100.009`) are rejected with `VALIDATION_ERROR`.
    - Normalizes trailing zero representations (e.g., `100.000` -> `100.00`) to guarantee canonical hashing and exact storage without relying on database engine rounding.
  - **Ledger Invariant**:
    - Atomically writes 1 DEBIT (`1001-LKR-SYS-FUNDING`) and 1 CREDIT (`2001-LKR-CUST-{customerId}`) for the exact positive funding amount.
    - Zero net difference, verified unit (`LKR`), and zero gold gram entries created. Replays create 0 new ledger rows.

### 2.4 API Layer (`CustomerController`)
- `GET /api/v1/customer/dashboard`: Authenticated `CUSTOMER` policy. Returns aggregate balances and recent transactions.
- `GET /api/v1/customer/wallet`: Authenticated `CUSTOMER` policy. Returns wallet limits, balance, and daily quota.
- `POST /api/v1/customer/wallet/fundings`:
  - Enforces `VerifiedCustomer` policy (authenticated `CUSTOMER` with verified email).
  - Rejects unverified customers with 403 Forbidden (`EMAIL_NOT_VERIFIED`).
  - Rejects non-customer roles (`ADMIN`) with 403 Forbidden.
  - Requires mandatory `Idempotency-Key` header (max 128 chars).
  - Protected by ASP.NET Core CSRF/Antiforgery middleware (`X-CSRF-TOKEN`).
  - Returns `201 Created` for new fundings and `200 OK` for idempotent replays.

---

## 3. Frontend & User Experience Implementation

### 3.1 Client Architecture
- Built within Next.js App Router under `frontend/web/`.
- Strict separation: no Next.js backend API routes; all data flows to ASP.NET Core.

### 3.2 Components & Pages
- **Validation Utility (`src/lib/validation/wallet.ts`)**:
  - Pure functions for client-side boundary checking (`100.00` – `1,000,000.00` LKR, max 2 decimals, wallet balance cap check, daily allowance check).
  - `formatLkr` utility for tabular currency formatting.
- **`WalletFundingModal` (`src/components/wallet/wallet-funding-modal.tsx`)**:
  - Modal with quick preset chips (`+500`, `+1k`, `+5k`, `+10k`, `+50k`).
  - Real-time balance and daily limit projection preview.
  - Simulation notice banner and email verification warning when unverified.
  - Idempotency key preserved across network interruptions to allow safe client-side retries without double charging.
  - Receipt card with transaction ID, amount, resulting balance, and timestamp.
- **`CustomerDashboard` (`src/components/customer/customer-dashboard.tsx`)**:
  - Hero card displaying simulated gold holding (`0.00000000 grams` matching the 8-decimal gold standard) and simulated wallet balance.
  - Quick action to open funding modal; disabled "Buy Gold (Session 8)" action.
  - Recent transactions table showing immutable financial receipts with empty state.
  - Unavailable data error state: ensures network or API errors are not rendered as false zero balances.
- **`WalletView` (`src/components/wallet/wallet-view.tsx`)**:
  - Dedicated `/wallet` page with balance cap progress bar (`LKR X / LKR 5,000,000.00`) and daily limit meter (`X of 20 remaining`).
  - Simulation disclosures and operational limits summary.
- **Navigation (`ApplicationShell`)**:
  - Activated `/wallet` link in desktop sidebar and mobile bottom navigation.

---

## 4. Test & Verification Matrix

### 4.1 Backend Test Results
- **Unit Tests**: `Nidhi.UnitTests` — **6 / 6 Passed** (0 failed, 0 skipped).
- **Integration Tests**: `Nidhi.IntegrationTests` with real PostgreSQL — **47 / 47 Passed** (0 failed, 0 skipped).
  - *Coverage*:
    - Verification and authentication policies (anonymous 401, unverified 403, customer isolation).
    - Amount boundaries: minimum (100.00), maximum (1,000,000.00), below min (99.99), above max (1,000,000.01).
    - Decimal scale validation: reject sub-cent (`100.001`, `100.009`), accept trailing zero (`100.000`).
    - Balance cap: accept resulting 5,000,000.00; reject resulting 5,000,000.01 (`WALLET_LIMIT_EXCEEDED`).
    - Daily limit: 20 operations per UTC day; reject 21st operation (`DAILY_FUNDING_LIMIT_EXCEEDED`).
    - Concurrency: 5 concurrent requests at balance cap boundary → exactly 1 succeeds, 4 rejected.
    - Concurrency: 5 concurrent requests at daily limit boundary → exactly 1 succeeds, 4 rejected.
    - Idempotency replay: exact replay returns 200 OK with original receipt; no duplicate credit; no new ledger entries.
    - Idempotency replay at cap: original funding replay succeeds even after wallet has reached 5M cap.
    - Idempotency replay at daily limit: original funding replay succeeds even after 20 operations completed; consumes no additional allowance.
    - Idempotency conflict: same key with different amount returns 409 Conflict (`IDEMPOTENCY_CONFLICT`).
    - Transactional rollback: pre-commit failure rolls back `IN_PROGRESS` idempotency record, permitting later retries.
    - Ledger balance: DEBIT + CREDIT amounts strictly match, positive values, LKR unit only.

### 4.2 Frontend Test Results
- **Unit Tests**: `tests/auth.test.mjs` + `tests/wallet.test.mjs` — **22 / 22 Passed** (0 failed).
- **Type Checking**: `tsc --noEmit` — **0 Errors**.
- **Linting**: `eslint .` — **0 Errors, 0 Warnings**.
- **Production Build**: `next build --webpack` — **12 / 12 Pages Built Successfully** (including `/dashboard` and `/wallet`).
