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
- `/verify-email`: enter `userId` and `token` from the private verification pickup JSON. The code is masked and never placed in a URL or persisted in browser storage.
- `/login`: routes the actual CUSTOMER to `/dashboard` and ADMIN to `/admin`.
- `/forgot-password`: always shows a generic recovery result.
- `/reset-password`: enter the reset pickup `userId`/`token`, new password, and confirmation, then sign in again.
- `/profile`: read-only safe current-user details from `/auth/me`; no planned profile business endpoint is called.

Pickup files are development credentials. Read them privately, never serve or commit them, and delete them after testing. The backend has no resend-verification endpoint; already-confirmed/expired/used codes return the documented generic invalid-token result. This session does not introduce emailed URLs or a production email provider.

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
