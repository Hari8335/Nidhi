# Nidhi v1 Financial Ledger Model

This document specifies the double-entry ledger design for Nidhi v1. It implements [OPEN_DECISIONS.md](../requirements/OPEN_DECISIONS.md) (OD-016), [FUNCTIONAL_REQUIREMENTS.md](../requirements/FUNCTIONAL_REQUIREMENTS.md) (FR-ADMIN-004), and [NON_FUNCTIONAL_REQUIREMENTS.md](../requirements/NON_FUNCTIONAL_REQUIREMENTS.md) (NFR-DATA-005).

---

## 1. Core Accounting Principles

1. **Strict Unit Isolation**: The ledger maintains two completely isolated books:
   - **LKR Book**: All entries are denominated in Sri Lankan Rupees (`Unit = LKR`).
   - **Gold Grams Book**: All entries are denominated in grams of fine gold (`Unit = GOLD_GRAMS`).
2. **Never Balance Currency against Commodity**: LKR entries only balance against LKR entries. Gold-gram entries only balance against gold-gram entries. The units are never numerically added or equated directly.
3. **Transaction Linkage**: A single `FinancialTransaction` record serves as the correlation anchor uniting an LKR posting set and a gold-gram posting set.
4. **Append-Only Immutability**: Ledger entries are strictly append-only. Once inserted, an entry is never modified or deleted. Any future adjustments require explicit compensating journal entries (NFR-DATA-004).

---

## 2. Entry Representation Choice: Direction + Positive Amount

We evaluated two standard double-entry representations:

| Feature | Option A: Signed Amounts | Option B: Direction (`DEBIT` / `CREDIT`) + Positive Amount |
|---|---|---|
| **Representation** | `amount decimal` (positive = debit, negative = credit, or vice versa). | `direction varchar(8)` (`DEBIT` or `CREDIT`) and `amount decimal` (strictly $> 0$). |
| **Balancing Rule** | $\sum \text{amount} = 0$ per unit per transaction. | $\sum \text{amount}_{\text{DEBIT}} = \sum \text{amount}_{\text{CREDIT}}$ per unit per transaction. |
| **Database Constraints**| Cannot enforce strictly positive amounts; risks confusing negative debits with credits. | Enforces `amount > 0` check constraint cleanly; direction is an enumerated value. |
| **Auditability** | Vulnerable to sign inversion bugs in application logic. | Standard accounting industry practice; unambiguous audit inspection for administrators. |
| **Recommendation** | Rejected. | **SELECTED**. |

### Decision: Option B
Every ledger entry specifies:
- `unit`: `LKR` or `GOLD_GRAMS`.
- `direction`: `DEBIT` or `CREDIT`.
- `amount`: strictly positive decimal (`decimal(18,2)` for LKR, `decimal(20,8)` for gold grams).

---

## 3. Conceptual Chart of Accounts (v1)

In double-entry accounting from the perspective of the **Nidhi System Operator**:

| Account Code | Account Name | Unit | Type | Owner | Description |
|---|---|---|---|---|---|
| `1001-LKR-SYS-FUNDING` | System Simulated Funding Source | `LKR` | Equity / Source | System | Conceptual source of simulated customer deposits. Holds an offsetting credit balance. |
| `2001-LKR-CUST-{CustomerId}` | Customer Simulated Wallet | `LKR` | Liability | Customer | The system's liability to the customer for their simulated LKR cash balance. |
| `2002-LKR-SYS-CLEARING` | Gold Conversion Clearing Account | `LKR` | Clearing | System | Temporary counterpart for LKR debited from customer wallets during gold saving. |
| `1501-GOLD-SYS-ISSUANCE` | System Gold Issuance Source | `GOLD_GRAMS` | Inventory / Source | System | Conceptual source of simulated fractional gold issuance. Holds an offsetting credit balance. |
| `2501-GOLD-CUST-{CustomerId}` | Customer Simulated Gold Holding | `GOLD_GRAMS` | Custodial / Asset | Customer | Customer's accumulated simulated gold holdings in grams. |

*Note*: These accounts are conceptual ledgers for a simulated fintech product. They do not imply real-world bank settlement accounts or physical gold vault custody.

---

## 4. Posting Sets by Operation

### 4.1. Operation 1: Simulated Wallet Funding

When a customer deposits simulated LKR (e.g., LKR 10,000.00):
- Transaction Type: `WALLET_FUNDING`
- Linked Posting Set: **LKR Book Only** (2 entries).

| Entry | Account Code | Direction | Unit | Amount (LKR) | Meaning |
|---|---|---|---|---|---|
| 1 | `1001-LKR-SYS-FUNDING` | **DEBIT** | `LKR` | 10,000.00 | System funding source disbursed funds. |
| 2 | `2001-LKR-CUST-{CustomerId}` | **CREDIT** | `LKR` | 10,000.00 | Customer wallet liability increased. |

**Balancing Verification**:
$$\sum \text{Debit}_{\text{LKR}} = 10,000.00 = \sum \text{Credit}_{\text{LKR}} = 10,000.00 \quad (\Delta = 0.00)$$

---

### 4.2. Operation 2: Gold-Saving Transaction

When a customer converts LKR into simulated gold (e.g., LKR 100.00 at LKR 30,000.0000/g = 0.00333333g gold):
- Transaction Type: `GOLD_PURCHASE`
- Linked Posting Sets: **Both LKR Book and Gold Grams Book** (4 entries total).

#### Set A: LKR Book (Currency Conversion)
Debits customer wallet for the full purchase amount and credits the system conversion clearing account.

| Entry | Account Code | Direction | Unit | Amount (LKR) | Meaning |
|---|---|---|---|---|---|
| 1 | `2001-LKR-CUST-{CustomerId}` | **DEBIT** | `LKR` | 100.00 | Customer wallet liability decreased. |
| 2 | `2002-LKR-SYS-CLEARING` | **CREDIT** | `LKR` | 100.00 | System conversion clearing account received LKR. |

**Balancing Verification (LKR)**:
$$\sum \text{Debit}_{\text{LKR}} = 100.00 = \sum \text{Credit}_{\text{LKR}} = 100.00 \quad (\Delta = 0.00)$$

#### Set B: Gold Grams Book (Commodity Allocation)
Debits the system gold issuance source and credits the customer's gold holding account.

| Entry | Account Code | Direction | Unit | Amount (Grams) | Meaning |
|---|---|---|---|---|---|
| 3 | `1501-GOLD-SYS-ISSUANCE` | **DEBIT** | `GOLD_GRAMS` | 0.00333333 | System gold source issued fractional gold. |
| 4 | `2501-GOLD-CUST-{CustomerId}` | **CREDIT** | `GOLD_GRAMS` | 0.00333333 | Customer gold holding credited. |

**Balancing Verification (Gold Grams)**:
$$\sum \text{Debit}_{\text{GRAMS}} = 0.00333333 = \sum \text{Credit}_{\text{GRAMS}} = 0.00333333 \quad (\Delta = 0.00000000)$$

---

## 5. Ledger Integrity Invariants & Reconciliation

The ledger must satisfy the following invariant checks during testing, database audits, and operational reviews (NFR-DATA-005):

### Invariant 1: Transaction-Level Balance
For every `transaction_id`:
```sql
-- LKR entries must balance
SELECT SUM(CASE WHEN direction = 'DEBIT' THEN amount ELSE -amount END)
FROM ledger_entries
WHERE transaction_id = @TransactionId AND unit = 'LKR';
-- Must equal 0.00

-- Gold gram entries must balance
SELECT SUM(CASE WHEN direction = 'DEBIT' THEN amount ELSE -amount END)
FROM ledger_entries
WHERE transaction_id = @TransactionId AND unit = 'GOLD_GRAMS';
-- Must equal 0.00000000
```

### Invariant 2: Account Balance vs Wallet/Holding Balance
The projection in `wallets.balance_lkr` must equal the net credit balance of `2001-LKR-CUST-{CustomerId}`:
$$\text{Wallet.BalanceLkr} = \sum \text{Credit}_{2001} - \sum \text{Debit}_{2001}$$

The projection in `gold_holdings.quantity_grams` must equal the net credit balance of `2501-GOLD-CUST-{CustomerId}`:
$$\text{GoldHolding.QuantityGrams} = \sum \text{Credit}_{2501} - \sum \text{Debit}_{2501}$$

### Invariant 3: Zero Mixing of Units
No single query or aggregate may compute sums across different `unit` types. The `ledger_entries` table enforces that an entry's unit must match its parent `ledger_accounts.unit`.
