# Nidhi product-development roadmap

The technical foundation is complete. The current session defines product requirements only. The original two-week plan is a development milestone, not a promise to complete public-launch, operational, regulatory or future real-financial-service requirements in fourteen days. Estimates follow approved scope and capacity; no dates are implied below.

| Step | Milestone | Exit evidence |
|---|---|---|
| 1 | Product requirements | Scope, actors, FR/NFR IDs, stories, flows and decisions reviewed; design-blocking decisions approved |
| 2 | Domain model | Invariants, amount/rounding rules, eligibility, accounting and price/goal semantics agreed |
| 3 | ER diagram | Relationships, constraints, numeric precision, immutable history and idempotency persistence reviewed |
| 4 | API contract | REST/OpenAPI shapes, exact decimal transport, auth/ownership, pagination, errors and retries specified |
| 5 | PostgreSQL + EF Core foundation | Local database/migrations reproducible; rollback and persistence test approach documented |
| 6 | Authentication/authorization | ASP.NET Core Identity cookie login/logout, verification and one-time reset emails, controlled ADMIN provisioning, two roles and ownership tested |
| 7 | First authenticated vertical slice | Registered email-verified Customer can login and retrieve only their own basic profile through web/API/database |
| 8 | Simulated wallet | Zero initial balance, funding receipts/ledger, caps, safe retries, concurrency and failure tests |
| 9 | Simulated gold pricing | Audited price versions, initial publication and effective/unavailable price policy tested |
| 10 | Gold-saving transaction | End-to-end atomic debit/credit/ledger/receipt; rounding, concurrency, retries and faults tested |
| 11 | Holdings and transactions | Dashboard, indicative valuation, history/details and empty/error states reconciled |
| 12 | Savings goals | Approved units/progress/count semantics implemented with acceptance tests |
| 13 | Admin operations | Dashboard, Customer search/details, transaction/ledger inspection, price management and audit review; no extra mutations |
| 14 | Public landing experience | Public content, disclosures, Terms, Privacy, Contact and auth entry points reviewed |
| 15 | Testing and security hardening | FR/story coverage, NFR evidence, accessibility/responsiveness, abuse controls and financial reconciliation reviewed |
| 16 | Docker / Compose | Reproducible local/deployment images and configuration instructions exercised |
| 17 | CI/CD | Required build/lint/type/test gates and controlled deployment steps pass |
| 18 | Deployment | Environment configuration, operator visibility, backup/restore and rollback drill completed |
| 19 | Launch-readiness review | All launch-blocking decisions closed, content/data policy approved, simulation labels verified, evidence accepted by product owner |

Testing and security checks happen throughout; step 15 consolidates evidence rather than deferring all testing. Step 9 may establish backend/admin price publication needed by step 10; step 13 completes the full Administrator experience. Public disclosures can be drafted early, with final experience completed at step 14.

An initial two-week checkpoint should demonstrate progress through agreed requirements/design and a feasible vertical slice, then reassess remaining scope. Incomplete gates delay public release rather than silently reducing integrity/security requirements.

## Post-MVP possibilities

Reassess native mobile, recurring simulation, general product/marketing notifications, advanced analytics and richer administration against demonstrated user needs. Real payments, bank integration, custody/redemption, KYC and live prices require a separately approved scope and operational assessment; they are not automatic next steps. See [MVP scope](requirements/MVP_SCOPE.md).

OD-001–012 and OD-016–018 are approved inputs for later design. OD-013–015 remain launch/deployment gates, not blockers to conceptual domain/API design. Session 3 must elaborate approved policies without inventing account suspension states or unsupported legal eligibility restrictions. Authentication emails are required v1 identity infrastructure, not deferred general notifications.
