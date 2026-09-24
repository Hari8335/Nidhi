# Decision log — Nidhi v1

Product-owner decisions approved on 2026-09-24. IDs are stable and must not be reused. DECIDED entries below replace earlier recommendations; implementation remains outside this documentation session. Only OD-013, OD-014 and OD-015 remain OPEN. They govern launch/deployment policy and **do not block conceptual domain or API design**. Do not encode unapproved legal eligibility or retention periods as domain facts.

## Approved product decisions

| ID / status | Approved decision and rationale | Traceability |
|---|---|---|
| OD-001 — DECIDED | Registration requires email and password; display name is optional. Do not collect phone in v1. Minimize identity data. | FR/US-AUTH-001; NFR-PRIV-001 |
| OD-002 — DECIDED | Email verification is required before simulated financial features. Password recovery uses a one-time email reset flow. Authentication emails are identity infrastructure in v1; general product/marketing notifications remain post-MVP. | FR/US-AUTH-005/006; NFR-SEC-002/004/005 |
| OD-003 — DECIDED | Simulated funding: LKR 100.00 minimum, LKR 1,000,000.00 maximum per operation, LKR 5,000,000.00 maximum resulting wallet balance, at most 20 successful funding operations per Customer per day. Use validated configuration where appropriate instead of scattered constants. Replays are not new successful operations. | FR/US-WALLET-002/003; NFR-DATA-002/003, NFR-SEC-005 |
| OD-004 — DECIDED | Gold-saving amount is LKR 100.00–1,000,000.00 inclusive per purchase, cannot exceed available simulated wallet balance, and uses two decimal places. | FR/US-GOLD-002; NFR-DATA-001 |
| OD-005 — DECIDED | Use C# decimal, never float/double. LKR monetary values: decimal(18,2); simulated price per gram: decimal(18,4); gold quantity: decimal(20,8). Round calculated gold DOWN to 8 decimal places; reject a zero result. Preserve sufficient exact conversion information to calculate/store the residual for reconciliation/audit. Historical transactions retain the immutable exact price/version used. | FR/US-GOLD-002/003; NFR-DATA-001/004/005; conceptual data design |
| OD-006 — DECIDED | Each successful Administrator publication creates an immutable price version active immediately. Purchases reference the displayed priceVersionId. Revalidate at execution; a noncurrent version causes a price-changed conflict requiring reconfirmation, with no silent substitution. Disable purchases when the active price is older than 24 hours. | FR/US-GOLD-001/002/003, FR/US-ADMIN-005/006; gold-saving flow |
| OD-007 — DECIDED | Goals target total gold grams. Progress = current total simulated gold holding / target grams. Market-value changes do not directly change completion. | FR/US-GOAL-001/002, FR/US-HOLDING-002 |
| OD-008 — DECIDED | One active goal per Customer; optional target date; no allocation/reservation of funds or gold. Actual progress can exceed target; the visual indicator may cap at 100%. | FR/US-GOAL-001/002 |
| OD-009 — DECIDED | Display name is the only editable profile field. Email/password changes are identity/security operations, not profile edits. This does not add an email-change workflow to scope. | FR/US-PROFILE-001; NFR-PRIV-001 |
| OD-010 — DECIDED | Registered, email-verified Customers are eligible for customer features. Do not model suspension/status workflows or speculative future states. Verification/recovery access remains possible before verification; it must not unlock customer financial features. | FR/US-AUTH-002/004/005; FR/US-ADMIN-003 |
| OD-011 — DECIDED | Public registration creates CUSTOMER only. ADMIN is created through controlled operational bootstrap/provisioning with protected configuration/secrets, no hard-coded/shared credential. Provisioning must be auditable where practical. | FR-AUTH-001/007; US-AUTH-001, US-ADMIN-007/008; NFR-AUDIT-001 |
| OD-012 — DECIDED | Financial idempotency key scope is Customer + operation and is bound to equivalent request content (or its hash). A successful transaction permanently retains its association. Same key/equivalent content returns original success; different content conflicts. Pre-commit failures may be retried safely with the same key. HTTP/header names are deferred to API design. | FR/US-WALLET-003, FR/US-GOLD-004; NFR-DATA-003 |
| OD-016 — DECIDED | Separate balanced books by unit: LKR balances against LKR; gold grams against gold grams. One business transaction links the relevant books; never balance currency numerically against grams. Conceptual chart only here; detailed accounts/entities belong to Session 3. | FR/US-ADMIN-004; NFR-DATA-005; conceptual data design |
| OD-017 — DECIDED | No production fallback price. An Administrator must successfully publish the first positive simulated price before gold-saving. Development/test may use explicit fixture/seed prices. | FR/US-GOLD-001, FR/US-ADMIN-006 |
| OD-018 — DECIDED | Web authentication uses ASP.NET Core Identity and secure HttpOnly cookies, server-side authorization, no remember-me, no browser JWT/localStorage authentication. Login/registration require abuse and rate-limit protections. Token/mobile authentication is deferred until a mobile client is designed. | FR/US-AUTH-002/003/004; NFR-SEC-001 through NFR-SEC-005; architecture |

## Open launch/deployment decisions

| ID / status | Decision needed / why | Options and current guidance | Gate |
|---|---|---|---|
| OD-013 — OPEN | Exact retention, privacy and deletion periods for records/logs/backups; privacy and historical integrity must agree | Distinct retention by record class; anonymization or controlled deletion procedures after review. Financial/audit history cannot be silently mutated/deleted; permanent successful idempotency associations remain a decided constraint. | Product/legal launch policy, not conceptual domain/API design |
| OD-014 — OPEN | Traffic assumptions, hosting budget, backup frequency and RPO/RTO | Bounded pilot or broader launch; choose measured capacity and recovery targets during deployment planning. No enterprise SLA is promised. | Deployment planning |
| OD-015 — OPEN | Public age/geographic eligibility and final legal content | Product/legal launch review. English-first is a working content assumption only; do not encode unsupported eligibility restrictions into the domain model. | Public launch review |

## Design elaboration, not reopened product decisions

Session 3/API design must specify the daily funding counter's timezone/day boundary, amount normalization and equivalent-request canonicalization, residual representation and storage without loss, concurrency ordering for price publication versus purchase execution, goal lifecycle once completed, and exact HTTP/header names. Security design must specify cookie lifetime/SameSite/CSRF behavior, verification/reset token lifetime and abuse thresholds. These details must be documented and tested before their implementation; no values are silently invented here. Indicative portfolio display rounding also needs an explicit presentation rule and must not change financial records. None reopens the approved limits, 24-hour freshness rule, precision, identity mechanism or permanent successful idempotency association.

## Decided boundaries

| ID / status | Decision, options considered, and rationale |
|---|---|
| DD-001 — DECIDED | Simulated LKR and simulated gold only. Real payments/custody are excluded by the v1 brief; no monetary value, real gold ownership or guaranteed return claims. |
| DD-002 — DECIDED | One responsive Next.js app for Visitor, Customer and Administrator experiences; native mobile deferred to keep one initial delivery surface. |
| DD-003 — DECIDED | Two roles, CUSTOMER and ADMIN, authorized by ASP.NET Core; complex RBAC and frontend-only security rejected. |
| DD-004 — DECIDED | Simulated funding creates a stable transaction receipt and ledger records atomically, rather than just editing a balance. This makes funding explainable and reconcilable. |
| DD-005 — DECIDED | Administrator mutations are limited to price updates. Customer suspension UI, reversal and manual financial corrections are post-MVP; future corrections must use linked compensating records rather than edits. |
| DD-006 — DECIDED | No fees, interest, rewards, gold sales, transfers or withdrawals in v1. The requested conversion amount is the wallet debit; rounding/residual requirements are decided in OD-005. |
| DD-007 — DECIDED | Every successful price change creates a new retained price version and an audit record. Overwriting historical prices is rejected so receipts remain reproducible. Immediate activation and freshness rules are decided in OD-006. |
| DD-008 — DECIDED | PostgreSQL/EF Core behind a .NET 10 modular monolith. Decimal values, atomic financial operations, safe retries and immutable history are mandatory; concrete schema and endpoint shapes are deferred. |

Decision changes must include rationale, affected FR/NFR/story IDs and approval date. A change to a decided boundary requires a scope update, not an implicit exception in implementation.
