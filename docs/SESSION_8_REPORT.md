# Session 8 — Simulated gold purchase

## Final implementation

Nidhi remains a modular monolith: ASP.NET Core API → Application and Infrastructure, Application → Domain, Infrastructure → Application/Domain, with PostgreSQL persistence. All web experiences share the Next.js App Router application. Financial validation, authorization, persistence and ledger posting run in ASP.NET Core; the frontend manages presentation and purchase intent only. No real-money integration, migrations or dependencies were added.

The current gold price is the latest persisted publication by `PublishedAtUtc`. The customer price endpoint returns the persisted version, four-decimal price, publication timestamp and freshness flags. No publication returns HTTP 503 / `PRICE_UNAVAILABLE`; an existing price older than 24 hours disables purchasing. Purchase execution rejects a noncurrent submitted version with HTTP 409 / `PRICE_CHANGED`, a stale current version with HTTP 503 / `PRICE_STALE`, and no publication with HTTP 503 / `PRICE_UNAVAILABLE`. Prices are never silently substituted.

A verified CUSTOMER submits only `amountLkr`, `priceVersionId` and an `Idempotency-Key`. The backend validates LKR 100.00–1,000,000.00, at most two decimal places and sufficient funds. Decimal division is truncated down to eight decimal places. A zero resulting quantity is rejected with HTTP 422 / `GOLD_QUANTITY_ZERO`. The sub-cent conversion residual is derived from immutable transaction values; it is not a fee or refund.

One PostgreSQL transaction locks the customer's wallet row with `FOR UPDATE`, resolves completed idempotency, inserts an in-progress mapping, validates price and balance, locks the GoldHolding row, debits the wallet, credits the holding, writes the completed GOLD_PURCHASE transaction and ledger, completes the mapping and commits. Funding uses the same wallet-row lock, so funding and purchase serialize compatibly. Any pre-commit failure rolls back all writes and leaves the key retryable.

Receipts are reconstructed from immutable persisted FinancialTransaction snapshots. Completed exact replays return HTTP 200 and the original transaction, price, quantity, balances and timestamp before checking current price or funds. Different amount or version with a completed key returns `IDEMPOTENCY_CONFLICT`. The receipt timestamp is `createdAtUtc` in the actual backend/API contract; the frontend labels it “Completed at” rather than inventing a `completedAtUtc` field.

Each purchase writes two separately balanced books, linked to the same transaction:

| Unit | Debit | Credit | Amount |
|---|---|---|---|
| LKR | `2001-LKR-CUST-{CustomerId}` | `2002-LKR-SYS-CLEARING` | Full purchase LKR |
| GOLD_GRAMS | `1501-GOLD-SYS-ISSUANCE` | `2501-GOLD-CUST-{CustomerId}` | Exact truncated quantity |

All entry amounts are positive. LKR is balanced only against LKR; grams only against grams.

## Frontend purchase intent and receipt

The `/gold` page uses a shared, tested purchase-flow module. Reviewing an amount creates an intent containing amount, price version and key. An unchanged transport retry explicitly resubmits the same content and same key; it never automatically retries a purchase. Editing or abandoning an intent generates a new key when reviewed again.

On `PRICE_CHANGED`, the old intent is discarded, the current backend price and balances are fetched, and the new estimate and persistent “Price updated” warning are shown. No purchase is automatically submitted. The customer must review and explicitly confirm the updated purchase; that request uses the new price version and a new idempotency key. A manual refresh changing the reviewed price likewise invalidates confirmation.

`PRICE_UNAVAILABLE` removes the usable price and blocks purchases, with a refresh path. `PRICE_STALE` explains the stale state and blocks purchases until a fresh publication is available. There is no fallback or fabricated price.

The receipt displays authoritative backend transaction ID, amount, applied price (four decimals), version, quantity (eight decimals), resulting wallet balance, resulting holding (eight decimals) and UTC server timestamp. Success updates displayed balances directly from the receipt. Returning to the dashboard fetches current backend balances and recent transactions. Gold navigation and the Buy Simulated Gold action link to `/gold`. The hardcoded dashboard valuation was removed. No market chart, profit/loss, production price, 24K/purity, physical ownership or guaranteed-return claim was added.

The amount input has a label and associated validation/help text. Confirmation uses keyboard-accessible buttons; pending operations disable editing/duplicate submission. Errors and price changes use text and alert announcements, and success uses a status region. Transaction/version IDs wrap. Existing reduced-motion rules remain active.

## Defects corrected

- PostgreSQL integration tests exposed the holding-lock query selecting nonexistent `holding_id`. It now selects the actual `id` column.
- Frontend contract verification identified the price GET endpoint allowing WalletFailure to become generic HTTP 500. It now maps unavailable pricing to the documented HTTP 503 / `PRICE_UNAVAILABLE`; the existing no-publication PostgreSQL test also verifies GET behavior.
- Frontend submission previously generated a new key on every retry, and price refresh erased the reconfirmation warning. The tested intent lifecycle corrects both behaviors.

## Final validation

- New GoldPurchaseTests class: 23 real PostgreSQL tests passed, 0 failed, 0 skipped.
- Full backend unit suite: 9 total, 9 passed, 0 failed, 0 skipped.
- Full backend integration suite: 70 total, 70 passed, 0 failed, 0 skipped (47 prior + 23 new; GET coverage extends an existing test without changing count).
- Backend build: passed, 0 warnings, 0 errors.
- Frontend Node suite: 34 total, 34 passed, 0 failed, 0 skipped. Gold tests increased from 3 to 12: ten behavioral tests replace one parsing-only placeholder, a net increase of nine from the 25-test baseline.
- Frontend lint, typecheck and production Webpack build: passed.
- PostgreSQL tests cover price state/changes without mutation, authoritative persistence and receipt, roles and validation, both exact ledger books, replay/conflicts, pre-commit retry, replay after changed/stale price, genuinely overlapping identical/distinct purchases, insufficient-funds serialization, holding lost-update protection, funding-versus-purchase concurrency and database-capacity rollback.
- Frontend behavior tests exercise the actual page's shared intent lifecycle through the actual API client with controlled fetch responses, including explicit Price A → Price B reconfirmation and same-key transport retry.
- Supplemental Chrome checks against the built page with mocked API responses passed: rendered reconfirmation/new key, keyboard confirmation, receipt fields, unavailable/stale blocking, long-ID wrapping, reduced motion, and no horizontal overflow at 320, 390, 768 and 1440 pixels. These are UI checks, not a live financial browser transaction; financial correctness is covered by real PostgreSQL integration tests.
- Live normal-development Kestrel `/health`: HTTP 200, `{"status":"ok"}`. The server was stopped cleanly afterward.
- `git diff --check`: passed. Modified, staged and untracked files reviewed for real credentials/private keys/tokens; no matches found. `.env` remains ignored and untracked. Test tokens/prices are fixtures, not production data.

Session 8 is ready to commit within its simulated financial slice. No commit was made. Session 9 was not started.
