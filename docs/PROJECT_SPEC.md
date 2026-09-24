# Nidhi v1 product definition

## Product and problem

Nidhi is a launch-oriented digital gold micro-savings product for people who want to explore saving small amounts of Sri Lankan Rupees (LKR) into fractional gold. V1 is a publicly accessible simulation: it demonstrates the complete customer and operational workflow using simulated LKR funds, simulated gold prices, simulated gold holdings, and simulated financial transactions.

The product hypothesis is that a clear, small-amount workflow and visible progress can make gold-denominated saving easier to understand. This is a hypothesis to validate with users, not a claim of established demand or improved financial outcomes.

## Target users and value proposition

- **Visitor:** a person evaluating how Nidhi works and what the simulation does before registering.
- **Customer:** a registered person exploring small LKR allocations, simulated gold accumulation, and progress toward a savings goal through a responsive web application.
- **Administrator:** an operational user who needs to inspect customer activity, reconcile transactions with ledger entries, manage simulated gold prices, and review audit records.

The value proposition is an understandable path from simulated funding to simulated gold, supported by a visible price, transaction receipt, holdings, and goal progress. Registration eligibility and age policy remain open; the Sri Lankan/LKR context does not imply approval for every jurisdiction.

## Proposed solution and principles

One web product contains a public marketing website, authenticated customer application, and administrative/back-office portal. The ASP.NET Core API owns business rules, authentication, authorization, and persistence.

1. Explain the simulation before registration and at every financial action; labels must not imply real deposits or ownership of real gold.
2. Make the amount, effective price, credited quantity, and resulting balances understandable and traceable.
3. Preserve financial integrity even when values are simulated: decimal arithmetic, atomic changes, safe retries, and immutable history.
4. Collect minimal personal information; protect customer access and enforce Administrator authorization on the server.
5. Prefer the smallest coherent web MVP and explicit decisions over speculative infrastructure.
6. Treat accessibility, error handling, and operational recovery as launch requirements.

## Boundaries and limitations

V1 accepts no real customer money, processes no real payments, purchases or custodies no real gold, and provides no redemption, guaranteed return, capital protection, or regulated investment service. Simulated balances have no cash value and cannot be withdrawn or transferred. Portfolio value is an indicative calculation from a simulated price; it is not a realizable sale value or promise of profit.

Real payment processing or real gold services require a separately authorized product and operational assessment. V1 does not claim regulatory certifications. Terms, Privacy, and Contact pages must describe the actual simulation and data practices; their final content and ownership are launch decisions.

React Native, recurring deductions, KYC/document verification, general product/marketing notifications, and live market feeds are post-MVP. Email verification and one-time password-reset emails are required identity infrastructure in v1. There is no simulation of document collection merely to imitate a regulated service.

## Actors and authorization

| Actor | Authentication | V1 capabilities |
|---|---|---|
| Visitor | None | Landing, About, features/benefits, how it works, FAQ, disclosures, Terms, Privacy, Contact; register or login |
| Customer | Authenticated role `CUSTOMER` | Logout, dashboard, own simulated wallet funding, simulated gold price, gold-saving transaction, own holdings/portfolio/history/details, savings goal/progress, basic profile |
| Administrator | Authenticated role `ADMIN` | Login/logout, admin dashboard, customer list/search/details, all transaction details and related ledger entries, current simulated gold price/history/update, audit logs |

Visitor is not a stored authorization role. Only `CUSTOMER` and `ADMIN` exist in v1; public registration cannot grant `ADMIN`. An Administrator's role alone does not authorize customer financial actions on another person's behalf. Admin mutations are limited to simulated gold-price updates; no balance editing, role management UI, customer suspension UI, deletion of history, or manual correction tool is in v1. ADMIN provisioning uses a controlled operational process and protected configuration/secrets, never shared/hard-coded credentials, with audit evidence where practical. Customer registration collects email/password and optional display name, not phone; only display name is profile-editable. Registered email-verified Customers may access customer features; verification/recovery remains reachable before that gate. No suspension/status workflows are modeled. Web authentication uses ASP.NET Core Identity and Secure HttpOnly cookies, with no remember-me or JWT/localStorage authentication.

## Scope and requirement authority

The completed implementation is still the technical foundation: minimal frontend, API health endpoint, OpenAPI, and tests. The documents below define future v1 behavior, not features already implemented.

- [MVP scope](requirements/MVP_SCOPE.md)
- [Functional requirements](requirements/FUNCTIONAL_REQUIREMENTS.md)
- [Non-functional requirements](requirements/NON_FUNCTIONAL_REQUIREMENTS.md)
- [User stories and acceptance criteria](requirements/USER_STORIES.md)
- [Gold-saving flow](requirements/GOLD_SAVING_FLOW.md)
- [Simulated wallet flow](requirements/SIMULATED_WALLET_FLOW.md)
- [Open decisions and agreed boundaries](requirements/OPEN_DECISIONS.md)
- [Architecture](ARCHITECTURE.md), [conceptual data design](DATABASE_DESIGN.md), [roadmap](ROADMAP.md)
- [Documentation audit](requirements/DOCUMENTATION_AUDIT.md)

AGENTS.md governs engineering discipline. Scope and DECIDED entries govern v1 boundaries; stable FR/NFR IDs and their acceptance criteria govern behavior. OPEN recommendations are proposals, not permission to implement defaults. OD-001–012 and OD-016–018 are approved. Remaining OD-013–015 govern launch/deployment and do not block conceptual domain/API design; resolve them at their stated gates. If documents conflict, record and resolve the conflict rather than choosing silently.

## Assumptions to validate

Customers have access to a modern web browser and can understand the chosen launch language. Manual Administrator-managed simulated pricing is sufficient for v1. A single LKR currency and gold grams are sufficient. All three experiences can share one Next.js app. English-first content is a working assumption. Exact traffic/hosting/recovery objectives, retention/privacy/deletion periods and public age/geographic/legal policies remain OD-013–015 for launch/deployment review. Do not encode unapproved eligibility claims into the domain model or promise an unsupported SLA.
