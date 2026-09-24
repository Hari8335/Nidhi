# Conceptual data design — Nidhi v1

This is a requirements-led conceptual inventory, **not a finalized schema or ER diagram**. PostgreSQL with Entity Framework Core is planned behind C#/ASP.NET Core. No entities, migrations, indexes or database configuration are implemented here. Product-approved numeric precision is recorded below; other physical schema details remain Session 3 work. Only launch/deployment OD-013–015 remain open in the [decision log](requirements/OPEN_DECISIONS.md); they do not block conceptual domain/API design.

## Financial constraints

Use C# decimal, never float/double, with exact API serialization. Approved precision (OD-005):

| Value | Precision |
|---|---|
| LKR monetary values | decimal(18,2) |
| Simulated LKR price per gram | decimal(18,4) |
| Gold quantity, including goal targets | decimal(20,8) |

Round calculated gold DOWN to 8 decimal places and reject a zero result. Preserve exact LKR amount, immutable price/version and credited grams to calculate/store `residual LKR = amount − (credited grams × price)` for reconciliation/audit. Residuals can be sub-cent, so do not round them away into a decimal(18,2) field; residual representation/storage is a detailed design task. The old integer-cents proposal and gold (18,8) candidate are superseded.

All financial records affecting one operation must commit atomically. Stable transaction identifiers, durable idempotency and concurrency-safe nonnegative wallet balances are required. Preserve immutable transaction/ledger history and price versions. Any future correction must use linked compensating entries, but v1 has no reversal capability.

Retain the simplified double-entry intent. Balance each accounting unit independently: LKR debits/credits cannot be added to gram debits/credits. OD-016 approves separate balanced unit books linked by business transaction. The conceptual chart below is sufficient for v1; detailed entities, debit/credit conventions and posting mechanics remain Session 3 work.

## Justified concepts and relationships

| Concept | Purpose / conceptual relationships | Requirement source / unresolved detail |
|---|---|---|
| User | ASP.NET Core Identity with CUSTOMER/ADMIN; required email/password hash, optional display name and verification information; no phone or speculative status/suspension states | FR-AUTH-001/004/005/006/007; approved identity and controlled provisioning OD-001/002/010/011/018 |
| Wallet | A Customer's simulated LKR balance, initially zero; related funding/saving transactions | FR-WALLET-001/002; approved caps/precision OD-003/005; successful daily count must survive concurrency |
| GoldHolding | A Customer's simulated gold grams, initially zero; affected by completed gold-saving transactions | FR-GOLD-002, FR-HOLDING-001; approved precision and separate-unit reconciliation OD-005/016 |
| GoldPrice | Retained simulated LKR/gram price versions; transactions reference the version actually used | FR-GOLD-001/003, FR-ADMIN-005/006; immediate activation, required priceVersionId, 24-hour freshness and first production publication OD-006/017 |
| Transaction | Stable Customer-linked receipt for simulated funding or gold-saving; contains committed inputs/results/status/time, including original post-operation balances | FR-TRANSACTION-001/002; failure record lifecycle and exact persisted fields await domain design |
| LedgerAccount | Conceptual classification of entries per unit and owner/system counterparty, sufficient for double-entry reconciliation | NFR-DATA-005; not a committed table design, OD-016 |
| LedgerEntry | Immutable transaction-linked debit/credit evidence with unit and account relationship | FR-ADMIN-004; accounting semantics OD-016 |
| Idempotency association | Associates Customer, operation and request content with durable outcome; must protect retries across restarts | FR-WALLET-003, FR-GOLD-004; successful association permanent; same-content replay, changed-content conflict and safe pre-commit retry OD-012; physical representation deferred |
| SavingsGoal | Customer-owned positive target and progress policy; no financial mutation merely from creation | FR-GOAL-001/002; total-gram target; one active goal; optional date; current total holding / target; no reservation OD-007/008 |
| AuditLog | Stable actor/action/subject/time/reason/before-after evidence, including price-version association | FR-AUDIT-001/002; privacy, retention and access OD-013 |

This inventory does not decide identifiers' physical types, relationship cardinalities at storage level, computed versus stored projections, constraints/indexes, transaction isolation or migration strategy. One conceptual wallet/holding per Customer is the v1 product view; implementation must derive an explicit model from approved rules.

## Deferred concepts

KycProfile/document verification, RecurringPlan, payment records, custody/redemption and general product/marketing notification models are outside v1. They are future concepts, not empty tables to scaffold. Identity verification and one-time email reset support are in scope through ASP.NET Core Identity; their concrete model follows OD-002/018 during identity design.

## Next design gate

Use the approved limits, numeric precision/round-down, immediate price versions and reconfirmation, total-gram goals, verified identity and permanent successful idempotency as inputs. Derive domain model, ER diagram, REST representations and migration plan in that order. Resolve detailed residual representation, posting mechanics, day-boundary/timezone, canonicalization and concurrency behavior in design; do not reopen decided product rules. Validate [business flows](requirements/GOLD_SAVING_FLOW.md) before implementation. Open launch/legal periods do not block this conceptual work.

## Conceptual chart of accounts — v1 only

These are logical roles, not entity classes, table names or finalized debit/credit conventions.

| Book / logical account | Purpose |
|---|---|
| LKR — Customer simulated wallet | Customer LKR balance projection reconciles to this account |
| LKR — System simulated funding source | Counterpart to a simulated wallet funding credit; no external bank settlement |
| LKR — System gold-saving conversion counterpart | Counterpart to the full Customer wallet debit for gold-saving |
| Gold grams — Customer simulated holding | Customer gram balance projection reconciles to this account |
| Gold grams — System simulated gold issuance counterpart | Counterpart to credited grams; no real inventory or custody implied |

Funding pairs equal LKR entries between the funding source and Customer wallet; gold is unchanged. Gold-saving pairs the full LKR amount between Customer wallet and LKR conversion counterpart, and separately pairs credited grams between Customer holding and gram issuance counterpart. The same business transaction links both balanced books. Exact price/version and conversion residual explain the relationship between the units; they never numerically balance one unit against another. Residual evidence does not introduce fees or an extra wallet credit. Detailed posting/entity design belongs to Session 3.
