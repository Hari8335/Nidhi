# User stories and acceptance criteria

Each story traces to one or more functional requirements. Shared criteria are mandatory in addition to the story-specific scenarios. Approved rules in the [decision log](OPEN_DECISIONS.md) apply below. Only OD-013–015 remain open for launch/deployment; design details explicitly listed in that log must receive test values before implementation.

## Shared acceptance criteria

- Given a protected Customer operation, when a Visitor or unverified Customer calls it or another Customer’s identifier is supplied, then access is denied and protected data/state remain unchanged.
- Given an Administrator operation, when a Visitor or Customer calls its API directly, then access is denied regardless of frontend route visibility.
- Given a read fails, when its screen renders, then an accessible error/retry state appears; an error is not represented as an empty success.
- Given validation fails, when input is returned for correction, then no partial mutation occurs and errors identify the correction without secrets/internal traces.
- Given any financial input/output, when shown, then LKR/gram units and simulation labels are explicit.
- Given a financial result is uncertain after a timeout, when retrying, then the same idempotency identity is used until its outcome is resolved.

## Visitor

### US-PUB-001 — Understand Nidhi

As a Visitor, I want to understand Nidhi, so that I can evaluate and enter the simulation.

Requirement: [FR-PUB-001](FUNCTIONAL_REQUIREMENTS.md#fr-pub-001).

Acceptance criteria:

- Given a Visitor opens the public site, when navigating its information, then each named topic and registration/login entry point is reachable without authentication.
- Given public content cannot load, when the page is requested, then an accessible error and retry/navigation option replace blank content.

### US-PUB-002 — Read disclosures and contact information

As a Visitor, I want to read disclosures and contact information, so that I can evaluate and enter the simulation.

Requirement: [FR-PUB-002](FUNCTIONAL_REQUIREMENTS.md#fr-pub-002). Decision references: OD-015.

Acceptance criteria:

- Given a Visitor evaluates registration, when opening disclosures, then the simulation limits, published Terms/Privacy and contact information are available without signing in.
- Given a disclosure page cannot load, when registration is attempted, then any required acknowledgement cannot be silently treated as accepted.

### US-AUTH-001 — Register a Customer

As a Visitor, I want to register a Customer, so that I can evaluate and enter the simulation.

Requirement: [FR-AUTH-001](FUNCTIONAL_REQUIREMENTS.md#fr-auth-001). Decision references: OD-001, OD-002, OD-010, OD-015, OD-018.

Acceptance criteria:

- Given a unique valid email/password with or without display name, when registration succeeds, then exactly one CUSTOMER account exists with zero wallet/holding balances and verification instructions, without collecting phone.
- Given missing/invalid required details, duplicate normalized email, phone or a requested ADMIN role, when submitted, then no duplicate/elevated account or unauthorized fields are created and a safe validation error is returned.

## Customer

### US-AUTH-005 — Verify email

As a Customer, I want to verify my registered email, so that I can access customer features and simulated financial operations.

Requirement: [FR-AUTH-005](FUNCTIONAL_REQUIREMENTS.md#fr-auth-005). Decision references: OD-002, OD-010.

Acceptance criteria:

- Given valid verification evidence for my registered email, when verified, then verification-based eligibility is granted without any wallet/holding mutation.
- Given unverified email, when customer financial APIs are called directly, then access is denied regardless of UI visibility.
- Given invalid, expired or another account’s evidence, when verification is attempted, then no account is incorrectly verified.
- Given identity-email delivery failure, when verification is requested, then a safe retry path is shown and verification is not bypassed.

### US-AUTH-006 — Recover a password

As a Customer who cannot login, I want a one-time email password reset, so that I can regain access without exposing my credentials.

Requirement: [FR-AUTH-006](FUNCTIONAL_REQUIREMENTS.md#fr-auth-006). Decision references: OD-002, OD-018.

Acceptance criteria:

- Given a registered email, when recovery is requested, then the identity reset flow sends one-time reset evidence to that address and returns a non-enumerating response.
- Given valid unused reset evidence and a valid new password, when reset succeeds, then the new password authenticates and the previous password does not.
- Given reused, expired, invalid or wrong-account reset evidence, when submitted, then credentials remain unchanged.
- Given an unknown email, when recovery is requested, then the public response does not reveal registration status.
- Given recovery without email verification, when reset completes, then it does not silently bypass the required verification gate for customer features.


### US-AUTH-002 — Login

As a Customer, I want to login, so that I can understand and control my simulated saving activity.

Requirement: [FR-AUTH-002](FUNCTIONAL_REQUIREMENTS.md#fr-auth-002). Decision references: OD-010, OD-018.

Acceptance criteria:

- Given a registered email-verified Customer and valid credentials, when login succeeds, then protected access uses a Secure HttpOnly cookie with no remember-me or JWT/localStorage authentication.
- Given invalid credentials, when login is attempted, then protected access is denied without credential disclosure.
- Given an unverified registration, when customer features or financial APIs are requested, then access is denied and verification/recovery remains reachable without inventing a suspension/status workflow.

### US-AUTH-003 — Logout

As a Customer, I want to logout, so that I can understand and control my simulated saving activity.

Requirement: [FR-AUTH-003](FUNCTIONAL_REQUIREMENTS.md#fr-auth-003). Decision references: OD-018.

Acceptance criteria:

- Given an authenticated session, when logout completes, then replaying that session against a protected operation is rejected.
- Given an expired session, when logout is requested, then the user is returned to a signed-out state without exposing an internal error.

### US-AUTH-004 — Protect accounts and roles

As a Customer, I want to protect accounts and roles, so that I can understand and control my simulated saving activity.

Requirement: [FR-AUTH-004](FUNCTIONAL_REQUIREMENTS.md#fr-auth-004).

Acceptance criteria:

- Given Customer A is signed in, when accessing A’s records, then access is permitted according to the operation.
- Given a Visitor, Customer B, or Customer invoking an admin operation, when calling a protected API directly, then unauthorized access is denied without exposing the protected data.

### US-PROFILE-001 — Maintain basic profile

As a Customer, I want to maintain basic profile, so that I can understand and control my simulated saving activity.

Requirement: [FR-PROFILE-001](FUNCTIONAL_REQUIREMENTS.md#fr-profile-001). Decision references: OD-009.

Acceptance criteria:

- Given a verified Customer supplies a valid display name, when saved, then only that name changes.
- Given email, password, phone, role or balance edits through profile input, when submitted, then the operation rejects disallowed fields without those mutations.

### US-WALLET-001 — View simulated wallet

As a Customer, I want to view simulated wallet, so that I can understand and control my simulated saving activity.

Requirement: [FR-WALLET-001](FUNCTIONAL_REQUIREMENTS.md#fr-wallet-001).

Acceptance criteria:

- Given a registered and email-verified Customer with no funding, when viewing the wallet, then the simulated LKR balance is zero.
- Given wallet retrieval fails, when opening the wallet, then unavailable data is not shown as a false zero.

### US-WALLET-002 — Add simulated funds

As a Customer, I want to add simulated funds, so that I can understand and control my simulated saving activity.

Requirement: [FR-WALLET-002](FUNCTIONAL_REQUIREMENTS.md#fr-wallet-002). Decision references: OD-003, OD-005, OD-010, OD-012, OD-016.

Acceptance criteria:

- Given an eligible verified Customer below all caps, when adding LKR 100.00 or LKR 1,000,000.00, then the inclusive boundary amount commits once with receipt/ledger evidence.
- Given LKR 99.99 or 1,000,000.01, or an amount not representable as decimal(18,2), when funding is submitted, then it is rejected with no partial financial state.
- Given a balance of LKR 4,999,900.00 and fewer than 20 successes that day, when funding LKR 100.00, then the resulting LKR 5,000,000.00 balance is accepted; a result above that cap is rejected.
- Given 19 successes in the configured day, when two new funding commands race, then at most one more succeeds; the 21st success is never committed. Day-boundary tests use the explicit timezone/boundary specified during design.
- Given a write failure or unverified Customer, when funding is attempted, then wallet, completed receipt, ledger and successful daily count remain unchanged.

### US-WALLET-003 — Retry simulated funding safely

As a Customer, I want to retry simulated funding safely, so that I can understand and control my simulated saving activity.

Requirement: [FR-WALLET-003](FUNCTIONAL_REQUIREMENTS.md#fr-wallet-003). Decision references: OD-012.

Acceptance criteria:

- Given a successful funding command, when equivalent content and the same Customer/operation/key are retried after response loss or restart, then the original receipt returns without another credit or daily-count increment, even if current caps would block a new command.
- Given a key bound to one amount, when reused with different content, then a conflict occurs.
- Given failure before financial commit, when the same key/content is retried after the failure is resolved, then it may commit once; the prior failure consumed no successful-operation allowance.

### US-GOLD-001 — View simulated gold price

As a Customer, I want to view simulated gold price, so that I can understand and control my simulated saving activity.

Requirement: [FR-GOLD-001](FUNCTIONAL_REQUIREMENTS.md#fr-gold-001). Decision references: OD-006, OD-017.

Acceptance criteria:

- Given a published positive current price, when read, then priceVersionId, exact LKR/gram value and publication/effective time are shown.
- Given production has no published price or the current price is older than 24 hours, when saving is attempted, then it is unavailable with no fallback; explicit development/test fixtures do not authorize a production default.
- Given a price exactly 24 hours old, when execution validates age, then age alone does not reject it; when older than 24 hours, it does.

### US-GOLD-002 — Save LKR into simulated gold

As a Customer, I want to save LKR into simulated gold, so that I can understand and control my simulated saving activity.

Requirement: [FR-GOLD-002](FUNCTIONAL_REQUIREMENTS.md#fr-gold-002). Decision references: OD-004, OD-005, OD-006, OD-010, OD-012, OD-016.

Acceptance criteria:

- Given a verified Customer, sufficient funds and a current fresh displayed priceVersionId, when saving LKR 100.00 or LKR 1,000,000.00, then the inclusive amount commits atomically with gold rounded DOWN to 8 places.
- Given LKR 99.99, LKR 1,000,000.01, more than two nonzero decimal places, insufficient funds, unverified email, zero-rounded gold or overflow, when submitted, then no financial mutation commits.
- Given LKR 100.00 and simulated price 30000.0000 per gram, when executed, then 0.00333333 grams are credited and LKR 0.000100000000 residual is exactly derivable, while the full LKR 100.00 is debited.
- Given the submitted priceVersionId was replaced before execution, when saving is attempted, then a price-changed conflict requires reconfirmation; no newer price is silently used.
- Given a price older than 24 hours or failure at any financial persistence step, when executing, then no partial wallet, holding, transaction, ledger or successful idempotency state remains.

### US-GOLD-003 — Receive an accurate saving receipt

As a Customer, I want to receive an accurate saving receipt, so that I can understand and control my simulated saving activity.

Requirement: [FR-GOLD-003](FUNCTIONAL_REQUIREMENTS.md#fr-gold-003). Decision references: OD-005, OD-006.

Acceptance criteria:

- Given a committed save, when reopened after price publication or expiry, then the exact original price/version, debit, rounded quantity and original post-operation balances remain unchanged, with sufficient conversion evidence for residual reconciliation.
- Given a failed save, when shown, then no completed receipt is fabricated.

### US-GOLD-004 — Retry a gold-saving transaction safely

As a Customer, I want to retry a gold-saving transaction safely, so that I can understand and control my simulated saving activity.

Requirement: [FR-GOLD-004](FUNCTIONAL_REQUIREMENTS.md#fr-gold-004). Decision references: OD-012.

Acceptance criteria:

- Given a successful save and permanent Customer/operation/key association, when equivalent content is retried after restart, changed price or stale price, then the original receipt returns without new execution or debit.
- Given the same key with a different amount or priceVersionId, when submitted, then a conflict occurs; reconfirming a new version uses a new key.
- Given failure before commit, when the same key/content is retried, then it may succeed if current execution validations pass.
- Given concurrent distinct requests exceed shared funds in total, when processed, then only affordable operations commit and the wallet never becomes negative.

### US-HOLDING-001 — View holdings and indicative value

As a Customer, I want to view holdings and indicative value, so that I can understand and control my simulated saving activity.

Requirement: [FR-HOLDING-001](FUNCTIONAL_REQUIREMENTS.md#fr-holding-001). Decision references: OD-005, OD-006.

Acceptance criteria:

- Given a known holding and usable price, when portfolio value is displayed, then it equals holding multiplied by price under the approved display-rounding policy.
- Given no usable price, when holdings are displayed, then known gold quantity remains visible but valuation is unavailable rather than zero.

### US-HOLDING-002 — View the customer dashboard

As a Customer, I want to view the customer dashboard, so that I can understand and control my simulated saving activity.

Requirement: [FR-HOLDING-002](FUNCTIONAL_REQUIREMENTS.md#fr-holding-002). Decision references: OD-007, OD-008.

Acceptance criteria:

- Given recorded activity, when opening the dashboard, then summaries agree with the underlying Customer records and disclose the price used for valuation.
- Given no activity or no goal, when opening the dashboard, then meaningful zero/empty states appear; an API failure is not rendered as a fabricated zero.

### US-TRANSACTION-001 — View own transaction history

As a Customer, I want to view own transaction history, so that I can understand and control my simulated saving activity.

Requirement: [FR-TRANSACTION-001](FUNCTIONAL_REQUIREMENTS.md#fr-transaction-001).

Acceptance criteria:

- Given multiple transactions, when paging through history with a fixed dataset, then each is reachable with no missing or duplicate rows.
- Given no transactions, when history opens, then an explicit empty state appears; another Customer’s records are never included.

### US-TRANSACTION-002 — Inspect own transaction details

As a Customer, I want to inspect own transaction details, so that I can understand and control my simulated saving activity.

Requirement: [FR-TRANSACTION-002](FUNCTIONAL_REQUIREMENTS.md#fr-transaction-002).

Acceptance criteria:

- Given an owned transaction identifier, when details are requested, then they match its durable receipt.
- Given an unknown or another Customer’s identifier, when details are requested, then protected details are not disclosed.

### US-GOAL-001 — Create a savings goal

As a Customer, I want to create a savings goal, so that I can understand and control my simulated saving activity.

Requirement: [FR-GOAL-001](FUNCTIONAL_REQUIREMENTS.md#fr-goal-001). Decision references: OD-005, OD-007, OD-008.

Acceptance criteria:

- Given no active goal, when a positive decimal(20,8) total-gold target with or without a valid target date is created, then one owned active goal appears without allocating/reserving funds or gold.
- Given an active goal already exists, two creations race, or the target is nonpositive/unrepresentable, when creating, then a second active goal or invalid goal is rejected without financial mutation.

### US-GOAL-002 — View savings-goal progress

As a Customer, I want to view savings-goal progress, so that I can understand and control my simulated saving activity.

Requirement: [FR-GOAL-002](FUNCTIONAL_REQUIREMENTS.md#fr-goal-002). Decision references: OD-007, OD-008.

Acceptance criteria:

- Given target 1.00000000 grams and holding 0.25000000 grams, when progress is read, then it is 25% regardless of current simulated price.
- Given target 1.00000000 grams and holding 1.20000000 grams, when read, then actual progress is 120% and complete; the indicator may cap at 100% while retaining actual quantity/progress.
- Given only price changes or becomes unavailable, when goal progress is recalculated, then progress/completion is unchanged if holding data is available.
- Given no goal or unavailable holding data, when progress is requested, then an empty/unavailable state is distinct from zero progress.

## Administrator

### US-ADMIN-007 — Authenticate as an Administrator

As an Administrator, I want to login and logout of the administrative experience, so that I can inspect and operate the simulation with my authorized identity.

Requirements: [FR-AUTH-002](FUNCTIONAL_REQUIREMENTS.md#fr-auth-002), [FR-AUTH-003](FUNCTIONAL_REQUIREMENTS.md#fr-auth-003), [FR-AUTH-004](FUNCTIONAL_REQUIREMENTS.md#fr-auth-004). Decision references: OD-011, OD-018.

Acceptance criteria:

- Given a provisioned eligible Administrator with valid credentials, when login succeeds, then the ADMIN experience is available and no Customer impersonation permission is implied.
- Given invalid credentials or a Customer session, when admin login/access is attempted, then administrative access is denied without revealing credential details.
- Given an authenticated Administrator, when logout completes, then the prior session cannot call administrative APIs.
- Given no provisioned Administrator, when public registration is used, then it cannot grant ADMIN; provisioning follows the separately approved operational procedure.

### US-ADMIN-001 — View operational dashboard

As an Administrator, I want to view operational dashboard, so that I can operate and inspect the simulation responsibly.

Requirement: [FR-ADMIN-001](FUNCTIONAL_REQUIREMENTS.md#fr-admin-001).

Acceptance criteria:

- Given an Administrator and known records, when opening the dashboard, then counts and the price match their documented data scope.
- Given a Visitor or Customer, when requesting dashboard data directly, then server-side authorization denies access.

### US-ADMIN-002 — Find Customers

As an Administrator, I want to find Customers, so that I can operate and inspect the simulation responsibly.

Requirement: [FR-ADMIN-002](FUNCTIONAL_REQUIREMENTS.md#fr-admin-002). Decision references: OD-001.

Acceptance criteria:

- Given a known Customer, when searching by their identifier, then the matching account is reachable.
- Given no match or no Customers, when searching/listing, then an empty state appears; a non-ADMIN request is denied.

### US-ADMIN-003 — Inspect Customer details

As an Administrator, I want to inspect Customer details, so that I can operate and inspect the simulation responsibly.

Requirement: [FR-ADMIN-003](FUNCTIONAL_REQUIREMENTS.md#fr-admin-003). Decision references: OD-001, OD-009, OD-010.

Acceptance criteria:

- Given an existing Customer, when inspected by ADMIN, then email, optional display name, verification information and reconciled balances are available without credentials or a suspension/status workflow.
- Given an unknown Customer or non-ADMIN caller, when inspected, then a safe not-found/access-denied result appears without internal errors or protected data.

### US-ADMIN-004 — Inspect transactions and ledger

As an Administrator, I want to inspect transactions and ledger, so that I can operate and inspect the simulation responsibly.

Requirement: [FR-ADMIN-004](FUNCTIONAL_REQUIREMENTS.md#fr-admin-004). Decision references: OD-016.

Acceptance criteria:

- Given a completed gold-saving transaction, when an Administrator inspects its ledger, then linked LKR entries balance within LKR and linked gold entries balance within grams, with conversion/residual evidence; the units are never added together.
- Given a funding transaction, when inspected, then its balanced LKR funding entries are available and no gold credit is fabricated.
- Given no match or a non-ADMIN caller, when querying, then an empty/not-found or denied result appears as appropriate.

### US-ADMIN-005 — Inspect simulated price history

As an Administrator, I want to inspect simulated price history, so that I can operate and inspect the simulation responsibly.

Requirement: [FR-ADMIN-005](FUNCTIONAL_REQUIREMENTS.md#fr-admin-005). Decision references: OD-006.

Acceptance criteria:

- Given multiple published versions, when an Administrator opens history, then prior versions remain inspectable.
- Given no published price, when history opens, then an empty state appears; non-ADMIN requests are denied.

### US-ADMIN-006 — Publish a simulated gold price

As an Administrator, I want to publish a simulated gold price, so that I can operate and inspect the simulation responsibly.

Requirement: [FR-ADMIN-006](FUNCTIONAL_REQUIREMENTS.md#fr-admin-006). Decision references: OD-005, OD-006, OD-017.

Acceptance criteria:

- Given authorized positive decimal(18,4) price and reason, when publication commits, then a new immutable version activates immediately with its audit record; older versions and purchase receipts remain unchanged.
- Given the first successful production publication, when a fresh matching-version purchase is submitted, then price availability no longer blocks it.
- Given a nonpositive/unrepresentable price, missing reason, update conflict, non-ADMIN caller or audit-write failure, when publishing, then no partial publication or history overwrite occurs.

### US-AUDIT-001 — Record material administrative actions

As an Administrator, I want to record material administrative actions, so that I can operate and inspect the simulation responsibly.

Requirement: [FR-AUDIT-001](FUNCTIONAL_REQUIREMENTS.md#fr-audit-001). Decision references: OD-013.

Acceptance criteria:

- Given a committed price update, when its audit evidence is inspected, then all required fields identify the exact change.
- Given a denied price-change attempt, when logged for security visibility, then it is not misrepresented as a successful price update and contains no secret credentials.

### US-AUDIT-002 — Review audit logs

As an Administrator, I want to review audit logs, so that I can operate and inspect the simulation responsibly.

Requirement: [FR-AUDIT-002](FUNCTIONAL_REQUIREMENTS.md#fr-audit-002). Decision references: OD-013.

Acceptance criteria:

- Given a recorded price change, when filtered by its actor/action/time, then the event and details are returned.
- Given no matches, when logs are queried, then an empty state appears; non-ADMIN access and history mutation requests are denied.

### US-ADMIN-008 — Provision an Administrator operationally

As an Administrator responsible for setup, I want controlled operational provisioning, so that ADMIN access cannot be self-assigned through the public product.

Requirement: [FR-AUTH-007](FUNCTIONAL_REQUIREMENTS.md#fr-auth-007). Decision reference: OD-011.

Acceptance criteria:

- Given an authorized operational provisioning process and protected configuration/secrets, when an Administrator is created, then a unique credential is used and provisioning evidence is retained where practical without exposing secrets.
- Given missing/invalid protected provisioning configuration, when setup is attempted, then no hard-coded or shared fallback credential is used.
- Given public registration or profile input requesting ADMIN, when submitted, then no elevation occurs.
