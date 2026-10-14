# Session 9 — Customer holdings and transaction history

Implemented on `feat/transactions-holdings`. No commit, new branch, dependency or migration. Session 10 is not started. Financial mutation logic and database schema remain unchanged.

## Backend reads and ownership

- `GET /api/v1/customer/holdings`: reads the authenticated customer's `GoldHolding.QuantityGrams` directly, retaining decimal(20,8). Current usable price supplies a display-only indicative simulation value. Wallet balance comes from the existing `/customer/wallet` endpoint and persisted `Wallet`.
- `GET /api/v1/customer/transactions`: PostgreSQL ownership predicate, `AsNoTracking`, count and bounded direct DTO projection, ordered by `CreatedAtUtc DESC, TransactionId DESC`. Defaults page 1 / size 20, size maximum 100. Nonpositive, malformed or out-of-range inputs produce 400 `VALIDATION_ERROR`; beyond-last pages return empty items with actual totals. Empty history has zero total pages. Large offsets use long arithmetic and never overflow into another page.
- `GET /api/v1/customer/transactions/{id}`: ownership predicate is included in the database query. Unknown and foreign-owned IDs return the same 404 `RESOURCE_NOT_FOUND` / "Transaction not found.". Funding returns the original funding receipt shape; purchase returns the original purchase receipt shape.

All three endpoints use `AuthPolicies.VerifiedCustomer`, deriving ownership solely from the principal. Anonymous, unverified CUSTOMER and ADMIN are rejected according to existing error conventions. GETs require no CSRF header and successful responses are not cached. Forged customer identifiers have no authority. Ledger/account rows, customer ownership IDs, idempotency keys/hashes and internal audit data are not exposed.

History uses `FinancialTransaction`, not a reconstruction from current balances or prices. Purchase detail retains applied price, priceVersionId, exact credited grams, post-wallet/post-holding snapshots and UTC timestamp. Residual is derived from original immutable amount, grams and applied price. The only financial status is the persisted `COMPLETED` status.

Dashboard activity and history share the same projection and ordering; the dashboard takes five records. Existing wallet and dashboard read policies are preserved. Existing `(customer_id, created_at_utc DESC)` and primary-key indexes support these queries. No entity graphs, N+1 queries, global-table in-memory filtering, speculative index or reporting framework were added. Offset pagination reflects current records; new commits can shift later pages. Count and page queries are separate reads, not a cross-request snapshot guarantee.

## Contract reconciliations

`API_CONTRACT.md`, `API_ERRORS.md` and `TRACEABILITY.md` now document:

- Verified-CUSTOMER authorization for the new reads, while preserving the earlier Customer-policy dashboard/wallet/price exceptions.
- Deterministic newest-first order with ID tie-break, invalid/beyond-last page semantics and empty totals. No customer history filters or alternate sorting are defined or implemented.
- `status` and original applied price in the shared history/dashboard item DTO, reconciling the incomplete example with FR-TRANSACTION-001.
- Explicit receipt field shapes and identical not-found behavior without existence disclosure.
- Indicative value uses holding × fresh current price, decimal rounding to cents with midpoint-to-even for presentation only. Missing price retains quantity with null price/version/valuation; stale price retains context but has null valuation. No cost basis, gains, returns or historical current-price substitution.

The older README and historical reports describe earlier implementation stages; they were not broadly rewritten. Existing design discrepancies outside this read slice, including older simulated-funding terminology and price-change error examples, were not used to change established mutations.

## Customer experience

`/gold` remains the holdings surface and existing purchase destination. It now shows exact eight-decimal simulated grams, real wallet balance, current indicative simulation value or explicit unavailable/stale valuation, current price version, recent activity and full-history navigation. It refreshes after a completed purchase and supports manual holdings refresh.

`/transactions` provides chronological customer cards, exact funding/purchase labels, localized timestamps, LKR amounts, eight-decimal purchase quantities, wrapping full references and receipt links. Previous/next/first-page controls follow the API pagination model; no unsupported filters, exports or financial operations were introduced. Desktop Transactions and mobile Activity navigation are active.

`/transactions/[id]` displays the authoritative original receipt as a semantic definition list, with complete UTC timestamp, balances and purchase price/version/grams/residual where appropriate. Native links provide keyboard navigation without modal focus traps. Pagination returns focus to the heading. Loading/status/error regions, named pagination controls, safe Problem Details messages and retry paths support customer access. Empty history guides to simulated wallet funding; no sample transactions appear.

Dashboard activity reuses the same list and receipt links. Its hardcoded goal numbers were removed. A live check discovered the pre-existing wallet/dashboard refresh unmounted the funding modal, clearing a successful receipt; the modal now remains mounted while summaries refresh. No funding request, idempotency or purchase rules changed. Responsive balance cards now accommodate full eight-decimal values at 320px.

Shared currency/gold formatters are reused. `allowImportingTsExtensions` is enabled with the existing `noEmit` configuration so Node's native TypeScript test runner can use the same presentation module and formatters as the Next.js UI. No runtime/test dependency was added.

## Automated validation

- New `CustomerReadTests`: 18 real PostgreSQL integration tests, all passing. Coverage includes zero persisted state; multiple-purchase holding/wallet consistency; authoritative projection rather than history reconstruction; missing/stale valuation; immutable funding and Price A receipts after Price B; two-customer isolation and forged identifiers; owned/foreign/unknown detail; privacy; deterministic tie ordering; multi-page/default/maximum/huge-offset/invalid pagination; dashboard consistency; anonymous/unverified/admin denial; no-CSRF/no-store reads.
- Full backend unit suite: 9 total, 9 passed, 0 failed, 0 skipped.
- Full backend integration suite: 88 total, 88 passed, 0 failed, 0 skipped (Session 8 baseline 70 + 18). Sessions 7–8 funding/purchase and auth regressions remain passing.
- Backend build: passed with 0 warnings and 0 errors.
- New frontend `transactions.test.mjs`: 8 tests for exact labels, shared formatting, original funding/purchase receipt fields, full references/UTC timestamp, original applied price/version/quantity, residual, pagination query construction/validation, empty DTO, credentials without GET CSRF, and safe not-found/authorization errors.
- Full frontend suite: 42 total, 42 passed, 0 failed, 0 skipped (baseline 34 + 8).
- Frontend lint, strict typecheck and production Webpack build: passed; `/transactions` and dynamic `/transactions/[id]` included.

The first PostgreSQL run caught ASP.NET query prefix binding when the action parameter was called `page`. Renaming it `pagination` restored actual page/size binding and validation; the full PostgreSQL rerun passed. Initial sandbox build processes stalled on process communication; supported execution completed successfully. One overlapping build caused a shared manifest-file conflict; sequential build passed. Neither condition was diagnosed as an application failure.

## Live validation and review

Live Chrome validation used the built frontend, Kestrel API and a newly migrated disposable PostgreSQL database. It passed registration/private pickup verification/login, empty history and zero holdings, simulated LKR 1,250 funding with the receipt surviving summary refresh, keyboard-confirmed LKR 100 purchase, both history rows and owned details, complete timestamps, original Price A (LKR 30,000.0000/g) after seeding Price B (LKR 40,000.0000/g), unchanged 0.00333333 g holding, LKR 1,150.00 wallet and LKR 133.33 current indicative simulated value. Refresh preserved history and dashboard activity matched the same records. Nineteen further allowed simulated fundings created a real 21-record history; 20/1 rows across two pages, previous/next navigation and heading focus passed.

Gold, history (including empty state), both receipt types and dashboard were checked at 320, 390, 768 and 1440 CSS pixels with no horizontal overflow. Screenshots of the final 320px history and 390px purchase receipt were visually inspected; full references wrap and eight-decimal quantities remain readable. Native receipt-link keyboard activation and pagination keyboard/focus checks passed. Browser page-error collection was empty. Not-found behavior was live; loading, service-error and retry recovery used an explicitly intercepted GET only, not fabricated financial success data. No screen-reader certification is claimed.

Screenshots are retained in `/private/tmp/nidhi-session9-browser/`; the temporary automation uses the already available external Playwright installation, not a repository dependency. The disposable database, private identity-email pickup and task-started servers were cleaned up after validation. The existing PostgreSQL development container/volume were retained.

Live `GET /health`: HTTP 200 with `{"status":"ok"}`. `git diff --check`: passed. Changed and new content was reviewed for private keys, credential/token patterns and local ignored credential values; no new secrets found. Whole-file substring checks matched only pre-existing illustrative API-error examples, not new credential assignments. `.env` remains ignored and untracked. Customer financial UI uses persisted facts or explicit unavailable/empty states, with no fabricated holdings, balances, transactions, performance percentages or market trends. No physical ownership, purity, delivery, profit or guaranteed-return claim was introduced.

Session 9 is ready for review on the existing branch. No commit was created.

## Exact review inventory

Created files:

- `backend/src/Nidhi.Api/Controllers/CustomerRecordsController.cs`
- `backend/src/Nidhi.Application/CustomerRecords/CustomerReadContracts.cs`
- `backend/src/Nidhi.Infrastructure/CustomerRecords/CustomerReadService.cs`
- `backend/tests/Nidhi.IntegrationTests/CustomerReadTests.cs`
- `docs/SESSION_9_REPORT.md`
- `frontend/web/src/app/(customer)/transactions/[id]/page.tsx`
- `frontend/web/src/app/(customer)/transactions/page.tsx`
- `frontend/web/src/components/gold/holdings-summary.tsx`
- `frontend/web/src/components/transactions/transaction-detail.tsx`
- `frontend/web/src/components/transactions/transaction-list.tsx`
- `frontend/web/src/components/transactions/transactions-page.tsx`
- `frontend/web/src/lib/transactions/presentation.ts`
- `frontend/web/src/types/transactions.ts`
- `frontend/web/tests/transactions.test.mjs`

Modified files:

- `backend/src/Nidhi.Application/Wallet/WalletContracts.cs`
- `backend/src/Nidhi.Infrastructure/DependencyInjection.cs`
- `backend/src/Nidhi.Infrastructure/Wallet/WalletService.cs`
- `docs/design/API_CONTRACT.md`
- `docs/design/API_ERRORS.md`
- `docs/design/TRACEABILITY.md`
- `frontend/web/src/app/globals.css`
- `frontend/web/src/components/customer/customer-dashboard.tsx`
- `frontend/web/src/components/gold/gold-page.tsx`
- `frontend/web/src/components/layout/application-shell.tsx`
- `frontend/web/src/components/wallet/wallet-view.tsx`
- `frontend/web/src/types/wallet.ts`
- `frontend/web/tsconfig.json`
