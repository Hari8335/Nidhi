# Session 6 — Frontend Foundation + Authentication UI

## 1. Frontend Architecture Decisions

- Built within `frontend/web/` using Next.js App Router, React 19, strict TypeScript, and Tailwind CSS v4 with curated CSS custom properties.
- **Strict single backend principle**: Adhered strictly to `AGENTS.md`. No second backend or Next.js API route handlers were created for Nidhi business logic. All authentication, authorization, validation, and session management remain authoritative in ASP.NET Core.
- **Routing structure**:
  - `(public)`: `/` (public landing with brand header and sign-in / registration links).
  - `(auth)`: `/login`, `/register`, `/verify-email`, `/forgot-password`, `/reset-password` sharing a focused, calm, centered `AuthLayout`.
  - `(customer)`: `/dashboard` and `/profile` sharing a responsive `CUSTOMER` `ApplicationShell`.
  - `admin`: `/admin` using the `ADMIN` `ApplicationShell`.
- **Modular responsibilities**:
  - `src/types/`: Frontend TypeScript DTOs matching the backend contract without exposing Identity internals.
  - `src/lib/api/`: Fetch-based typed API client, Problem Details parsing, and memory-only CSRF lifecycle.
  - `src/lib/auth/`: Role home routing and session helpers.
  - `src/lib/validation/`: Pure client-side validation for password guidance and confirmations.
  - `src/components/ui/`: Reusable accessible primitives (`Button`, `Input`, `PasswordInput`, `FormField`, `Card`, `Alert`, `StatusBadge`, `LoadingState`).
  - `src/components/shared/`: Reusable `Brand` component wrapping the approved logo asset.
  - `src/components/layout/`: `ApplicationShell` and `PageContainer`.
  - `src/components/auth/`: `AuthProvider`, `AuthForm`, and `AccountOverview`.

## 2. Design-System & Token Implementation

- Implemented the single premium dark theme specified in `docs/design/UI_UX_DESIGN_SPEC.md` centrally in `src/app/globals.css`.
- **Color tokens**:
  - Background: `#0B0D10` (Ink / Black)
  - Deep Navy: `#0A1424`
  - Surface: `#12161D`
  - Surface Elevated: `#181D25`
  - Primary / Gold 500: `#C9A227`
  - Gold Highlight / 300: `#F0D77B`
  - Gold Dark / 700: `#8C6A1D`
  - Foreground: `#F7F7F5`
  - Muted Foreground: `#A9B0BA`
  - Border: `#2A313B`
  - Success: `#2FBF71`
  - Warning: `#F3B33D`
  - Danger: `#E45B5B`
- **Typography**: Next.js Google font loading with Manrope (`subsets: ["latin"]`, `display: "swap"`), falling back to Inter and system sans-serif. Numbers use `tabular-nums` for financial clarity.
- **Controls & geometry**: 12px control radius (`--radius-control`), 16px card radius (`--radius-card`), 24px feature radius (`--radius-feature`).
- **Brand logo**: The approved primary asset `frontend/web/public/brand/nidhi-logo-primary.png` is preserved intact and wrapped by `Brand` with appropriate responsive sizing and subtle entry reveal.

## 3. API Configuration & Rewrite

- Centralized API configuration via `NEXT_PUBLIC_API_BASE_URL` (defaulting to `/api/v1`).
- Same-origin forwarding configured in `next.config.ts`:
  - Next.js rewrites `/api/:path*` to `API_BASE_URL` (default `http://localhost:5050/api/:path*`).
  - This avoids cross-origin cookie complications and keeps ASP.NET Core HttpOnly cookies strictly same-origin to the browser.
  - Security headers added: `Referrer-Policy: no-referrer` and `X-Content-Type-Options: nosniff`.

## 4. CSRF / Antiforgery Implementation

- Integrated `GET /api/v1/auth/antiforgery`.
- Token fetched automatically on demand for state-changing HTTP requests (`POST`) and kept strictly in memory.
- Token passed as `X-CSRF-TOKEN` header alongside browser-managed HttpOnly cookies (`credentials: "include"`).
- Token invalidated and refetched across identity transitions (`/auth/login`, `/auth/logout`, `/auth/reset-password`).
- Safe retry handling: Only pre-mutation `400 Bad Request` with `code: "CSRF_VALIDATION_FAILED"` triggers a single automatic refetch and retry. Network failures or mutations that could have executed are never retried automatically.
- No tokens or cookies are ever placed in `localStorage` or `sessionStorage`.

## 5. Authentication State Strategy

- Authoritative backend session queried via `GET /api/v1/auth/me`.
- Client states explicitly modeled in `AuthProvider`:
  - `loading`: Waiting for initial session resolution.
  - `anonymous`: Confirmed unauthenticated (401 response).
  - `authenticated`: User profile resolved (`userId`, `email`, `displayName`, `isEmailVerified`, `roles`).
  - `error`: Network or server failure while querying session; provides user retry without falsely asserting an anonymous state.
- Automatic synchronization:
  - Re-evaluates `/auth/me` on window focus and document visibility changes.
  - Background polling every 60 seconds while visible.
  - Listens to `nidhi:session-expired` events triggered on API 401 responses.

## 6. Route Protection

- Implemented in `ApplicationShell` as a client-side UX navigation guard (backend ASP.NET Core policies remain the true security boundary).
- Unauthenticated / anonymous users trying to access `/dashboard`, `/profile`, or `/admin` are immediately routed to `/login`.
- Authenticated `CUSTOMER` accounts attempting to view `/admin` are redirected to `/dashboard`.
- Authenticated `ADMIN` accounts attempting to view `/dashboard` or `/profile` are redirected to `/admin`.
- Prevents protected content flashes by rendering a clean accessible `LoadingState` until the session and role are confirmed.

## 7. Pages Implemented

1. **Public Home (`/`)**: Minimal landing page with Nidhi branding, value proposition, and links to registration and sign in. Displays prominent simulation disclosure.
2. **Registration (`/register`)**: Email, password (with client guidance), optional display name. Rejects invalid inputs, prevents double submit, handles server `EMAIL_ALREADY_EXISTS` (409) and validation errors, and presents success state leading to verification.
3. **Email Verification (`/verify-email`)**: Masked inputs for Account ID (`userId`) and verification code (`token`). Handles invalid, expired, or already-used codes (`INVALID_TOKEN`) with reassuring feedback.
4. **Login (`/login`)**: Email and password fields. Handles invalid credentials, lockout, rate limiting (`Retry-After`), and routes users to `/dashboard` or `/admin` according to verified role.
5. **Forgot Password (`/forgot-password`)**: Non-enumerating password recovery request. Always displays generic instructions reassuring the user without leaking account existence.
6. **Reset Password (`/reset-password`)**: Account ID, reset code, new password with guidance, and password confirmation. Clears sessions and routes to sign in upon success.
7. **Customer Dashboard (`/dashboard`)**: Customer application shell landing with welcome message, email verification reminder if unconfirmed, and placeholders for future financial features.
8. **Customer Profile (`/profile`)**: Read-only display of current account information (display name, email, verification status, role).
9. **Admin Overview (`/admin`)**: Administrator shell with dedicated administration sidebar and mobile drawer navigation. Clearly states administrative scope with no fake metrics.

## 8. Customer and Admin Application Shells

- **Customer Shell**:
  - Desktop: Sticky sidebar with Nidhi brand, primary navigation (`Dashboard`, `Profile`), and disabled indicators for future features (`Wallet`, `Gold`, `Transactions`, `Savings Goal`).
  - Mobile (<1024px): Responsive top header with brand and account summary; fixed bottom navigation bar with `Home`, disabled future actions, and an expandable `More` sheet containing `Profile`.
- **Admin Shell**:
  - Dedicated administration branding badge ("Administrator").
  - Sidebar and mobile `<details>` navigation for administrative tools (`Overview`, plus upcoming `Customers`, `Transactions`, `Ledger`, `Gold Prices`, `Audit Logs`).
- **Header & Account Actions**:
  - Displays user display name or email.
  - Server-backed `Sign out` action calling `POST /api/v1/auth/logout`, clearing client session and antiforgery state, and routing to `/login`.

## 9. Testing & Validation Performed

1. **Pure Unit / Integration Tests (`frontend/web/tests/auth.test.mjs`)**:
   - 16 tests using Node.js built-in `--test` runner (zero third-party test dependencies).
   - Validates Problem Details parsing, error masking, password rules, role routing, memory-only CSRF lifecycle, concurrent request token deduplication, identity transition token clearing, and retry safety.
2. **Real-Browser End-to-End Validation (`frontend/web/tests/browser-smoke.mjs`)**:
   - Executed against a live Kestrel API backend, local PostgreSQL, and private email pickup files.
   - Validated complete lifecycle:
     - Public page rendering across viewports (1440px desktop, 768px tablet, 390px mobile) with zero horizontal overflow.
     - Customer registration and antiforgery token issuance.
     - Development email verification pickup and submission.
     - Customer login and HttpOnly `.Nidhi.Session` cookie validation (`SameSite=Strict`).
     - Session persistence across reloads and `/auth/me` resolution.
     - Customer shell, profile page, and mobile `More` navigation.
     - Access control enforcement: CUSTOMER denied `/admin` and redirected to `/dashboard`.
     - Server-side logout revoking access to `/dashboard`.
     - Non-enumerating forgot-password request.
     - Password reset token consumption and login with new password.
     - Provisioned ADMIN login and navigation to `/admin`.
     - ADMIN excluded from customer shell.
     - Inline form validation, keyboard tab navigation, focus rings, and `prefers-reduced-motion` compliance.
     - Clean browser storage validation: `localStorage.length === 0` and `sessionStorage.length === 0`.
     - Server error responses: 429 rate limit with `Retry-After` countdown and connection failure fallback.
3. **Static Analysis & Build**:
   - `corepack pnpm --filter web lint`: 0 errors, 0 warnings.
   - `corepack pnpm --filter web typecheck`: 0 errors.
   - `corepack pnpm --filter web build`: Next.js production build succeeded with static prerendering for all routes.
   - Backend regression: `dotnet build backend/Nidhi.sln` succeeded with 0 errors and 0 warnings; all unit tests passed.
   - Health check: `GET /health` returned HTTP 200 `{"status":"ok"}`.
   - `git diff --check`: 0 whitespace or formatting errors.

## 10. Deviations from Specification

- None. Color tokens, typography, radii, spacing, logo usage, motion constraints, and API contracts follow `docs/design/UI_UX_DESIGN_SPEC.md` and `docs/design/API_CONTRACT.md` exactly.
- Brand logo: The provided raster asset `nidhi-logo-primary.png` was retained and wrapped in a reusable `Brand` component without vector alteration or AI regeneration.

## 11. Backend Defects Discovered

- No backend defects were discovered. The Session 5 ASP.NET Core API, Identity cookie authentication, antiforgery middleware, and Problem Details envelope functioned exactly as documented.
