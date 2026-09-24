# Nidhi v1 MVP scope

V1 is the smallest coherent public web simulation described in the [product definition](../PROJECT_SPEC.md). All financial amounts, gold holdings, and transactions are simulated. This is target scope, not a statement of implemented features.

## In scope — Nidhi v1

| Area | Required capability |
|---|---|
| Public | Landing with how it works, features and benefits; About; FAQ; simulation disclosures; Terms; Privacy; Contact information; Register and Login entry points |
| Identity | Email/password registration with optional display name and no phone; email verification and one-time email password reset; ASP.NET Core Identity Secure HttpOnly cookies, no remember-me; Customer/Administrator login/logout and two server-enforced roles (`CUSTOMER`, `ADMIN`) |
| Customer dashboard | Own simulated wallet balance, simulated gold holding, indicative portfolio value, current simulated gold price, recent transactions, goal progress |
| Simulated wallet | View own balance; add LKR 100.00–1,000,000.00 simulated funds, at most LKR 5,000,000.00 resulting balance and 20 successes per Customer/day; durable receipt, ledger, atomicity and permanent successful idempotency |
| Simulated gold | View current price; save LKR 100.00–1,000,000.00 within available balance against the displayed current priceVersionId; round gold DOWN to 8 places; require price no older than 24 hours; retain receipt/residual evidence |
| Customer records | Own holdings, paginated transaction history and details, explicit empty/error states |
| Savings goal | Create one active total-gold-gram goal with optional date, list own goals and view current total holding / target progress; no allocation/reservation or edit/delete/automation in v1 |
| Profile | View profile and edit display name only; email/password are separate identity/security concerns |
| Administrator | Dashboard with customer count, completed transaction count and current simulated gold price; customer list/search/details; transactions/details/associated ledger; current price/history/update; audit log review |
| Engineering | ASP.NET Core REST API/OpenAPI, PostgreSQL, EF Core migrations, authentication/authorization, decimal precision, atomicity, idempotency, structured logging, tests, Docker/Compose, CI/CD, deployment and launch checks |

Public information may use pages or accessible sections; route structure is deferred to API/UI design. Contact means published contact information, not a ticketing system. Registration creates a Customer account only. No admin financial mutations beyond price management are implied by read access.

## Out of scope / post-MVP

- React Native/native mobile apps.
- Real payment processing, bank/payment-gateway integrations, actual gold purchasing, custody, redemption, withdrawals, transfers, and selling simulated gold back to LKR.
- Real KYC providers, simulated or real document verification, and identity-document collection.
- Recurring automated deductions/plans, live gold-market integration, push notifications, general product/marketing email notification workflows, and automated reminders.
- Advanced analytics, profit forecasting, customer-support ticketing, multi-currency support, complex admin roles, and advanced fraud-detection systems.
- Microservices, message queues, Redis, Kafka, event buses, or speculative enterprise abstractions.
- Administrator customer suspension, arbitrary balance edits, role-management screens, transaction reversal/manual financial corrections, and deletion of transaction history.

These remain possible future work, not implied commitments. Email verification and one-time password-reset emails are explicitly in scope as identity infrastructure (OD-002); this does not include general product/marketing notifications. No phone, customer suspension/status workflows or speculative future states are modeled.

## V1 completion boundary

A Visitor can understand the simulation and register; a Customer can verify email, authenticate, recover their password, add simulated funds, save into simulated gold, inspect receipts/holdings/progress, and logout; an Administrator can inspect those records and safely publish a new simulated price. No real-money path exists.

Release requires applicable [FRs](FUNCTIONAL_REQUIREMENTS.md), [NFRs](NON_FUNCTIONAL_REQUIREMENTS.md), and [acceptance criteria](USER_STORIES.md) to pass with recorded evidence, all launch-blocking [decisions](OPEN_DECISIONS.md) resolved, and the [roadmap](../ROADMAP.md) launch review completed. A two-week milestone alone is not release approval.
