# Conceptual data design — Nidhi v1

This requirements-led overview summarizes the approved [relational model](design/ERD.md). Session 4 implemented and validated PostgreSQL 18 / EF Core 10 persistence, Identity storage, constraints, and the initial migration. Financial application use cases remain future work. Only launch/deployment OD-013–015 remain open in the [decision log](requirements/OPEN_DECISIONS.md).

## Financial constraints

Use C# decimal, never float/double, with exact API serialization. Approved precision (OD-005):

| Value | Precision |
|---|---|
| LKR monetary values | decimal(18,2) |
| Simulated LKR price per gram | decimal(18,4) |
| Gold quantity, including goal targets | decimal(20,8) |

Round calculated gold DOWN to 8 decimal places and reject a zero result. Preserve exact LKR amount, immutable price/version and credited grams to calculate `residual LKR = amount − (credited grams × price)` for reconciliation/audit. Residuals can be sub-cent, so do not round them away into a decimal(18,2) field. Under ODQ-001, conversion residual is dynamically derived from immutable transaction inputs rather than stored as a redundant database column; if future reporting requires indexed residual queries, a generated/reporting representation can be added later. The old integer-cents proposal and gold (18,8) candidate are superseded.

All financial records affecting one operation must commit atomically. Stable transaction identifiers, durable idempotency and concurrency-safe nonnegative wallet balances are required. Preserve immutable transaction/ledger history and price versions. Any future correction must use linked compensating entries, but v1 has no reversal capability.

Retain the simplified double-entry intent. Balance each accounting unit independently: LKR debits/credits cannot be added to gram debits/credits. OD-016 approves separate balanced unit books linked by business transaction. The conceptual chart below is sufficient for v1; the detailed entities and posting conventions are specified in the Session 3 design documents; posting services are not yet implemented.

## Justified concepts and relationships

| Concept | Purpose / conceptual relationships | Requirement source / unresolved detail |
|---|---|---|
| User | ASP.NET Core Identity with CUSTOMER/ADMIN; Identity-owned email/password hash and verification information; no phone collection or 2FA workflow and no speculative status/suspension states | FR-AUTH-001/004/005/006/007; approved identity and controlled provisioning OD-001/002/010/011/018 |
| CustomerProfile | Display name and timestamps linked 1:1 to the Identity user; no duplicated email or authentication fields | FR-PROFILE-001; Identity is the source of truth for email |
| Wallet | A Customer's simulated LKR balance, initially zero; related funding/saving transactions | FR-WALLET-001/002; approved caps/precision OD-003/005; successful daily count must survive concurrency |
| GoldHolding | A Customer's simulated gold grams, initially zero; affected by completed gold-saving transactions | FR-GOLD-002, FR-HOLDING-001; approved precision and separate-unit reconciliation OD-005/016 |
| GoldPrice | Retained simulated LKR/gram price versions; transactions reference the version actually used | FR-GOLD-001/003, FR-ADMIN-005/006; immediate activation, required priceVersionId, 24-hour freshness and first production publication OD-006/017 |
| Transaction | Stable Customer-linked receipt for simulated funding or gold-saving; contains committed inputs/results/status/time, including original post-operation balances | FR-TRANSACTION-001/002; COMPLETED records only; failed command attempts may be logged/observed separately, never stored as financial transactions |
| LedgerAccount | Conceptual classification of entries per unit and owner/system counterparty, sufficient for double-entry reconciliation | NFR-DATA-005; implemented ledger_accounts mapping, OD-016 |
| LedgerEntry | Immutable transaction-linked debit/credit evidence with unit and account relationship | FR-ADMIN-004; accounting semantics OD-016 |
| Idempotency association | Associates Customer, operation and request content with durable outcome; must protect retries across restarts | FR-WALLET-003, FR-GOLD-004; successful association permanent; same-content replay, changed-content conflict and safe pre-commit retry OD-012; implemented idempotency_records storage, command orchestration deferred |
| SavingsGoal | Customer-owned positive target and progress policy; no financial mutation merely from creation | FR-GOAL-001/002; total-gram target; one active goal; optional date; current total holding / target; no reservation OD-007/008 |
| AuditEvent | Stable admin or system actor/action/subject/time/reason/before-after evidence, including price-version correlation; no mandatory Identity-user FK | FR-AUDIT-001/002; privacy, retention and access OD-013 |

The [ERD](design/ERD.md) specifies the implemented identifiers, columns, relationships, constraints, and indexes. Identity retains standard built-in `phone_number`, `phone_number_confirmed`, and `two_factor_enabled` columns; these do not authorize phone collection or expose phone/2FA functionality in Nidhi v1. Profile email is obtained from Identity, whose normalized-email index is unique.

The ledger account alternate key `AK_ledger_accounts_id_unit` on `(id, unit)` supports the composite FK from `ledger_entries(account_id, unit)`. This database constraint guarantees that an entry's unit matches its account. Gold-purchase CHECK constraints use explicit `IS NOT NULL` guards because PostgreSQL rejects only `FALSE`, while `UNKNOWN` passes. These guards implement the existing required-field rule.

Audit actors may be system-generated and need not correspond to a human Identity user. Required stable actor IDs, roles, subjects, timestamps, and recorded context preserve attribution. `gold_prices.audit_log_id` is a correlation identifier, not a mandatory FK; controlled application transactions must preserve the audit association.

PostgreSQL does not independently guarantee append-only behavior against privileged/manual SQL, whole-posting-set balance, LKR cent granularity in the shared `numeric(20,8)` ledger amount column, or cross-record financial business rules. Controlled application writes, transaction orchestration, and appropriate use-case/reconciliation tests must enforce these rules. Session 4 validates structural persistence, not those future use cases; see [enforcement boundaries](design/ERD.md#persistence-enforcement-boundaries).

## Deferred concepts

KycProfile/document verification, RecurringPlan, payment records, custody/redemption and general product/marketing notification models are outside v1. They are future concepts, not empty tables to scaffold. Identity verification and one-time email reset support are in scope through ASP.NET Core Identity; Identity persistence exists, while the OD-002/018 authentication and token workflows remain future work.

## Detailed Design Specifications

Session 3 establishes the concrete specifications elaborated from this conceptual inventory:
- [Domain Model](design/DOMAIN_MODEL.md): Core entities, value objects, and aggregate boundaries.
- [Transaction Model](design/TRANSACTION_MODEL.md): Single-table financial transaction model and floor-to-8 decimal rounding.
- [Financial Ledger Model](design/LEDGER_MODEL.md): Double-entry ledger with direction and positive amounts across isolated unit books.
- [Relational Data Model & ERD](design/ERD.md): Detailed PostgreSQL tables, column types, check constraints, and partial unique indexes.
- [Concurrency & Idempotency Design](design/CONCURRENCY.md): Pessimistic row locking for wallets and permanent idempotency retention.

## Conceptual chart of accounts — v1 only

These logical account roles are expanded into posting conventions in the [ledger model](design/LEDGER_MODEL.md). No fixed accounts are seeded by the initial migration.

| Book / logical account | Purpose |
|---|---|
| LKR — Customer simulated wallet | Customer LKR balance projection reconciles to this account |
| LKR — System simulated funding source | Counterpart to a simulated wallet funding credit; no external bank settlement |
| LKR — System gold-saving conversion counterpart | Counterpart to the full Customer wallet debit for gold-saving |
| Gold grams — Customer simulated holding | Customer gram balance projection reconciles to this account |
| Gold grams — System simulated gold issuance counterpart | Counterpart to credited grams; no real inventory or custody implied |

Funding pairs equal LKR entries between the funding source and Customer wallet; gold is unchanged. Gold-saving pairs the full LKR amount between Customer wallet and LKR conversion counterpart, and separately pairs credited grams between Customer holding and gram issuance counterpart. The same business transaction links both balanced books. Exact price/version and conversion residual explain the relationship between the units; they never numerically balance one unit against another. Residual evidence does not introduce fees or an extra wallet credit. Detailed posting/entity design is recorded in Session 3; application posting services remain future work.
