# Session 13 — E2E, security, accessibility and production hardening

Implemented on `feat/hardening`; no branch or commit created. No Session 14 deployment work, schema change, new business feature, real-money integration or accounting redesign.

## Sources and regression baseline

Reviewed AGENTS.md, README.md, the requested requirements/design documents, Sessions 5–12 reports and DEVELOPMENT_AUTHENTICATION.md against the implementation. Current code remains authoritative. The checkout contains additional email/resend/TLS tests after the Session 12 report: its actual starting baseline was **34 unit, 114 integration and 73 frontend tests**, rather than the historical 15/112/71. All original tests remain. Final totals are **36 unit, 120 integration and 85 frontend**, all passed, zero failed, zero skipped. Six integration cases, two unit cases and twelve frontend cases were added; existing tests were strengthened. No disappearing/skipped DB tests.

README still describes several already-implemented features as future work; this historical mismatch was not used to rebuild them. Profile currently displays account details only: there is no profile mutation/form to test or extend during this hardening session. OD-013–015 remain open launch decisions.

## Discovered issues and fixes

- **PRICE_CHANGED comparison was lost.** The shared purchase flow discarded the reviewed intent/quote and refreshed the current price into the ordinary form. Its warning did not preserve the prior quote or present an explicit rejection/comparison. Existing financial semantics correctly rejected the old version.
- Added a dedicated state retaining the original price and amount across successful/failed quote refreshes. It displays previous/current four-decimal prices, the updated eight-decimal estimate, publication/freshness context and **No purchase was made**. Cancel and Review new price are separate actions. Review enters the existing final confirmation; no financial POST occurs until that confirmation. The rejected key is abandoned and the new version receives a new key. Repeated refresh cannot erase/bypass the comparison.
- Uncertain transport/server outcomes retain the exact request/key, explain receipt recovery and disable price refresh until retry or explicit abandonment. This prevents a quote refresh from converting a possibly committed request into a new financial intent. Lost committed responses are explicitly retried with the same content/key and return HTTP 200 original receipts. No automatic network/financial replay; the existing single retry for pre-mutation CSRF rejection remains.
- Gold's unstyled amount input now uses the established Input/FormField/Button primitives, a visible boundary/background, label, required state, decimal keyboard, available balance, associated help/error, pending/disabled states and visible keyboard focus. Invalid amounts produce no fabricated zero estimate. Inputs/admin controls share a boundary with measured contrast above 3:1. The missing `--space-5` token previously collapsed auth form spacing; it is restored.
- Removed price-version UUIDs from the ordinary Gold/holding/review surfaces. Internal version submission remains intact; full version/reference remain in the receipt detail. Activity cards prioritize operation/time/amount/exact grams/completed state and View receipt; full UUIDs remain in routes/keys and receipt detail, rather than list text.
- Replaced backend/idempotency/ledger-verification explanations and stale profile wording with customer language. Removed the unsupported customer-facing Double-Entry Ledger Verified badge. Simulation disclosures remain. Authoritative money/grams/receipt data remain unchanged.
- Added one explicit UTC date/time formatter for activity, Gold, funding receipts, customer receipt timestamps and admin records. ISO values remain in data/dateTime attributes and receipt timestamp titles; goal calendar dates retain their existing UTC handling. Browser locale punctuation can differ, while date/time fields and UTC context remain consistent.
- Funding used a `role=dialog` div without containment/return. It now uses native showModal/inert background, explicit Tab/Shift+Tab wrapping, Escape/cancel, disabled close while pending, a scrollable viewport-fitting container, success-heading focus and focus return. Summary refresh runs in the background after success so it does not replace the opener. Uncertain funding freezes the submitted amount/presets for same-key retry. A synchronous ref guard prevents duplicate funding, goal creation and price publication.
- **Safari rendering:** the filtered/animated dialog layer was reachable through accessibility controls but did not paint in the captured Safari window. Removed backdrop blur and dialog/container entrance effects; a standard native backdrop/static card paints correctly. Critical auth forms also render without depending on an entrance animation. No browser-specific hack. Wallet subsection headings now use h2 rather than skipping directly from h1 to h3.
- Session expiry now removes protected content, explains that the session ended, and resumes only an allowlisted same-origin destination within the resolved role. First-time anonymous access is distinguished from expiry. Role/identity race protections remain.
- Huge valid decimal JSON numbers could overflow multiplication during cent validation and become HTTP 500. Range checks now precede multiplication. Min/max/extreme inputs remain rejected without financial writes; accepted amounts and canonicalization are unchanged.
- Normal client-aborted reads were logged as internal server failures. Request cancellation now ends without an INTERNAL_ERROR/secret-bearing exception log. A test-only HTTP probe proves cancellation produces an empty 499, while unexpected exceptions still produce safe Problem Details.
- Antiforgery's existing no-cache requirements previously produced repetitive header-override warnings. Its endpoint now sets the exact no-cache/no-store/Pragma headers before obtaining tokens; protection is unchanged.

## Problem Details namespace and consistency

`AuthProblems.Create` hard-coded an error-document hostname that was also repeated in design examples. No requirement, deployment configuration or Session 12 decision approved ownership of that hostname. Session 12 explicitly deferred actual deployment origins.

The final strategy is **type about:blank + standard HTTP status title + safe occurrence detail + unchanged application code**. This uses the registered [RFC 9457 section 4.2.1](https://www.rfc-editor.org/rfc/rfc9457.html#section-4.2.1) type without inventing another hostname or endpoint-relative identity. Consumers still branch on code/status. `PRICE_CHANGED` remains HTTP 409 and its accounting behavior is unchanged. API_ERRORS and API_CONTRACT reconcile this genuine contract correction. The earlier activePrice example was also inaccurate: the real backend supplies the standard envelope; the UI obtains the current quote through the existing GET.

Real HTTP coverage checks 400, 401, 403, 404, 409, 422, 429, 503 and 500 for application/problem+json, type/title/status/detail/instance/code/timestampUtc/traceId and no-store. A deliberately sensitive unexpected exception is not reflected. Field validation avoids echoing submitted input or framework/provider errors. Client disconnection is a transport cancellation, not a delivered application Problem Details response.

## Authentication, CSRF and authorization

Full existing real-PostgreSQL auth coverage passes: atomic CUSTOMER registration, duplicate/invalid/concurrent registration, verified/pending behavior, verification account binding/reuse/expiry, login/me/logout, generic recovery, password reset/account binding/reuse/expiry and revocation of copied old sessions. Gmail and pickup behavior remain covered. Browser evidence includes private pickup verification, verified/unverified flows, reload, reset/new-password login and copied-cookie revocation.

Cookies remain HttpOnly, SameSite=Strict, host-only, Path=/, nonpersistent, with an absolute eight-hour non-sliding ticket. Secure is always required outside Development; local HTTP remains usable. Every authenticated request validates the real Identity security stamp. No JWT or browser token storage.

The existing shared memory-only CSRF client invalidates tokens after login/logout/reset and now explicitly after registration as well. Missing, invalid and other-identity tokens are rejected before writes for customer funding/purchase/goal creation; missing/invalid admin publication tokens are rejected. Valid tokens then succeed for each real mutation. Existing backend identity-transition and frontend concurrency/stale-401 tests remain green. No second CSRF mechanism or unsafe automatic transport replay.

| Caller | Customer dashboard/wallet/price reads | Verified customer holdings/history/receipt/goals and financial writes | All admin routes/publication |
|---|---|---|---|
| Anonymous | 401 | 401 | 401 |
| Unverified CUSTOMER | Existing permitted reads retained | 403 EMAIL_NOT_VERIFIED | 403 |
| Verified CUSTOMER | Own principal-derived records | Own principal-derived records | 403 |
| ADMIN | 403 | 403, including funding/purchase | Allowed |

The matrix is enforced by ASP.NET Core and tested with direct HTTP calls, not just navigation. Foreign/unknown receipts return the same 404/code/detail. Other customer IDs in read queries cannot replace principal ownership; wallet, holding, goal and profile have no customer-selectable ID route. Existing customer/goal isolation and admin boundary tests pass. Unsupported goal edit/delete and financial-history mutation methods remain absent. React renders display names/reasons/metadata as text; the live display name containing angle brackets does not become markup.

## Financial regression and live acceptance

All Session 7 funding and Session 8 purchase tests pass against real PostgreSQL, including FOR UPDATE/xmin, caps, UTC daily-limit concurrency, immutable idempotent replay, conflict binding, precision/truncation/zero quantity, insufficient funds, holding lost-update protection, simultaneous funding/purchase and atomic rollback. LKR and GOLD_GRAMS remain independent balanced books. No historical mutation or accounting change.

Goal tests retain one active goal (including achieved), positive decimal(20,8), optional safe UTC date, exact persisted holding-derived progress, concurrent uniqueness, no reservation and no financial/ledger/idempotency mutation. Actual browser purchases move a goal beyond 100% while only its visual indicator caps. Admin publication retains positive decimal(18,4), reason, immutable new version, deterministic timestamp/UUID ordering and atomic audit; concurrent publication and injected audit rollback pass.

The live Chromium suite reuses the externally installed Playwright **1.62.1** with Google Chrome **155.0.8059.80**, rather than adding a test dependency. It uses a production Next.js build, real Kestrel and a freshly migrated disposable PostgreSQL database with a CLI-provisioned ADMIN and private email pickup. It asserts:

1. Landing/register → unverified login/denied funding → verify → dashboard; zero wallet/holding and empty transactions/goals; reload/cookie flags.
2. Minimum LKR 100 funding and receipt; a second funding response lost **after server acceptance**, followed by same-key replay with one credit.
3. Goal creation, Admin publication A, customer review A, Admin publication B, customer POST A → 409 PRICE_CHANGED. Wallet/holding/history are unchanged. Old/new prices, retained amount, updated quantity and no-purchase message are visible with logical focus. Review B performs no POST; final confirmation uses B/new key and creates exactly one new purchase.
4. A committed purchase response lost in transit, explicit same-key retry → original HTTP 200 receipt and one purchase; exact holding/history/receipt values and achieved goal.
5. Receipt refresh/back/forward without resubmission; customer/admin direct shell isolation; intercepted read failures with no false zero and successful Retry.
6. Customer/admin cookie removal → real 401, protected content removed, expiry notice and sensible role-bound return path; reset/reused-token/copied-session revocation and logout; no localStorage/sessionStorage auth/CSRF.
7. Every requested public/auth/customer/admin route, both detail families and ledger at 320/390/768/1024/1440px, without page/control/text-edge overflow; one h1 per route.
8. Native modal keyboard wrapping/Escape/return, accessible form names, reduced motion and production headers; no unexpected console/page/hydration/asset error.

Native Safari **26.6.2** was separately inspected through the computer-use bridge after the user made a window available. Verified real login/cookie persistence on reload, amount/focus/form affordance, Price A 30,000 → B 32,000 comparison/no-purchase/new review/final purchase (0.00312500 g), date input 31 December 2026 retained without day shift, native dialog paint/cancel and wallet hierarchy. A direct PostgreSQL snapshot before/after the rejected Safari request stayed **5 transactions, 2 purchases, 14 ledger entries, LKR 1,900.00 total wallets, 0.00769230 total grams**. After explicit confirmation it became one additional purchase/four entries with the expected debit/credit. Safari also showed genuine stale-price blocking and unavailable valuation after disposable fixture aging; publishing/refreshing a fresh version recovered. The task-created Safari tab was closed after logout, preserving existing tabs. Safari review was desktop/native; the exhaustive five-width automation was Chromium.

No-price UX was tested live before the first publication; no fallback/zero quote or purchase control was fabricated. Stale/missing price financial rejection remains covered in the complete DB suite. Browser fault injection is limited to read/network/lost-response testing; financial success responses come from the actual API.

## Accessibility, responsive, state and performance review

Keyboard-driven Chromium registration/login, funding, purchase/reconfirmation, goal creation, admin publication and receipt navigation are covered. Native dialog backgrounds are inert, Tab wraps, Escape cancels, success/price-change/confirmation focus is explicit, errors/status use text/live semantics and associated labels/help. Existing pagination heading focus and skip links remain. Wallet heading levels are corrected. Native Safari amount/date/dialog behavior was inspected. No formal WCAG certification or manual screen-reader certification is claimed.

Measured representative contrast on #181D25: primary text 15.77:1; muted 7.74:1; gold highlight 11.85:1; error text 4.79:1; control boundary 3.62:1. Dark text on gold primary button is 8.04:1. Disabled controls remain identifiable; their exemption is not used to claim active-control contrast. Reduced-motion checks find no animated main-content elements on landing/auth/Gold/admin price pages; information/controls remain available.

Loading, empty, unavailable and error are separate on inspected customer/admin data pages. Gold clears unavailable balances/quote and retains a rejected quote for comparison. Refresh/retry recover; errors do not become false financial zeros. Auth/client requests have a finite timeout and at most one explicitly pre-mutation CSRF retry. No request loop, new polling, giant asset or additional client/state framework was introduced. Public landing remains static; login now reads server search parameters for the expiry notice/return target. Shared date/transaction/financial formatters are reused, with four-decimal price/eight-decimal gram detail preserved.

## Production configuration and dependency review

- Gmail revocation checking remains enabled by default. The explicit local revocation opt-out is rejected in both Production and Staging. Two new unit cases also prove nondevelopment defaults retain revocation and a null custom certificate callback. Required STARTTLS, normal hostname/trust/expiry/chain validation and private configuration are unchanged. No SMTP credentials or actual external messages were used in validation.
- Existing per-IP auth fixed-window limits/lockout remain active. HTTP 429 includes the safe envelope and Retry-After. Health/static resources have no global limiter. Trust-all forwarded headers were not enabled.
- Production API HSTS is added only outside Development and verified over HTTPS on a nonlocalhost test host. API and frontend emit nosniff, no-referrer and frame denial. The frontend additionally uses a deliberately limited CSP (`frame-ancestors 'none'; object-src 'none'; base-uri 'self'`) and disables camera/microphone/geolocation permissions and X-Powered-By. It does not claim a complete script-source CSP; no directive breaks Next.js inline scripts/hydration. Development localhost HTTP remains usable.
- The approved browser architecture is same-origin `/api` forwarding. No CORS grant (wildcard or credentialed) is enabled; an untrusted Origin gets neither allow-origin nor allow-credentials. `API_BASE_URL` configures the upstream and defaults to local development only. Actual production origins/trusted proxies/HTTPS termination are deployment inputs, not invented hosts.
- Fixed SQL identifiers and parameter placeholders are retained for financial locks; Identity role insert interpolation is parameterized; the provisioning advisory lock is a fixed constant. EF queries/search remain parameterized. No unsafe user HTML, raw dynamic SQL, query-body logging or sensitive-data logging was added. Audit details remain allowlisted; no auth/CSRF/reset/email token, credential or connection string is introduced.
- NuGet direct/transitive vulnerability review reports none. Frontend audit initially reported 16 advisories. Same-line security updates: **next 16.3.3 → 16.3.8**, **eslint-config-next 16.3.3 → 16.3.8**. Patched transitive pins are **brace-expansion 1.1.21** and **source-map-js 1.2.2**; Next's resolved sharp is **0.35.5**. No direct dependency added/removed or major framework upgrade. Full lockfile differences are reviewable below.
- Final pnpm audit: **0 critical, 1 high, 0 moderate/low**. Remaining [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) affects **braces 3.0.3** in the ESLint/fast-glob/micromatch development chain, has no patched release, and requires deeply nested attacker-controlled glob patterns. No application runtime path takes user patterns into that package. Deferred to a compatible upstream dev-tool fix; not force-overridden to an incompatible major. Next's [ImageResponse advisory](https://github.com/vercel/next.js/security/advisories/GHSA-vcvr-r3jv-pc5j) did not match an implemented route, but the patch also removes the framework advisory set. No Open Graph image route or untrusted SVG ImageResponse exists.
- Package type=module removes Node's project-controlled typeless-module warnings. pnpm 9's own url.parse deprecation remains a package-manager warning, not an application/browser runtime warning. The repository's pinned package-manager policy is retained.

## Validation evidence and cleanup

Backend restore/build were run from backend; build has **0 warnings/0 errors**. All 36 unit and 120 integration cases pass with real PostgreSQL and zero failures/skips. Frontend install with frozen lockfile, 85 tests, lint, typecheck and production Webpack build pass. Existing DB tests apply the actual migration to suite-owned clean databases; the live smoke also migrates a new database from scratch. No migration was added or historical migration edited.

Live health returned **HTTP 200** with the existing minimal status body. The final production-build Chromium hardening suite and existing Session 12 public browser suite both passed end-to-end. The former wrote results.json with journeys=passed, consoleErrors=[], the five widths and the exact PRICE_CHANGED values (25,000 → 26,000; LKR 100.00; 0.00384615 g; 409 with no mutation; two explicit requests/new key). The latter passed public/auth/404 routes, anchors, CTAs, keyboard/skip/focus, validation, reduced motion, zoom equivalent, metadata, robots and accessible-tree checks. API/frontend logs contain no warning/error/failure during final acceptance. `git diff --check` passes. Exact ignored local credential values were scanned across tracked/untracked review files with zero matches. `.env` is ignored, untracked and mode 0600. Private keys/SMTP secrets/auth cookies/tokens/credentialed connection strings are absent from review files. Private fixture credentials/pickup remain outside the repository and are removed at teardown. Screenshots/TRX/redacted validation logs are local /private/tmp artifacts, not committed credential material.

Initial harness runs exposed a startup-readiness race and an overstrict multi-loading-element selector; these were corrected. Live testing also drove the dialog return/wrap/render corrections above. Normal client cancellation logging was corrected rather than counted as an unexplained server error. A final logout harness assertion was corrected to accept the existing safe return-path query on /login; the complete final run then passed. An automatic approval review timed out for a build and a later review hit its usage limit; user-requested retries succeeded. Neither prevented final validation. None is a remaining product failure. The latest purchase-flow coverage also proves that a definite price rejection after an uncertain transport response clears uncertainty and permits explicit new-price recovery, while the intervening transport retry keeps its original key.

Final acceptance snapshot: **4 financial transactions, 2 gold purchases, 12 ledger entries, LKR 900.00 total wallet balance and 0.00769230 g total holdings** in the disposable database. Final health was HTTP 200 before teardown. The task-owned API/frontend were stopped; the task database, private pickup messages and owner-only live credential file were removed. The existing PostgreSQL service/volume and developer data were preserved. The temporary Safari credential helper was also removed. Nonsecret logs, screenshots and TRX remain under /private/tmp for local review.

All Session 13 implementation and validation gates are complete. Ready for review and commit on feat/hardening, with the explicitly documented upstream development-tool advisory and Session 14 deployment inputs. No commit was made. Suggested message: `fix(hardening): require explicit price reconfirmation and secure application flows`.

## Deferred Session 14 deployment inputs

Actual owned public/frontend/API origins; trusted reverse proxy addresses/header handling and TLS termination/HTTP redirect policy; protected persistent Data Protection keys; production SMTP secrets and revocation connectivity; deployment smoke/headers at the real HTTPS edge; backup/restore/capacity/RPO/RTO and OD-013–015 legal/retention/eligibility decisions; canonical/social-image URLs only after a real origin exists. Monitor the unfixed build-only braces advisory for a compatible upstream patch. No hosting, deployment, real payment integration, new profile mutation or other business feature was implemented.

## Exact review inventory

Generated below from Git; every listed file must be reviewed before committing. No commit created.

Created (5):

- `backend/tests/Nidhi.IntegrationTests/HardeningTests.cs`
- `docs/SESSION_13_REPORT.md`
- `frontend/web/src/lib/format/date.ts`
- `frontend/web/tests/hardening-browser.mjs`
- `frontend/web/tests/hardening.test.mjs`

Modified (34):

- `backend/src/Nidhi.Api/Authentication/AuthProblems.cs`
- `backend/src/Nidhi.Api/Controllers/AuthController.cs`
- `backend/src/Nidhi.Api/Program.cs`
- `backend/src/Nidhi.Infrastructure/Wallet/WalletService.cs`
- `backend/tests/Nidhi.UnitTests/GmailConfigurationTests.cs`
- `docs/design/API_CONTRACT.md`
- `docs/design/API_ERRORS.md`
- `frontend/web/README.md`
- `frontend/web/next.config.ts`
- `frontend/web/package.json`
- `frontend/web/src/app/(auth)/login/page.tsx`
- `frontend/web/src/app/globals.css`
- `frontend/web/src/components/admin/admin-pages.tsx`
- `frontend/web/src/components/auth/account-overview.tsx`
- `frontend/web/src/components/auth/auth-form.tsx`
- `frontend/web/src/components/auth/auth-provider.tsx`
- `frontend/web/src/components/customer/customer-dashboard.tsx`
- `frontend/web/src/components/goals/goals-page.tsx`
- `frontend/web/src/components/gold/gold-page.tsx`
- `frontend/web/src/components/gold/holdings-summary.tsx`
- `frontend/web/src/components/layout/application-shell.tsx`
- `frontend/web/src/components/transactions/transaction-detail.tsx`
- `frontend/web/src/components/transactions/transaction-list.tsx`
- `frontend/web/src/components/ui/primitives.tsx`
- `frontend/web/src/components/wallet/wallet-funding-modal.tsx`
- `frontend/web/src/components/wallet/wallet-view.tsx`
- `frontend/web/src/lib/api/client.ts`
- `frontend/web/src/lib/auth/routing.ts`
- `frontend/web/src/lib/gold/purchase-flow.ts`
- `frontend/web/src/lib/validation/gold.ts`
- `frontend/web/tests/auth.test.mjs`
- `frontend/web/tests/gold.test.mjs`
- `package.json`
- `pnpm-lock.yaml`

Prioritize purchase-flow/Gold, dialog and auth state, shared client, API error/middleware/amount guards, PostgreSQL hardening tests, and manifests/lockfile; the remaining presentation/docs/test files are also part of the review.

## Exact lockfile package resolution changes

No direct package was added/removed. These are all package version-set differences produced by the security patch update/pins (including optional platform builds and development transitive packages). Unchanged packages are omitted.

| Package | Before | After |
|---|---|---|
| `@babel/helpers` | 7.29.7 | 7.29.10 |
| `@babel/parser` | 7.29.8 | 7.29.9 |
| `@babel/traverse` | 7.29.8 | 7.29.10 |
| `@img/sharp-darwin-arm64` | 0.35.4 | 0.35.5 |
| `@img/sharp-darwin-x64` | 0.35.4 | 0.35.5 |
| `@img/sharp-freebsd-wasm32` | 0.35.4 | 0.35.5 |
| `@img/sharp-libvips-darwin-arm64` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-darwin-x64` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-linux-arm` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-linux-arm64` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-linux-ppc64` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-linux-riscv64` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-linux-s390x` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-linux-x64` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-linuxmusl-arm64` | 1.3.3 | 1.3.4 |
| `@img/sharp-libvips-linuxmusl-x64` | 1.3.3 | 1.3.4 |
| `@img/sharp-linux-arm` | 0.35.4 | 0.35.5 |
| `@img/sharp-linux-arm64` | 0.35.4 | 0.35.5 |
| `@img/sharp-linux-ppc64` | 0.35.4 | 0.35.5 |
| `@img/sharp-linux-riscv64` | 0.35.4 | 0.35.5 |
| `@img/sharp-linux-s390x` | 0.35.4 | 0.35.5 |
| `@img/sharp-linux-x64` | 0.35.4 | 0.35.5 |
| `@img/sharp-linuxmusl-arm64` | 0.35.4 | 0.35.5 |
| `@img/sharp-linuxmusl-x64` | 0.35.4 | 0.35.5 |
| `@img/sharp-wasm32` | 0.35.4 | 0.35.5 |
| `@img/sharp-webcontainers-wasm32` | 0.35.4 | 0.35.5 |
| `@img/sharp-win32-arm64` | 0.35.4 | 0.35.5 |
| `@img/sharp-win32-ia32` | 0.35.4 | 0.35.5 |
| `@img/sharp-win32-x64` | 0.35.4 | 0.35.5 |
| `@napi-rs/wasm-runtime` | 1.2.3 | 1.2.5 |
| `@next/env` | 16.3.3 | 16.3.8 |
| `@next/eslint-plugin-next` | 16.3.3 | 16.3.8 |
| `@next/swc-darwin-arm64` | 16.3.3 | 16.3.8 |
| `@next/swc-darwin-x64` | 16.3.3 | 16.3.8 |
| `@next/swc-linux-arm64-gnu` | 16.3.3 | 16.3.8 |
| `@next/swc-linux-arm64-musl` | 16.3.3 | 16.3.8 |
| `@next/swc-linux-x64-gnu` | 16.3.3 | 16.3.8 |
| `@next/swc-linux-x64-musl` | 16.3.3 | 16.3.8 |
| `@next/swc-win32-arm64-msvc` | 16.3.3 | 16.3.8 |
| `@next/swc-win32-x64-msvc` | 16.3.3 | 16.3.8 |
| `@tybys/wasm-util` | 0.10.3 | 0.10.4 |
| `@typescript-eslint/eslint-plugin` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/parser` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/project-service` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/scope-manager` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/tsconfig-utils` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/type-utils` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/types` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/typescript-estree` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/utils` | 8.68.0 | 8.71.2 |
| `@typescript-eslint/visitor-keys` | 8.68.0 | 8.71.2 |
| `array-includes` | 3.1.9 | 3.2.0 |
| `axe-core` | 4.13.0 | 4.14.0 |
| `baseline-browser-mapping` | 2.11.19 | 2.11.29 |
| `brace-expansion` | 1.1.18, 5.0.9 | 1.1.21, 5.0.12 |
| `browserslist` | 4.28.8 | 4.29.3 |
| `caniuse-lite` | 1.0.30001810 | 1.0.30001816 |
| `electron-to-chromium` | 1.5.415 | 1.5.458 |
| `eslint-config-next` | 16.3.3 | 16.3.8 |
| `fastq` | 1.20.1 | 1.20.3 |
| `ignore` | 5.3.2, 7.0.6 | 5.3.2, 7.0.12 |
| `is-core-module` | 2.16.2 | 2.17.0 |
| `nanoid` | 3.3.18 | 3.3.18, 3.3.20 |
| `next` | 16.3.3 | 16.3.8 |
| `node-releases` | 2.0.53 | 2.0.59 |
| `sharp` | 0.35.4 | 0.35.5 |
| `source-map-js` | 1.2.1 | 1.2.2 |
| `string.prototype.matchall` | 4.0.12 | 4.1.0 |
| `typed-array-byte-offset` | 1.0.4 | 1.0.5 |
| `typescript-eslint` | 8.68.0 | 8.71.2 |
| `update-browserslist-db` | 1.3.1 | 1.3.4 |
| `which-typed-array` | 1.1.22 | 1.1.24 |
| `zod` | 4.4.3 | 4.6.7 |
