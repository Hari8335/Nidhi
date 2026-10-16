# Nidhi web

Next.js App Router, React, strict TypeScript, and Tailwind CSS provide one dark-theme frontend for the public entry page, authentication, customer shell, and admin shell. ASP.NET Core remains responsible for authentication, authorization, and all business logic.

## Run against the API

Follow [authentication development](../../docs/DEVELOPMENT_AUTHENTICATION.md) to configure PostgreSQL, apply migrations, and enable private development email pickup. Start the API on port 5050. From the repository root:

```sh
corepack pnpm install
API_BASE_URL=http://localhost:5050 corepack pnpm dev:web
```

Open http://localhost:3000. `API_BASE_URL` is the server-side ASP.NET Core origin. Next.js rewrites `/api/*` to that origin; it does not implement API handlers. The browser uses `NEXT_PUBLIC_API_BASE_URL=/api/v1` by default and includes cookies. Keep this same-origin path; cross-origin deployments require a separately approved cookie/CORS design. Configure these variables in the process environment or ignored `frontend/web/.env.local`; Next.js does not load the root Compose `.env`. Public variables and rewrites are resolved at build time: rebuild after changing them.

Manrope is loaded and self-hosted through `next/font/google`. Development/production compilation needs access to Google Fonts. The browser does not contact Google. Node 22.6+ is required for the dependency-free TypeScript unit-test runner (Node 24 is validated).

## Authentication pages

- `/register`: email, password, optional display name; success links to verification.
- `/verify-email`: open the emailed Verify email link and confirm. Without a link, enter your email to request another verification message. No account ID or token fields are shown.
- `/login`: routes the actual CUSTOMER to `/dashboard` and ADMIN to `/admin`.
- `/forgot-password`: always shows a generic recovery result.
- `/reset-password`: open the emailed Reset password link, choose a new password, and confirm it. Legacy private pickup `userId`/`token` entry remains available when no link is supplied.
- `/profile`: read-only safe current-user details from `/auth/me`; no planned profile business endpoint is called.

Pickup files are development credentials. Read them privately, never serve or commit them, and delete them after testing. Set `IdentityEmail:PublicBaseUrl` to the trusted frontend origin to include a `link` in pickup JSON. Gmail sends styled action buttons with the same links. Credentials travel in a URL fragment, are removed from the address bar after loading, and remain only in memory; opening a link never submits verification automatically. Expired or used links can be replaced using `POST /api/v1/auth/resend-verification` from the verification page. The UI has a 60-second resend cooldown and the API independently rate-limits requests. Repeat signup for an unverified email offers verification without replacing the original password.

`tests/email-verification-browser.mjs` checks verification/resend and reset-link UI with intercepted responses, without creating accounts or sending mail. Supply `NIDHI_PLAYWRIGHT_MODULE` and optionally `NIDHI_CHROME_PATH` for an existing external Playwright/Chrome installation, then run it from the repository root with Node.

## Foundation

`src/app` owns routes/layouts; `components/ui` owns visual primitives; `components/auth` owns account forms/session state; `components/layout` owns shells; `lib/api` owns fetch/Problem Details/CSRF; `lib/auth` owns role routing; `lib/validation` owns password checks; `types` mirrors safe API DTOs. Theme tokens live in `src/app/globals.css`.

The session provider reads `/auth/me` initially, after login/verification, on window focus/visibility, and every 60 seconds while visible. A 401 clears memory state and antiforgery state. Protected content waits for session resolution and role checks; these are UX guards, not the backend security boundary. Network errors offer a retry without pretending the user is anonymous. No authentication data is persisted in web storage.

All POSTs obtain an in-memory antiforgery token, send `X-CSRF-TOKEN`, and include browser cookies. Login/logout/reset invalidate the cached token; the next POST obtains fresh evidence. Only an explicit pre-mutation `CSRF_VALIDATION_FAILED` rejection is retried once. Network errors and other server errors never replay a mutation automatically.

## Validate

From the repository root:

```sh
corepack pnpm --filter web test
corepack pnpm --filter web lint
corepack pnpm --filter web typecheck
corepack pnpm --filter web build
```

Tests use Node’s built-in runner, with no new framework dependency. Real browser/backend and visual-review evidence is recorded in [Session 6 report](../../docs/SESSION_6_REPORT.md).

### Optional real-browser acceptance

`tests/browser-smoke.mjs` drives the real frontend/API, reads private pickup messages, and uses an ADMIN already created through the approved CLI. Use a disposable local database; the test registers customers and resets their passwords. It is restricted to localhost and never prints passwords/tokens. Start the frontend and API first.

The repository does not add a browser-testing dependency. For the Session 6 run, `playwright-core@1.56.1` was installed in a temporary directory and drove the installed Chrome. For a repeat run, supply an owner-only JSON file outside the repository via `NIDHI_BROWSER_CONFIG`, containing:

- `playwrightModule`: file URL to the installed driver’s `index.mjs`.
- `executablePath`: local Chrome executable.
- `baseUrl`: local frontend URL (default `http://localhost:3000`).
- `emailPickupDirectory`: API’s private development pickup directory.
- `adminEmail` and `adminPassword`: disposable CLI-provisioned ADMIN credentials.
- `outputDirectory`: absolute temporary screenshot directory outside the repository.

Then run `corepack pnpm --filter web test:browser` with `NIDHI_BROWSER_CONFIG` exported. Remove the disposable database, private configuration, and pickup files afterwards. No browser traces/videos or credential-bearing request logs are recorded.

Session 13 live acceptance uses `NIDHI_BROWSER_CONFIG=/absolute/private/config.json node frontend/web/tests/hardening-browser.mjs` from the repository root. Reuse the external Playwright module described above (no browser dependency is installed by this repository). Use a **freshly migrated disposable localhost database with no price**, a CLI-provisioned ADMIN, Development API with private pickup, and a production frontend whose `API_BASE_URL` points to that API. The script creates only simulated test accounts/transactions and exercises registration, verification, financial response-loss/replay, price-change rejection/reconfirmation, goals, receipts, authorization routing, session expiry/recovery, password reset, keyboard/dialog behavior and all five responsive widths. Never run it against developer/production data.

The owner-only JSON config has `baseUrl`, `playwrightModule`, `executablePath`, `emailPickupDirectory`, `outputDirectory`, `adminEmail`, and `adminPassword`, as in the existing browser smoke approach. Supply generated local test credentials privately; never commit the config, pickup files or cookies. Remove the disposable database and pickup/config files after validation. Screenshots/results contain test data only. The API rewrite target is captured by the frontend build, so set it for both the build and local run as appropriate.
