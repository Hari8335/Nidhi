# Product-documentation audit

Read before revision: AGENTS.md, root README and every Markdown document under docs/, including FOUNDATION_MIGRATION.md. The branch began clean at the completed foundation. This session changes documentation only.

| Finding | Existing evidence | Disposition / reasoning preserved |
|---|---|---|
| Old backend and ORM | PROJECT_SPEC and ARCHITECTURE prescribe NestJS/Prisma; ROADMAP assumes Prisma migrations | Replace current guidance with C#/.NET 10 controllers and planned EF Core/PostgreSQL. Preserve modular monolith, REST and separation of business logic. Historical references remain explicitly historical. |
| Wrong immediate delivery scope | PROJECT_SPEC/ARCHITECTURE/ROADMAP treat React Native/Expo as immediate; website mostly landing/admin | One responsive Next.js application serves Visitor, Customer and Administrator. Native mobile is post-MVP. |
| Nonexistent workspace | ARCHITECTURE lists apps/*, packages/db/shared/config and docker-compose.yml | Document frontend/web and backend/src/tests. Docker remains future engineering scope, not an existing file. |
| Oversized first release | Recurring plans, KYC, notifications, simulated document collection and broad admin features | Preserve ideas as deferred concepts; no KYC/documents/recurring/general notifications or arbitrary admin mutations in v1; required authentication emails are identity infrastructure. |
| Product positioning | Original PROJECT_SPEC says portfolio-only prototype; current brief says public-facing product | Launch-oriented public simulation with honest limitations; retain no-real-money/custody/guaranteed-return boundary. |
| Conflicting precision | DATABASE_DESIGN uses integer cents; AGENTS requires decimal | Use approved decimal(18,2) LKR, decimal(18,4) price and decimal(20,8) gold, round DOWN to 8 places and preserve residual evidence (OD-005); prior gold candidate is superseded. |
| Ambiguous ledger conservation | DATABASE_DESIGN says all entries sum to zero without a unit boundary | OD-016 approves separate balanced unit books; the conceptual v1 chart is documented, while detailed entities/posting and residual representation stay in Session 3. |
| Unclear limits | PROJECT_SPEC lists LKR 100/500/1,000 without policy | Prior presets do not define policy. OD-003/004 now approve LKR 100.00–1,000,000.00 operation limits, a LKR 5,000,000.00 wallet cap and 20 successful funding operations per day. |
| Missing decisions | Registration, recovery, funding caps, rounding, price timing/consent, goals, account lifecycle, idempotency retention and data retention absent | Product owner approved OD-001–012 and OD-016–018 on 2026-09-24; OD-013–015 remain open for launch/deployment, not conceptual design. |
| No measurable requirement set | Existing docs list features without IDs or acceptance behavior | Add traceable FRs, NFRs, actor stories and success/validation/authorization/error scenarios. |
| Overpromised sequencing | ROADMAP implies a fourteen-day MVP including financial and launch work | Use gated requirements → domain → ER → API → implementation → operational readiness sequence; two weeks is a checkpoint. |
| Historical statements becoming stale | README and migration notes say original product docs remain unchanged | README links current requirements; migration note explicitly dates that statement to the foundation milestone. Preserve history, validation and missing-client-source warning. |

## Preserved domain decisions

Simulated LKR-to-gold workflow, fractional holdings, visible progress, stable transaction receipts, simplified double-entry intent, idempotency, atomicity, immutable history and administrative auditability remain. Future corrections require linked compensating entries, but reversal tooling is outside v1.

## Current unresolved areas, not contradictions

The [decision log](OPEN_DECISIONS.md) now has 15 approved ODs and three open launch/deployment ODs. Authentication emails are required v1 identity infrastructure; general product/marketing notifications remain post-MVP. Customer eligibility means registered and email-verified, with no suspension/status model. Numeric precision, goal semantics, pricing, idempotency and separate-unit accounting are decided. Retention/legal/deployment policy does not block conceptual domain/API design.

No current-stack contradiction is intentionally retained. Old technology names remain only in historical/rationale contexts. The source archive missing before foundation commit remains a historical preservation limitation, not a new archive created or deleted in this session.

## Product-owner reconciliation

Resolved stale contradictions: blanket email exclusion versus required identity email, generic account-status workflows versus verification-only eligibility, undecided precision/goals versus approved values, provisional session transport versus Identity cookies, and all-OPEN design blocking versus launch-only remaining decisions.

No unresolved contradiction was found. Execution and replay are distinct: an already committed purchase returns its historical receipt even if the price is now stale/replaced; a new execution must validate the submitted version and age. Reconfirmation with a different version changes content and uses a new idempotency key. Successful financial associations are permanent even though broader retention periods remain a launch-policy decision; silent deletion is not an option. Detailed day-boundary, residual representation and security settings are explicitly deferred design elaboration, not invented policy.
