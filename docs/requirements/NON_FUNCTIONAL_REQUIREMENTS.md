# Non-functional requirements

These are verifiable v1 release requirements, not claims about the current foundation. See [decisions](OPEN_DECISIONS.md) for unresolved parameters and [roadmap](../ROADMAP.md) for verification gates. Performance figures are provisional acceptance targets; decision approval is required before treating them as final.

## NFR-SEC-001

**Credential storage.** Use ASP.NET Core Identity password hashing, never plaintext/reversible password storage or password logging. Review configuration and storage and test correct/incorrect-password behavior. Work factors remain security-design details, not a different identity mechanism.

## NFR-SEC-002

**Server authorization.** ASP.NET Core shall enforce authenticated cookie sessions, role and ownership checks and verified email before customer features. Test unverified attempts at funding/saving, cross-Customer reads/writes, CUSTOMER calls to admin APIs, and public attempts to assign ADMIN. Verification/recovery access must not imply financial authorization.

## NFR-SEC-003

**Secrets and transport.** Secrets shall use environment-provided configuration and shall not be committed or returned in responses. Before release, scan tracked content and configuration, verify HTTPS for public access, and verify passwords/session credentials do not enter logs.

## NFR-SEC-004

**Errors and session defense.** Use ASP.NET Core Identity with Secure, HttpOnly cookie authentication and no remember-me. Never store browser authentication JWTs in localStorage or design a parallel token login. Specify/test cookie lifetime, SameSite, CSRF and allowed-origin defenses plus logout/expiry invalidation during security design. Verification/reset secrets shall expire and be protected; test invalid/expired verification and invalid/expired/reused/wrong-account one-time reset evidence. Errors shall expose no stack trace, SQL or credentials. Mobile/token authentication is deferred (OD-002/018).

## NFR-SEC-005

**Abuse controls.** Login and registration require documented abuse/rate-limit protections; verification/reset requests also need identity-flow abuse protection. Simulated funding shall enforce LKR 100.00–1,000,000.00 per operation, LKR 5,000,000.00 resulting-balance cap and 20 successful operations per Customer per day under concurrency. Replays and failed pre-commit attempts do not increment the success count. Validate configured limits and fail configuration validation for invalid policy settings rather than scattering constants. Exact request throttles and the daily boundary/timezone require documented design choices; do not substitute those choices for the approved product caps.

## NFR-DATA-001

**Decimal precision.** Use C# decimal, never float/double or JavaScript Number arithmetic for financial results. Use decimal(18,2) for LKR monetary values, decimal(18,4) for simulated price/gram and decimal(20,8) for gold quantities. Gold-saving amounts use two decimal places. Round positive calculated gold DOWN to 8 places, reject zero/overflow and preserve exact amount, price/version and quantity to calculate/store residual without loss. Define exact decimal API transport and residual representation in domain/API design. Test limits, scale errors, zero after rounding and residual reconciliation (OD-005).

## NFR-DATA-002

**Financial atomicity.** Inject a failure at each financial persistence step and verify no partial wallet, holding, completed transaction, ledger or idempotency-success record remains. Simulated funding and gold-saving use this same standard.

## NFR-DATA-003

**Concurrency and idempotency.** Scope keys by Customer and operation, bind equivalent request content (including priceVersionId for saving), and permanently retain successful associations. Test identical concurrent submissions, differing-content conflicts, lost responses, restart, successful replay after price expiry/change and safe same-content retry after pre-commit failure. Different requests must not overspend or exceed funding caps/count. No time-based eviction may permit duplicate successful commands (OD-012).

## NFR-DATA-004

**Historical integrity.** Completed financial records and ledger entries shall have stable identifiers and shall not be silently overwritten/deleted. Verify immutable receipts across later price changes and no v1 mutation endpoint; future corrections require linked compensating records.

## NFR-DATA-005

**Ledger reconciliation.** Maintain separate balanced LKR and gold-gram books linked by business transaction. Test each unit independently; never sum currency against grams. Reconcile wallet/holding changes and exact conversion residual evidence using the conceptual accounts in [data design](../DATABASE_DESIGN.md); detailed entities and posting mechanics belong to Session 3 (OD-005/016).

## NFR-REL-001

**Failure recovery.** A restart after commit but before response shall preserve the financial operation and allow a safe outcome lookup/retry; a crash before commit shall leave no partial effect. Verify both cases with integration tests.

## NFR-REL-002

**Backup and restore.** Before public launch, execute and record a database backup/restore drill and reconciliation on restored data. Document frequency, retention, responsible operator and approved RPO/RTO (OD-013/OD-014). These are launch targets, not an availability SLA.

## NFR-REL-003

**Dependency failures.** When the API/database is unavailable, financial actions shall fail visibly without displaying success; when pricing is unusable, saving and valuation shall show unavailability while known holdings remain accessible if their data source works. Validate fault scenarios.

## NFR-PERF-001

**API responsiveness target.** Proposed launch acceptance target: p95 <= 1 second for ordinary reads and <= 2 seconds for funding/saving at 20 concurrent sessions over a 5-minute test excluding deliberate validation failures. Record hardware, dataset, network, cold-start treatment and results. Confirm or revise workload/targets under OD-014 before release; do not advertise them as an SLA.

## NFR-PERF-002

**Bounded lists.** Customer history and Administrator lists/history/logs shall be paginated with a documented maximum page size and deterministic ordering. Test empty, boundary and multi-page datasets; approve concrete size and load fixture during API design.

## NFR-ACC-001

**Accessible core flows.** Public information, registration/login, email verification, one-time password reset, funding, saving, history and goals shall be keyboard-operable with visible focus, semantic headings, named controls and associated errors. Record keyboard/screen-reader smoke evidence for each flow before launch.

## NFR-ACC-002

**Visual and status accessibility.** Use WCAG 2.2 AA as a design target, without claiming certification. Verify normal text contrast >= 4.5:1, large text >= 3:1, no color-only financial/error cues, 200% text zoom, and announced validation/transaction status messages.

## NFR-UI-001

**Responsive web.** Verify public, Customer and Administrator core tasks at viewport widths 360, 768 and 1280 CSS pixels. No page-level horizontal scrolling or obscured actions; data tables may use a labeled keyboard-accessible internal scroll region.

## NFR-AUDIT-001

**Administrative traceability.** Price publication and its audit evidence commit together; audit failure rolls back publication. Verify actor/action/subject/time/reason/before-after fields and immutable price-version linkage. Controlled Administrator provisioning shall leave auditable evidence where practical (for example operator/time/result) without logging secrets; record the evidence mechanism in operational design. No public audit-edit/delete capability exists.

## NFR-MAINT-001

**Module boundaries.** Keep the documented Api/Application/Domain/Infrastructure references; Domain shall not reference Infrastructure. Review dependency graph and reject controllers or Next.js routes containing financial business logic. No new framework/abstraction without a documented requirement.

## NFR-MAINT-002

**Requirements and tests.** Each functional requirement shall trace to an acceptance story and later automated tests or justified manual evidence. Before merging a feature, run affected xUnit tests and frontend lint/typecheck/build; add integration tests for persistence, concurrency and authorization behavior as those features are introduced.

## NFR-OBS-001

**Structured diagnostics.** Emit structured operation/error logs with correlation identifier, timestamp, severity and operation outcome. Do not log passwords, session tokens, authorization headers or full registration payloads. Verify a failed request can be traced without exposing Customer personal data.

## NFR-OBS-002

**Operational visibility.** Before launch, document an operator-owned method to see production exceptions, health failures and repeated financial errors; demonstrate detection with an injected error. Health responses shall disclose no secrets; process liveness shall not be mistaken for database readiness.

## NFR-PRIV-001

**Data minimization.** Registration collects required email/password and optional display name, never phone. Profile editing is display name only. Do not collect identity documents, bank/card data or speculative eligibility/status fields. Verify forms, contracts, logs and stored fields; only safely hashed passwords are stored through Identity (OD-001/009/010).

## NFR-PRIV-002

**Retention and access.** Exact retention/privacy/deletion periods remain OD-013 and final public legal/eligibility content OD-015. Resolve them before public launch, not as a blocker to conceptual domain/API design. Financial/audit history cannot be silently mutated/deleted; successful financial idempotency associations remain permanent. Test Customer ownership and admin omission of credential secrets; make no unsupported certification/age/geographic claims.

## NFR-OPS-001

**Reproducible delivery.** Before launch, document Docker/Compose startup, environment variables, migrations, CI validation and deployment/rollback steps. Exercise them in a non-production environment, including rollback/recovery without silently losing committed financial records. This session implements none of these tools.
