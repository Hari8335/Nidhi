# Functional requirements

These are target v1 behaviors. IDs are stable. OD-001–012 and OD-016–018 are approved product policy. OPEN OD-013–015 govern launch/deployment policy and do not block conceptual domain/API design. See [scope](MVP_SCOPE.md), [decisions](OPEN_DECISIONS.md), [stories](USER_STORIES.md), [gold-saving flow](GOLD_SAVING_FLOW.md), and [wallet flow](SIMULATED_WALLET_FLOW.md).

All Customer operations require ownership checks; all Administrator operations require ADMIN authorization in ASP.NET Core. Financial values are simulated. Error codes and endpoint shapes are deferred to API design.

## FR-PUB-001

**Understand Nidhi.** The system shall publicly expose landing, About, how it works, features, benefits and FAQ information with navigation to registration and login.

Acceptance: [US-PUB-001](USER_STORIES.md).

## FR-PUB-002

**Read disclosures and contact information.** The system shall publicly expose Terms, Privacy, Contact information and simulation disclosures; registration and financial screens shall identify the simulation and shall not claim real funds, real gold ownership or guaranteed returns.

Decision references: OD-015.

Acceptance: [US-PUB-002](USER_STORIES.md).

## FR-AUTH-001

**Register a Customer.** The system shall require email and password, accept optional display name, and collect no phone; create only CUSTOMER with zero simulated wallet and gold balances. Reject duplicate normalized email or attempts to grant ADMIN without partial account creation. Direct the Customer to email verification.

Decision references: OD-001, OD-002, OD-010, OD-011.

Acceptance: [US-AUTH-001](USER_STORIES.md).

## FR-AUTH-002

**Login.** The system shall authenticate through ASP.NET Core Identity using secure HttpOnly cookie authentication with no remember-me. Grant customer features only to registered email-verified Customers, and admin features only to provisioned ADMIN identities. Invalid credentials shall not establish access; verification/recovery pathways shall remain available without granting financial access.

Decision references: OD-002, OD-010, OD-018.

Acceptance: [US-AUTH-002](USER_STORIES.md).

## FR-AUTH-003

**Logout.** The system shall end the current authenticated session on logout so it cannot authorize subsequent protected requests; support this for Customer and Administrator.

Decision references: OD-018.

Acceptance: [US-AUTH-003](USER_STORIES.md).

## FR-AUTH-004

**Protect accounts and roles.** The system shall enforce authentication, CUSTOMER/ADMIN roles, email verification for customer features, and Customer ownership in ASP.NET Core. Unverified identities may complete verification/recovery but cannot invoke customer financial APIs. Do not add suspension/status workflows, JWT/localStorage browser authentication, or speculative roles/states.

Decision references: OD-010, OD-018.

Acceptance: [US-AUTH-004](USER_STORIES.md).

## FR-PROFILE-001

**Maintain basic profile.** The system shall return the Customer’s own profile and permit editing display name only. Reject email, password, role, phone or balance changes through the profile operation; email/password changes are separate identity/security concerns.

Decision references: OD-009.

Acceptance: [US-PROFILE-001](USER_STORIES.md).

## FR-WALLET-001

**View simulated wallet.** The system shall display the Customer’s current simulated LKR wallet balance and identify it as having no real cash value.

Acceptance: [US-WALLET-001](USER_STORIES.md).

## FR-WALLET-002

**Add simulated funds.** The system shall accept only eligible verified Customer funding requests from LKR 100.00 to LKR 1,000,000.00 inclusive, within decimal(18,2), leaving no more than LKR 5,000,000.00 in the wallet and no more than 20 successful funding operations per Customer per day. Atomically credit the wallet and persist its completed transaction, balanced LKR ledger evidence and idempotency association; count a committed operation once. Enforce caps under concurrency, use validated configuration where appropriate, and return the receipt/balance without a payment provider.

Decision references: OD-003, OD-005, OD-010, OD-012, OD-016.

Acceptance: [US-WALLET-002](USER_STORIES.md).

## FR-WALLET-003

**Retry simulated funding safely.** The system shall scope the supplied idempotency key by Customer and operation and bind equivalent request content to it. Permanently retain the successful association; return the original successful receipt on equivalent retries without another credit or daily-count increment. Different content with the same key shall conflict. Pre-commit failures may retry safely with the same key; concurrent duplicates must not double-credit.

Decision references: OD-012.

Acceptance: [US-WALLET-003](USER_STORIES.md).

## FR-GOLD-001

**View simulated gold price.** The system shall expose the current immutable simulated LKR/gram price, priceVersionId and publication/effective timestamp, identifying missing or stale pricing. Purchases are disabled without a positive current price or when it is older than 24 hours. Production has no fallback; the first usable price requires successful Administrator publication. Explicit development/test fixtures are permitted.

Decision references: OD-006, OD-017.

Acceptance: [US-GOLD-001](USER_STORIES.md).

## FR-GOLD-002

**Save LKR into simulated gold.** The system shall require a verified Customer, an exact two-decimal LKR amount between 100.00 and 1,000,000.00 inclusive, sufficient wallet funds, an idempotency key and the displayed priceVersionId. At execution require that version to be current, positive and no older than 24 hours; otherwise reject, requiring reconfirmation for price-changed conflicts and never substituting another price. Using C# decimal, calculate gold rounded DOWN to 8 decimal places, reject zero/overflow, and atomically persist the full wallet debit, gold credit, transaction, separate balanced unit books, residual conversion evidence and permanent successful idempotency association.

Decision references: OD-004, OD-005, OD-006, OD-010, OD-012, OD-016.

Acceptance: [US-GOLD-002](USER_STORIES.md).

## FR-GOLD-003

**Receive an accurate saving receipt.** The system shall return and preserve transaction identifier, full LKR debit, exact immutable price/version used, credited gold grams, post-operation wallet/holding balances, timestamp and completed status. Preserve exact amount/price/quantity evidence sufficient to calculate/store the conversion residual for reconciliation/audit without silently rounding it to LKR cents.

Decision references: OD-005, OD-006.

Acceptance: [US-GOLD-003](USER_STORIES.md).

## FR-GOLD-004

**Retry a gold-saving transaction safely.** The system shall scope the idempotency key by Customer and operation, bind equivalent content including amount and priceVersionId, and permanently retain the successful association. Equivalent retries return the original result even after price changes/expiry; changed content conflicts. Pre-commit failures may retry with the same key/content after validations are met. Reconfirmation with a different priceVersionId is changed content and requires a new key. Concurrent distinct requests shall never overspend a shared wallet.

Decision references: OD-012, OD-006.

Acceptance: [US-GOLD-004](USER_STORIES.md).

## FR-HOLDING-001

**View holdings and indicative value.** The system shall show the Customer’s simulated gold quantity and indicative LKR value using the current usable simulated price, with price/time context; price changes shall not change held quantity or historical purchase receipts.

Decision references: OD-005, OD-006.

Acceptance: [US-HOLDING-001](USER_STORIES.md).

## FR-HOLDING-002

**View the customer dashboard.** The system shall show own wallet balance, gold holding, indicative portfolio value, effective simulated price, recent transactions and goal progress; identify unavailable sections and empty states separately.

Decision references: OD-007, OD-008.

Acceptance: [US-HOLDING-002](USER_STORIES.md).

## FR-TRANSACTION-001

**View own transaction history.** The system shall provide paginated own transaction history including simulated funding and gold-saving transactions, stable identifiers, types, statuses, timestamps and relevant amounts in deterministic newest-first order.

Acceptance: [US-TRANSACTION-001](USER_STORIES.md).

## FR-TRANSACTION-002

**Inspect own transaction details.** The system shall return authorized transaction details including type, status, timestamp, amount and resulting balances; gold-saving details additionally include price/version and credited quantity.

Acceptance: [US-TRANSACTION-002](USER_STORIES.md).

## FR-GOAL-001

**Create a savings goal.** The system shall create an owned positive total-gold-gram target representable as decimal(20,8), with optional target date and at most one active goal per Customer. Creating a goal shall neither mutate nor allocate/reserve wallet funds or gold. Reject invalid target/date input or a second active goal, including concurrent attempts.

Decision references: OD-005, OD-007, OD-008.

Acceptance: [US-GOAL-001](USER_STORIES.md).

## FR-GOAL-002

**View savings-goal progress.** The system shall show own goals, gram targets and actual progress as current total simulated gold grams divided by target grams; completion means progress at least 1. Price-only changes shall not change completion. Actual progress may exceed 100%; the indicator may visually cap at 100% without hiding the actual quantity. Missing holding data is unavailable, not zero; no price is needed to compute goal progress.

Decision references: OD-007, OD-008.

Acceptance: [US-GOAL-002](USER_STORIES.md).

## FR-ADMIN-001

**View operational dashboard.** The system shall provide ADMIN-only customer count, completed transaction count and current simulated gold price with timestamps or freshness context; do not imply advanced analytics.

Acceptance: [US-ADMIN-001](USER_STORIES.md).

## FR-ADMIN-002

**Find Customers.** The system shall provide paginated Customer lists and search by stable Customer identifier or normalized email, excluding passwords and verification/reset/session secrets.

Decision references: OD-001.

Acceptance: [US-ADMIN-002](USER_STORIES.md).

## FR-ADMIN-003

**Inspect Customer details.** The system shall provide ADMIN-only Customer email, optional display name, email-verification information, simulated wallet/holding balances and links to transaction history. Do not expose credentials or add a customer suspension/status-management workflow.

Decision references: OD-001, OD-009, OD-010.

Acceptance: [US-ADMIN-003](USER_STORIES.md).

## FR-ADMIN-004

**Inspect transactions and ledger.** The system shall provide paginated transaction lists filterable by Customer identifier and transaction type, transaction details by stable identifier, and associated ledger entries including account, unit, debit/credit amounts and linkage.

Decision references: OD-016.

Acceptance: [US-ADMIN-004](USER_STORIES.md).

## FR-ADMIN-005

**Inspect simulated price history.** The system shall expose the current simulated gold price and paginated retained versions including value, LKR/gram unit, version, effective time and publishing actor.

Decision references: OD-006.

Acceptance: [US-ADMIN-005](USER_STORIES.md).

## FR-ADMIN-006

**Publish a simulated gold price.** The system shall accept an ADMIN-authorized positive decimal(18,4) simulated price and reason, create a new immutable version active immediately, and atomically record its audit evidence. Reject invalid/conflicting updates without overwriting historical versions. Successful initial publication is required in production before any gold-saving transaction.

Decision references: OD-005, OD-006, OD-017.

Acceptance: [US-ADMIN-006](USER_STORIES.md).

## FR-AUDIT-001

**Record material administrative actions.** The system shall record each successful simulated price change with stable event ID, actor ID, action, subject/version ID, timestamp, reason and before/after values; restrict audit data access and never silently overwrite it.

Decision references: OD-013.

Acceptance: [US-AUDIT-001](USER_STORIES.md).

## FR-AUDIT-002

**Review audit logs.** The system shall provide ADMIN-only paginated audit records filterable by actor identifier, action and time interval, with event details; expose no mutation endpoint for financial/audit history.

Decision references: OD-013.

Acceptance: [US-AUDIT-002](USER_STORIES.md).

## FR-AUTH-005

**Verify Customer email.** The system shall provide an email verification flow and require successful verification before customer features, especially simulated funding and gold-saving. Invalid, expired or another account’s verification evidence shall not grant eligibility. Email delivery failure shall expose a safe retry path without bypassing verification.

Decision references: OD-002, OD-010. Acceptance: [US-AUTH-005](USER_STORIES.md).

## FR-AUTH-006

**Recover a password.** The system shall support password recovery through a one-time email reset flow using ASP.NET Core Identity. Valid reset evidence permits a new password; expired, reused, invalid or wrong-account evidence shall not change credentials. Recovery requests shall not disclose whether an email is registered, and shall never expose a password or reset secret in logs/responses outside the intended identity flow.

Decision references: OD-002, OD-018. Acceptance: [US-AUTH-006](USER_STORIES.md).

## FR-AUTH-007

**Provision Administrators operationally.** The system shall restrict ADMIN creation to a controlled operational bootstrap/provisioning process using protected configuration/secrets, with no hard-coded or shared credential and auditable provisioning evidence where practical. Public registration and profile operations shall never grant ADMIN. This is not an admin role-management UI.

Decision references: OD-011. Acceptance: [US-ADMIN-008](USER_STORIES.md).
