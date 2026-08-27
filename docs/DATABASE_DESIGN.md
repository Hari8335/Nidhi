# Nidhi: Database Design

## 1. Financial Engineering Constraints
To ensure precision and correctness:
- **LKR (Sri Lankan Rupees)**: Stored as `Int` (representing cents, e.g., 100.00 LKR = 10000). Avoids JavaScript floating-point arithmetic issues.
- **Gold Quantities**: Stored as `Decimal` (e.g., precision 18, scale 8) to accurately track fractional grams.
- **Double-Entry Bookkeeping**: Core transactions will utilize a simplified double-entry ledger system.

## 2. Core Tables

### 2.1 Users & Identity
- **User**: Core user identity (ID, email, phone, hashed password, role).
- **KycProfile**: Simulated KYC details (ID verification status, simulated documents).

### 2.2 Financial Assets
- **Wallet**: Tracks the user's LKR balance.
- **GoldHolding**: Tracks the user's current gold balance in grams.

### 2.3 Ledger & Transactions
- **Transaction**: The user-facing record of an action (e.g., DEPOSIT, GOLD_CONVERSION).
- **LedgerAccount**: System accounts for double-entry bookkeeping (e.g., User_LKR_Asset, System_Gold_Liability).
- **LedgerEntry**: The atomic debits and credits associated with a `Transaction`. Must always sum to zero per transaction.
- **IdempotencyKey**: Stores unique keys provided by clients for payment-like requests to prevent duplicate execution.

### 2.4 Savings Features
- **SavingsGoal**: Target LKR/Gold amount, deadline, current progress.
- **RecurringPlan**: Frequency (daily, weekly, monthly), amount, next trigger date.

### 2.5 Market & System
- **GoldPrice**: Historical logs of the simulated gold price in LKR per gram.
- **AuditLog**: Immutable append-only log of critical system actions (e.g., admin overriding a setting, failed login attempts).
