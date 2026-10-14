# Session 11 — Admin backend and portal

Implemented on the existing `feat/admin-portal` branch. No commit, branch, dependency, migration, schema change or Session 12 work was created.

## Architecture and authorization

AdminController uses the existing AuthPolicies.Admin policy and IAdminService. AdminService lives in Infrastructure and projects persisted EF Core data through Application DTOs; controllers contain no business logic. All nine approved endpoints require authenticated ADMIN, return no-store, and are documented in OpenAPI with cookie security and the publication CSRF header. Anonymous receives 401 and CUSTOMER 403; ADMIN remains rejected from CUSTOMER funding/purchase APIs. Existing cookie authentication, CLI provisioning and public CUSTOMER-only registration are unchanged.

## Operational reads

Dashboard exposes exactly real customer count, completed transaction count and nullable current simulated price/freshness. Missing price is an explicit null state, never a fallback or zero price. No revenue, AUM, fake growth or market analytics were introduced.

Customer summaries expose ID, Identity email, display name, verification and created UTC time; detail adds current persisted simulated wallet/holding only. Profile-based selection excludes operational ADMIN identities. Missing financial projections are unavailable/null rather than invented zero. Customer oversight has no mutation/suspension/impersonation controls. Existing customer-filtered transaction inspection supplies financial history without extending detail with goals or unapproved summary fields.

Transactions expose immutable FinancialTransaction snapshots for WALLET_FUNDING and GOLD_PURCHASE, including original price/version, grams, residual derived from original values and original post-operation balances. Failed pre-commit requests never become history. No reversal/edit routes exist.

Ledger inspection uses only approved `/admin/transactions/{id}/ledger`. The Ledger page accepts a transaction reference and links from transactions. Each bounded operation contains two funding or four purchase postings. Actual persisted account numbers/names, unit/direction, positive exact amount, posting ID/time are shown. LKR and GOLD_GRAMS are separately grouped and decimal-balanced; empty funding gold book is explicit. No global ledger route or mixed-unit total was invented.

## Queries and pagination

Collections default to page 1/size 20; size 1–100 and positive integer page enforced. Offset arithmetic uses long to avoid overflow. Empty/beyond-last pages preserve totals and return empty items; empty history has zero totalPages. Counts and pages are not a snapshot, so concurrent inserts may move page boundaries.

Filtering and DTO projection execute in PostgreSQL; reads use AsNoTracking, deterministic time/UUID ordering and no N+1 customer lookup. Search is literal case-insensitive substring of email or UUID, max 256 characters; display-name/verification filters are not approved. Transaction filters are customerId and exact type only; audit filters actorId and exact action. No date/status/search-engine extensions. Existing indexes support the implemented scope; no migration was justified.

## Price publication, concurrency and audit

POST prices accepts only positive decimal(18,4) price and a nonblank reason up to 500 characters. Scale above four, including trailing zero places, overflow, zero/negative and unsupported client fields are rejected, never silently rounded. Exact JSON decimal numbers and strings are supported; browser submits a string and preserves decimal response tokens without floating-point conversion. Actor, UUIDv7 version and UTC timestamp are server-owned.

One EF SaveChanges PostgreSQL transaction inserts new GoldPrice plus exactly one PUBLISH_PRICE AuditEvent, correlated through audit_log_id/entity version. Audit failure rolls back price insertion. Existing prices are never updated. Concurrent atomic insert publications both remain immutable. All current-price consumers now implement the previously approved `published_at_utc DESC, id DESC` tie-break: admin dashboard/history, customer quote, purchase execution and holdings context. No distributed lock or mutable current flag. Selection follows timestamp/UUID ordering, not request arrival/response order. Purchases retain their existing execution-time price check; no new global purchase lock or stricter publication-versus-commit guarantee is claimed.

Audit browser shows existing persisted events only, deterministically paged. Safe details allow only version/price/reason/admin ID for PUBLISH_PRICE and source for ADMIN_PROVISIONED. Unknown/malformed/non-object metadata is hidden. No cookies, credentials, headers or tokens are collected in publication evidence. Admin-authored reasons are operational free text; UI asks operators not to enter secrets. No historical audit fabrication, editing or deletion. Immutability is enforced by restricted domain state/application paths and absent mutation APIs; privileged SQL is still able to change records as already documented in ERD.

## Frontend and accessibility

Existing ADMIN shell now links Dashboard, Customers, Transactions, Ledger, Gold Prices and Audit Logs, with customer and transaction detail routes. Session roleHome gate prevents CUSTOMER rendering admin content and redirects to Dashboard. Backend remains authoritative.

Responsive structured records become single-column cards at narrow widths. Dark navy/charcoal surfaces, existing gold accents, exact tabular numbers and wrapping IDs preserve the Nidhi brand. Labels, meaningful headings, loading/status/alert regions, keyboard confirmation/cancellation and heading focus after details/publication are included. Price confirmation repeats the exact amount/reason and explains immediate immutable activation and customer PRICE_CHANGED reconfirmation. Pending submission is guarded and disabled. Errors distinguish unavailable data from empty records; refresh/retry is explicit. Uncertain publication transport/server failures direct the operator to inspect history before retrying. No extra frontend application/backend/state library. Shared API client supplies credentials, CSRF, Problem Details and session expiration behavior. No unsupported real-gold/physical custody/return claims.

## Tests and final validation

- Backend restore/build passed, zero build warnings/errors.
- Backend unit: 15 total, 15 passed, 0 failed, 0 skipped (baseline preserved).
- Backend integration: 112 total, 112 passed, 0 failed, 0 skipped; 14 Session 11 PostgreSQL tests added to baseline 98.
- Frontend: 63 total, 63 passed, 0 failed, 0 skipped; 13 Session 11 tests added to baseline 50.
- Frontend lint, typecheck and production build passed. No dependency/lockfile changes.
- Live API `/health`: HTTP 200, `{"status":"ok"}`.
- Real PostgreSQL 18 healthy; existing clean-database migration/constraint regression passes. All test/live databases are disposable, separate from the configured development database.
- Live browser: CLI-provisioned ADMIN login; all six routes/details, customer search, transaction inspection, actual LKR/GRAMS postings, no-price state, excessive price precision rejected, keyboard publication/confirmation and authoritative response/history/audit.
- Actual customer path: fund wallet, load/review A, ADMIN publish B, submit A receives PRICE_CHANGED with unchanged balance; explicitly review/confirm B succeeds. Immutable admin transaction detail retains B after further publications. API integration additionally asserts unchanged holding/history on rejection and original-price snapshot after a later publication.
- Role isolation: CUSTOMER `/admin` redirects to Dashboard without admin content; direct admin API 403; ADMIN funding/purchase API 403. Anonymous rejection and CSRF covered in PostgreSQL tests.
- Screenshots/checks at 320, 390, 768 and 1440px for dashboard, customers/detail, transactions/detail, funding/purchase ledger, prices and audit: no horizontal overflow. Error recovery, focus, reduced motion and no uncaught browser JS errors verified. Semantics/keyboard inspected; no manual screen-reader audit claimed.
- Concurrent API publications both committed; persisted timestamp/UUID selection matches customer/admin/history. Equal-timestamp tie-break tested separately. Test-injected audit insert failure returns safe 500 and leaves no price/audit change.
- Disposable live database and private email/credential state were removed after validation.
- Temporary browser scripts/screenshots are under `/private/tmp/nidhi-session11-*`; no browser dependency added.
- `git diff --check`: passed. Changed/new source reviewed for secrets and unsupported mutations/fallbacks; no credentials introduced. Ignored local credentials were never printed or copied into tracked files.

## Discrepancies and reconciliation

API_CONTRACT's admin customer/detail/transaction DTOs were underspecified; exact minimal fields, missing-record behavior, pagination/search/filter semantics and audit metadata are now documented there. Ledger remains transaction-scoped rather than adding a global API. Current price reads originally omitted the CONCURRENCY §2.4 UUID tie-break; those three existing queries now align with admin selection. Publication accepts a reason because the authoritative contract requires it despite the task's illustrative price-only input. No additional schema is needed; ERD and DATABASE_DESIGN remain unchanged.

The first PostgreSQL run exposed EF filtering after a customer DTO constructor; filtering/order now precede DTO projection and all real PostgreSQL checks pass. Additional test setup was corrected to avoid future timestamps contaminating later tests and to avoid applying string LIKE to jsonb. Browser automation was corrected for existing `Review updated purchase` / `Confirm updated purchase` labels; no customer flow redesign was needed. Screenshot review corrected admin label contrast/confirmation surfaces to existing theme tokens.

Session 11 is ready for review. Suggested Conventional Commit: `feat(admin): implement operational portal and simulated price publication`.

## Exact review inventory

Created files:

- `backend/src/Nidhi.Api/Controllers/AdminController.cs`
- `backend/src/Nidhi.Application/Admin/AdminContracts.cs`
- `backend/src/Nidhi.Infrastructure/Admin/AdminService.cs`
- `backend/tests/Nidhi.IntegrationTests/AdminTests.cs`
- `docs/SESSION_11_REPORT.md`
- `frontend/web/src/app/admin/audit-events/page.tsx`
- `frontend/web/src/app/admin/customers/[id]/page.tsx`
- `frontend/web/src/app/admin/customers/page.tsx`
- `frontend/web/src/app/admin/ledger/page.tsx`
- `frontend/web/src/app/admin/prices/page.tsx`
- `frontend/web/src/app/admin/transactions/[id]/page.tsx`
- `frontend/web/src/app/admin/transactions/page.tsx`
- `frontend/web/src/components/admin/admin-pages.tsx`
- `frontend/web/src/lib/admin/presentation.ts`
- `frontend/web/tests/admin.test.mjs`

Modified files:

- `backend/src/Nidhi.Api/Authentication/AuthOpenApiTransformer.cs`
- `backend/src/Nidhi.Infrastructure/CustomerRecords/CustomerReadService.cs`
- `backend/src/Nidhi.Infrastructure/DependencyInjection.cs`
- `backend/src/Nidhi.Infrastructure/Wallet/WalletService.cs`
- `docs/design/API_CONTRACT.md`
- `docs/design/API_ERRORS.md`
- `docs/design/TRACEABILITY.md`
- `frontend/web/src/app/admin/page.tsx`
- `frontend/web/src/app/globals.css`
- `frontend/web/src/components/layout/application-shell.tsx`
