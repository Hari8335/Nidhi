# Nidhi v1 Business Transaction Model

This document specifies the business transaction model, receipt durability, rounding rules, conversion residual derivation, and failure representations for Nidhi v1, adhering to [OPEN_DECISIONS.md](../requirements/OPEN_DECISIONS.md) (OD-004, OD-005, OD-006, OD-012).

---

## 1. Business Transaction Concept

A business transaction represents an agreed, completed financial event in the Nidhi system. In v1, there are two distinct financial transaction types:
1. `WALLET_FUNDING`: Simulated cash credit to the customer's wallet.
2. `GOLD_PURCHASE`: Simulated cash debit from the wallet and conversion into fractional gold grams.

---

## 2. Modeling Options Evaluation

We evaluated three potential database and domain representations:

| Approach | Structure | Pros | Cons | Recommendation |
|---|---|---|---|---|
| **Option A: Single Table with Discriminator (`financial_transactions`)** | One table with `type` column (`WALLET_FUNDING`, `GOLD_PURCHASE`). Shared fields (`amount_lkr`, `post_wallet_balance_lkr`) are non-null; gold-specific fields (`gold_quantity_grams`, `price_version_id`, `applied_price_lkr`) are nullable for funding. | Single table for customer history queries; simple sequential pagination; easy foreign key linking from ledger entries; standard EF Core TPH or single entity. | Gold-specific columns are nullable for wallet funding. | **SELECTED (Cleanest & Most Pragmatic)** |
| **Option B: Separate Tables (`wallet_funding_transactions`, `gold_purchase_transactions`)** | Two completely separate tables with strict non-nullable schemas. | Strict relational schema; no nullable columns. | Polymorphic foreign keys from `ledger_entries`; difficult to perform unified pagination for customer transaction history without SQL `UNION`. | Rejected due to ledger and pagination complexity. |
| **Option C: Table-Per-Type (TPT) with Base Table** | Base table `transactions` + extension tables `wallet_fundings` and `gold_purchases` sharing PK. | Fully normalized; no nullable fields in base table. | Requires JOINs for every query; EF Core TPT adds unnecessary mapping overhead and query complexity. | Rejected as overengineering for v1. |

### Decision: Option A (Single Unified Table with Type Check Constraints)
We select **Option A**. The table contains a `CHECK` constraint ensuring that if `type = 'GOLD_PURCHASE'`, the fields `gold_quantity_grams`, `price_version_id`, `applied_price_per_gram_lkr`, and `post_gold_holding_grams` must be strictly non-null and positive. Conversely, if `type = 'WALLET_FUNDING'`, those fields must be `NULL`.

---

## 3. Transaction Entity Specification

### 3.1. Attributes & Schema

```text
financial_transactions:
  id:                             uuid (PK, UUIDv7)
  customer_id:                    uuid (FK to customer_profiles, NOT NULL)
  type:                           varchar(32) (NOT NULL, 'WALLET_FUNDING' | 'GOLD_PURCHASE')
  status:                         varchar(32) (NOT NULL, 'COMPLETED')
  amount_lkr:                     decimal(18,2) (NOT NULL)
  gold_quantity_grams:            decimal(20,8) (NULLable, required for GOLD_PURCHASE)
  price_version_id:               uuid (FK to gold_prices, NULLable, required for GOLD_PURCHASE)
  applied_price_per_gram_lkr:     decimal(18,4) (NULLable, required for GOLD_PURCHASE)
  post_wallet_balance_lkr:        decimal(18,2) (NOT NULL)
  post_gold_holding_grams:        decimal(20,8) (NULLable, required for GOLD_PURCHASE)
  idempotency_record_id:          uuid (FK to idempotency_records, NOT NULL, UNIQUE)
  created_at_utc:                 timestamptz (NOT NULL)
```

### 3.2. Immutability and Receipt Semantics
- **Strict Immutability**: Once committed, rows in `financial_transactions` are never updated or deleted. There are no v1 endpoints or workflows that modify transaction rows.
- **Snapshot of Balances**: `post_wallet_balance_lkr` and `post_gold_holding_grams` preserve the customer's exact balances immediately following the transaction commit. If a customer views their historical receipt a week later, these values remain the historical snapshot, not the customer's current balance.
- **Immutable Pricing**: The `applied_price_per_gram_lkr` captures the exact price per gram active and validated at the execution moment. Even if the price version later expires or is superseded, the transaction receipt remains permanently verifiable.

---

## 4. Handling Failed Attempts vs Committed Transactions

A critical requirement is that failed commands must not create fabricated or dangling financial transactions (NFR-DATA-002, US-GOLD-002, US-WALLET-002).

| Outcome | Persisted in `financial_transactions`? | Persisted in `ledger_entries`? | Persisted in `idempotency_records`? | Persisted in Structured Logs? |
|---|---|---|---|---|
| **Committed Success** | **Yes** (`Status = COMPLETED`) | **Yes** (Balanced entries) | **Yes** (`Status = COMPLETED`, links `TransactionId`) | Yes (`Information` level) |
| **Validation Failure** (e.g., amount out of range, unverified email) | **No** | **No** | **No** (Key not consumed) | Yes (`Warning` level) |
| **Business Conflict** (e.g., `PRICE_CHANGED`, `INSUFFICIENT_SIMULATED_FUNDS`) | **No** | **No** | **No** (Key not consumed; safe to retry) | Yes (`Warning` level) |
| **Database Failure / Rollback** | **No** (Transaction rolled back) | **No** (Rolled back) | **No** (Rolled back) | Yes (`Error` level) |

*Key Principle*: In v1, the `financial_transactions` table only contains **committed, valid financial transactions**. Rejections and validation failures do not leave rows in `financial_transactions`. This prevents cluttering customer history with failed attempts and guarantees that every transaction in the table corresponds directly to balanced ledger entries.

---

## 5. Rounding and Residual Design (OD-005)

When a customer specifies an LKR amount to save in gold, the conversion follows strict financial precision rules.

### 5.1. Mathematical Definitions

Let:
- $A_{\text{LKR}}$ = Requested LKR amount (in `decimal(18,2)`, where $100.00 \le A_{\text{LKR}} \le 1,000,000.00$).
- $P_{\text{LKR}}$ = Effective simulated gold price per gram (in `decimal(18,4)`, strictly $> 0$).

1. **Raw Unrounded Gold Grams**:
   $$\text{rawGold} = \frac{A_{\text{LKR}}}{P_{\text{LKR}}}$$

2. **Credited Gold Grams (Round DOWN to 8 decimal places)**:
   $$G_{\text{credited}} = \frac{\lfloor \text{rawGold} \times 10^8 \rfloor}{10^8}$$
   *Constraint*: If $G_{\text{credited}} == 0.00000000m$, the transaction is **rejected** (`VALIDATION_ERROR: Gold quantity rounded to zero`).

3. **Credited Value at Applied Price**:
   $$V_{\text{credited}} = G_{\text{credited}} \times P_{\text{LKR}}$$

4. **Conversion Residual in LKR**:
   $$R_{\text{LKR}} = A_{\text{LKR}} - V_{\text{credited}}$$

### 5.2. Concrete Worked Example
- Requested amount: $A_{\text{LKR}} = 100.00m$
- Applied price: $P_{\text{LKR}} = 30,000.0000m$ per gram
- Raw gold:
  $$\text{rawGold} = \frac{100.00}{30000.0000} = 0.0033333333333333333333333333\dots$$
- Truncate (round DOWN) to 8 decimal places:
  $$G_{\text{credited}} = 0.00333333m \text{ grams}$$
- Actual gold value credited:
  $$V_{\text{credited}} = 0.00333333 \times 30000.0000 = 99.999900000000m \text{ LKR}$$
- Conversion residual:
  $$R_{\text{LKR}} = 100.00 - 99.999900000000 = 0.000100000000m \text{ LKR}$$

### 5.3. Rounding Algorithm in C# (Domain Logic)

```csharp
public static class GoldConversionEngine
{
    private const int GoldDecimalPlaces = 8;
    private static readonly decimal Multiplier = 100_000_000m; // 10^8

    public static (decimal CreditedGrams, decimal ResidualLkr) CalculateConversion(decimal amountLkr, decimal pricePerGramLkr)
    {
        if (pricePerGramLkr <= 0m)
            throw new ArgumentOutOfRangeException(nameof(pricePerGramLkr), "Price must be strictly positive.");
        
        if (amountLkr <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amountLkr), "Amount must be strictly positive.");

        // Exact decimal division
        decimal rawGrams = amountLkr / pricePerGramLkr;

        // Truncate (Round Down) to 8 decimal places
        decimal creditedGrams = decimal.Floor(rawGrams * Multiplier) / Multiplier;

        if (creditedGrams == 0m)
            throw new InvalidOperationException("Calculated gold quantity is zero after truncation.");

        // Exact residual calculation
        decimal creditedValue = creditedGrams * pricePerGramLkr;
        decimal residualLkr = amountLkr - creditedValue;

        return (creditedGrams, residualLkr);
    }
}
```

### 5.4. Storage vs Derivation of Conversion Residual (ODQ-001 — DECIDED)

Should `residual_lkr` be stored as an explicit persisted database column on `financial_transactions` or derived dynamically on demand?

**Decision: Dynamically Derived (ODQ-001 — DECIDED)**
- **Immutable Source Values**:
  1. Requested LKR amount (`amount_lkr`, `decimal(18,2)`)
  2. Credited gold quantity (`gold_quantity_grams`, `decimal(20,8)`)
  3. Applied price per gram (`applied_price_per_gram_lkr`, `decimal(18,4)`)
  4. Price version identifier (`price_version_id`, `uuid`)
- **Conceptual Calculation**:
  $$\text{conversionResidualLkr} = \text{amountLkr} - (\text{goldQuantityGrams} \times \text{appliedPricePerGramLkr})$$
- **Relational Schema**: Do **not** add `conversion_residual_lkr` to the v1 relational schema.
- **Precision Guidance**: Because gold is `decimal(20,8)` and price is `decimal(18,4)`, the product and resulting residual can have up to 12 decimal places (sub-cent precision). Reconciliation and audit calculations require this sub-cent decimal precision (e.g. `decimal(28,12)`) even though customer-visible LKR amounts use two decimal places (`decimal(18,2)`). Sub-cent residuals must not be silently rounded away or forced into a 2-decimal column.
- **Future Reporting**: If future reporting requires indexed residual queries, a PostgreSQL generated column or dedicated reporting view can be introduced later without altering the core domain model or duplicating state.
- **Financial Meaning**: The customer wallet is debited the full requested $A_{\text{LKR}}$ (e.g. LKR 100.00). The residual is not a customer fee or refund; it is the rounding delta inherent in fractional commodity purchases.
