# Session 10 — Customer savings goals

Implemented on the existing `feat/savings-goals` branch. No commit, new branch, dependency, migration, or Session 11 work was introduced. The task-owned disposable database and private email pickup were removed and the task-started servers stopped; the existing development PostgreSQL container and volume were retained.

## Approved behavior and contract reconciliation

The existing `SavingsGoal` entity is used unchanged: UUIDv7 GoalId, CustomerId, TargetGrams decimal(20,8), optional TargetDateUtc timestamp with time zone, Status, CreatedAtUtc and UpdatedAtUtc. Creation persists ACTIVE. Reaching a target is derived from current total GoldHolding and does not change lifecycle status, archive or remove the goal. No parallel goal holding, balance, progress snapshot or reservation exists.

The API's earlier POST replacement wording contradicted MVP_SCOPE's explicit no-edit/delete v1 boundary and US-GOAL-001's requirement to reject a second goal. The implementation follows those requirements and reconciles POST to create-only with `409 ACTIVE_GOAL_ALREADY_EXISTS`. No PUT/PATCH/DELETE/replacement operation was added. COMPLETED/REPLACED enum values remain in the established persistence model but Session 10 does not write them. Dashboard's documented abbreviated activeGoal is now the same complete DTO as the goal read, eliminating separate progress models.

Target-date validation details were absent. The smallest clarification preserves the existing UTC timestamp contract: supplied dates must be valid UTC timestamps, not date-only strings; past dates are allowed because no future-only rule is approved. Dates remain advisory. The browser sends its chosen date at UTC midnight and displays the timestamp's UTC calendar date without local timezone shifting. No completion guarantee or automatic purchase exists.

For exact browser input, targetGrams accepts a JSON decimal number or exact decimal string; output retains the established decimal-number contract. The existing API client now accepts an optional response decoder. Goal numeric tokens are preserved as strings before JavaScript floating-point parsing, including nested Dashboard goals. Exact remaining grams use scaled BigInt, while all authoritative progress arithmetic is C# decimal. This adds no dependency or second fetch wrapper.

## API, validation and security

- `GET /api/v1/customer/savings-goal`: active GoalResponse or HTTP 200 `{"goal":null}`.
- `POST /api/v1/customer/savings-goal`: HTTP 201, the same complete GoalResponse, and a Location pointing to the read route.
- Both use AuthPolicies.VerifiedCustomer. Anonymous requests return 401; unverified Customers and ADMIN return the existing appropriate 403 Problem Details. CustomerId is derived from the principal. Request ownership/progress/status/other unsupported fields are rejected.
- Creation uses the existing antiforgery middleware; reads and responses are no-store. No financial idempotency record is used.
- Target must be positive, less than 1,000,000,000,000, and at most 8 decimal places. Excess scale, including excess trailing zeros, is rejected; no silent rounding or arbitrary product maximum is added. The bound is database representability.
- The existing `uq_savings_goals_one_active_per_customer` partial unique index enforces at most one ACTIVE goal per customer. Only that specific PostgreSQL unique violation maps to the documented conflict. There is no process lock or check-then-insert correctness dependency.
- Missing persisted holding returns 503 HOLDING_UNAVAILABLE, not a fabricated zero. Normal zero holdings are supported.

ProgressRatio = current persisted GoldHolding quantity / target; ProgressPercentage = ratio * 100; VisualProgressPercentage = min(percentage, 100); IsCompleted = holding >= target. The full supported quantity/target range fits decimal calculations. No price is required. Goal reads use AsNoTracking; Dashboard uses its existing persisted holding snapshot and the same GoalResponse mapping.

## Financial independence and isolation

Goal creation writes SavingsGoal only. Integration assertions verify unchanged wallet/holding and no customer FinancialTransaction, LedgerEntry or IdempotencyRecord. Goal achievement is read-derived and leaves financial records unchanged. Concurrent same-customer HTTP creation produces exactly one 201, one 409 and one ACTIVE row. Independent Customers each create their own goal; forged query customer IDs never expose another customer's goal. Unsupported edit/delete methods return 405 and leave the other customer's goal unchanged. There are no goal-ID mutation routes to test.

A mandatory real PostgreSQL test creates a verified Customer and 5 g goal, sets the test-only persisted starting holding to 1 g, funds through Session 7, publishes a fresh explicit fixture price, and purchases through the actual Session 8 HTTP path. Goal progress moves from 20% to 40%, matches the purchase's persisted resulting holding and Dashboard DTO, and remains consistent on reload. The fixture holding setup is test data only.

## Customer UI

`/goals` uses the existing customer shell and desktop/mobile navigation. It shows a real no-goal state, concise grams/optional-date form, active goal, exact current/target/remaining quantities, progress, optional date and calm achieved state. Creation focuses the page heading and announces success. Form errors are associated with labeled inputs; pending fields/buttons are disabled. Loading, API failure, authorization, creation failure and reload recovery are distinct from empty or zero states. Duplicate-creation errors direct the customer to reload the authoritative goal.

Dashboard fetches the same activeGoal DTO through its existing endpoint and reuses GoalCard; returning to Dashboard or Goals fetches persisted data rather than retaining competing goal copies. Existing funding and Gold purchase flows are preserved. Gold page was not changed: its optional goal reference is unnecessary for the approved create/view slice. V1 has no editing/removal controls or deletion dialog.

The progress bar exposes accessible progress semantics, capped width and uncapped text. Achieved status is text as well as color. Exact quantities wrap at narrow widths; controls retain the established touch sizes. The new progress treatment adds no animation and respects the existing reduced-motion behavior. No fake financial state, sample targets, automatic contribution, forecasting, physical gold ownership, custody, purity or return claims were introduced.

## Validation

- Backend restore and build from backend/: passed; build 0 warnings, 0 errors.
- Backend unit: 15 total / 15 passed / 0 failed / 0 skipped (Session 9 baseline 9; 6 Session 10 cases).
- Backend integration with secure ignored NIDHI_TEST_POSTGRES and real healthy PostgreSQL 18: 98 total / 98 passed / 0 failed / 0 skipped (baseline 88; 10 Session 10 tests).
- Frontend: 50 total / 50 passed / 0 failed / 0 skipped (baseline 42; 8 Session 10 tests).
- Frontend lint, typecheck and production build: passed.
- Existing auth, funding, purchase, holdings and history tests remain present and pass. Clean-database migrations/constraints test passes; no migrations were edited or added.
- Live GET /health: HTTP 200, `{"status":"ok"}`.
- Live browser against a separately migrated disposable PostgreSQL database: register/verify/login, empty goal, excessive precision validation, create with UTC date, refresh persistence, actual funding/purchases, progress 0 → 20 → 125%, achieved bar 100%, exact holding 6.25000000 g, Dashboard goal, API-failure recovery, focus and semantic ARIA checks, reduced motion and no uncaught JS errors.
- Responsive checks and screenshots at 320, 390, 768 and 1440px: no horizontal overflow. Empty, active, achieved and Dashboard states checked; maximum-size targets and keyboard submission additionally checked. Screen-reader semantics were inspected; no claim of a manual assistive-technology audit is made.
- Loading/503/403 UI fault injection is supplemental; successful financial and goal flows use the real API.
- Screenshots and temporary automation are under `/private/tmp/nidhi-session10-browser/` and `/private/tmp/nidhi-session10-browser.mjs`; no browser dependency was added to the project.
- git diff --check: passed. Changed/new content secret review: no secrets introduced; ignored local database credentials were never printed or copied into tracked files.

The first database run exposed acceptance of unsupported request properties; explicit unmapped-member rejection fixed it, and the final full database run passed. Browser validation exposed insignificant decimal trailing zeros in percentage text; text now removes only those zeros while preserving exact quantities and authoritative progress.

Session 10 is ready for review. Suggested commit: `feat(goals): add simulated gold savings goals`.

## Exact review inventory

Created files:

- `backend/src/Nidhi.Api/Controllers/SavingsGoalController.cs`
- `backend/src/Nidhi.Application/SavingsGoals/GoalContracts.cs`
- `backend/src/Nidhi.Infrastructure/SavingsGoals/SavingsGoalService.cs`
- `backend/tests/Nidhi.IntegrationTests/SavingsGoalTests.cs`
- `backend/tests/Nidhi.UnitTests/GoalProgressTests.cs`
- `docs/SESSION_10_REPORT.md`
- `frontend/web/src/app/(customer)/goals/page.tsx`
- `frontend/web/src/components/goals/goal-card.tsx`
- `frontend/web/src/components/goals/goals-page.tsx`
- `frontend/web/src/lib/goals/presentation.ts`
- `frontend/web/tests/goals.test.mjs`

Modified files:

- `backend/src/Nidhi.Application/Wallet/WalletContracts.cs`
- `backend/src/Nidhi.Infrastructure/DependencyInjection.cs`
- `backend/src/Nidhi.Infrastructure/Wallet/WalletService.cs`
- `docs/design/API_CONTRACT.md`
- `docs/design/API_ERRORS.md`
- `docs/design/TRACEABILITY.md`
- `frontend/web/src/app/globals.css`
- `frontend/web/src/components/customer/customer-dashboard.tsx`
- `frontend/web/src/components/layout/application-shell.tsx`
- `frontend/web/src/lib/api/client.ts`
- `frontend/web/src/types/wallet.ts`
