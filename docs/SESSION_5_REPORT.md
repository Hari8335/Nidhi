# Session 5 — Authentication Foundation

1. **Files created/modified.** Implemented on `feat/auth-foundation`. The exact review inventory is in item 18. No frontend, financial use case, schema migration, or Git commit was added.

2. **Packages added.** No NuGet or JavaScript packages. Added the `Microsoft.AspNetCore.App` shared-framework reference to Infrastructure for Identity sign-in/token and hosting services. The existing repository-local `dotnet-ef` 10.0.12 tool was restored for validation. Added a nonsecret API UserSecretsId for operational local configuration.

3. **Endpoints implemented.** Under `/api/v1/auth`: GET `antiforgery`, POST `register`, POST `verify-email`, POST `login`, POST `logout`, GET `me`, POST `forgot-password`, POST `reset-password`. Development OpenAPI includes request/response DTOs, status codes, cookie security and the required CSRF header.

4. **Cookie + CSRF design.** HttpOnly, SameSite=Strict, Secure outside Development; localhost HTTP supported. Nonpersistent browser cookie with a fixed eight-hour ticket, no sliding expiration/remember-me. Every authenticated request checks the real Identity security stamp. All unsafe `/api` requests, including anonymous auth POSTs, validate `X-CSRF-TOKEN` against the antiforgery cookie. Login/logout/reset clear the antiforgery cookie; the client fetches fresh state. Auth responses are not cached. API failures use Problem Details, never login redirects.

5. **Registration/verification/login/logout/reset.** Registration atomically creates the Identity user, CUSTOMER membership, profile, 0.00 wallet and 0.00000000 gold holding. Unknown fields/role selection are rejected. Duplicate normalized emails return 409, including concurrent registration. Verification and reset use one-hour Identity tokens with invalid/wrong-account/expired/reused-token rejection. Login uses the built-in password hasher, lockout and session cookies. Logout and password reset revoke all user sessions, including copied cookies. Recovery returns the same generic response for known/unknown accounts and delivery failures. Development email uses private pickup files; registration delivery failure rolls back for retry. No Identity internals appear in the current-user DTO.

6. **Authorization policies.** `Customer` requires authenticated CUSTOMER; `Admin` requires authenticated ADMIN; `VerifiedCustomer` additionally checks the current database email-confirmed state. Unverified customers can authenticate for verification/recovery; future financial endpoints must use the verified policy. Test-only authorization probes are not registered in production.

7. **Admin provisioning approach.** `dotnet run --project src/Nidhi.Api --no-launch-profile -- provision-admin` from `backend/`, with environment/user-secrets configuration. No HTTP provisioning endpoint or hardcoded credentials. Transactional PostgreSQL advisory lock, idempotent role/account creation, refusal to promote existing customers, unchanged existing admin passwords, and one `ADMIN_PROVISIONED` audit event on creation. Executes without an HTTP listener and logs only safe outcomes.

8. **Rate limiting.** Built-in in-process, per-remote-IP fixed windows; configurable positive permit/window settings. Defaults per minute: register 5, login 10, forgot-password 5, verify/reset combined 20. Antiforgery GET is unthrottled by the application; refreshes do not consume verification/reset permits. No queue. 429 `RATE_LIMITED` and `Retry-After`. Identity locks out after five failed password attempts for 15 minutes. No Redis/distributed infrastructure.

9. **Tests added.** PostgreSQL HTTP tests cover atomic initialization, duplicate/role rejection, concurrent signup, delivery rollback/retry, email confirmation persistence/reuse, login failure, safe current-user fields, copied-cookie logout revocation, password-reset validity/reuse/account binding/expiration/session revocation, non-enumerating recovery, CUSTOMER/ADMIN/verified policies, idempotent audited provisioning, CSRF identity transitions, rate limits, production Secure cookies, eight-hour session expiry, account lockout, OpenAPI, and framework error envelopes. Unit tests cover private local email files and permissions, nondevelopment refusal, and invalid pickup configuration.

10. **Backend build/test results.** `dotnet restore Nidhi.sln`, `dotnet build Nidhi.sln --no-restore`, and `dotnet test Nidhi.sln --no-build --no-restore`, from `backend/`. Restore/build passed with zero warnings/errors. Final test result: 27 passed (6 unit, 21 integration), zero failures/skips. The integration count includes existing health/OpenAPI/persistence tests.

11. **PostgreSQL integration result.** Passed against the existing local Docker PostgreSQL 18 service. Both suites use randomly named disposable databases and the real EF migrations/Identity store, not EF InMemory. Test databases were dropped; the existing development data was not changed.

12. **Auth-flow validation result.** Passed both in the automated test host and against a separate live Kestrel process: antiforgery → register → verify → login → current user → logout → 401 → forgot password → reset → new-password login. The live run used real private local email pickup, validated CUSTOMER-only signup/CSRF rejection, executed the ADMIN CLI twice, and logged in as the provisioned ADMIN. Temporary process/database/pickup files were cleaned up.

13. **`/health` result.** HTTP 200 from the live updated API. Existing Development and Production liveness tests also pass without database configuration.

14. **Frontend regression result.** Unchanged frontend. `pnpm --filter web lint`, `typecheck`, and `build` all passed through the existing `corepack pnpm` launcher (`pnpm` was not directly on PATH).

15. **`git diff --check`.** Passed.

16. **Secrets review.** `.env` remains ignored and untracked; `.identity-emails/` is ignored defensively. No actual passwords, identity tokens, or environment credentials were added to review files. A scan for the actual ignored local database password across tracked and new review files found no matches. Test passwords/tokens are generated at runtime. Pickup files are owner-only on Unix, removed after validation, and never logged or served by an endpoint. No Git commit was created.

17. **Discrepancies found.** The approved contract had no current-user endpoint; `/api/v1/auth/me` was added and documented. The error catalog lacked CSRF, rate-limit, delivery, unexpected-failure and HTTP method/media-type codes; these now use the approved envelope. No standalone Nidhi SRS artifact is present; the current product specification, requirements and design documents were used. Production email delivery is deliberately unavailable until an `IIdentityEmailSender` implementation is registered; the development sender cannot leak tokens in production. Logout revokes all user sessions rather than maintaining a separate session table. Hosting-specific Data Protection key persistence, trusted proxies and any cross-origin deployment remain deployment concerns. No resend-verification endpoint/provider/outbox was added beyond the approved contract.

18. **Exact files to review before commit.** Review all of these, especially service transactions, cookie/CSRF configuration, operational provisioning, and PostgreSQL security tests:

    Created:
    - `backend/src/Nidhi.Api/Authentication/AuthConfiguration.cs`
    - `backend/src/Nidhi.Api/Authentication/AuthOpenApiTransformer.cs`
    - `backend/src/Nidhi.Api/Authentication/AuthProblems.cs`
    - `backend/src/Nidhi.Api/Authentication/VerifiedCustomerAuthorization.cs`
    - `backend/src/Nidhi.Api/Controllers/AuthController.cs`
    - `backend/src/Nidhi.Application/Authentication/AuthContracts.cs`
    - `backend/src/Nidhi.Infrastructure/Identity/AdminProvisioner.cs`
    - `backend/src/Nidhi.Infrastructure/Identity/AuthService.cs`
    - `backend/src/Nidhi.Infrastructure/Identity/LocalIdentityEmailSender.cs`
    - `backend/tests/Nidhi.IntegrationTests/AuthTests.cs`
    - `backend/tests/Nidhi.UnitTests/LocalIdentityEmailSenderTests.cs`
    - `docs/DEVELOPMENT_AUTHENTICATION.md`
    - `docs/SESSION_5_REPORT.md`

    Modified:
    - `.gitignore`
    - `backend/src/Nidhi.Api/Nidhi.Api.csproj`
    - `backend/src/Nidhi.Api/Program.cs`
    - `backend/src/Nidhi.Infrastructure/Nidhi.Infrastructure.csproj`
    - `docs/DEVELOPMENT_DATABASE.md`
    - `docs/design/API_CONTRACT.md`
    - `docs/design/API_ERRORS.md`
    - `docs/design/TRACEABILITY.md`

19. **Suggested Conventional Commit message.** `feat(auth): implement Identity cookie authentication foundation`

20. **Documentation/security reconciliation (2026-09-25).** Reconciled the current-user DTO/statuses, logout revocation, error semantics, auth traceability and antiforgery rate-limit scope. The contract has exactly eight implemented auth endpoints and 30 total designed endpoints. No code defect was found or code changed in this reconciliation; no frontend or financial features were introduced. No repository-local standalone SRS currently exists; existing requirements/design documents remain authoritative and no duplicate requirements artifact was added. Secret-file/signature and local database-password scans found no tracked/review-file credentials. Fresh `dotnet build backend/Nidhi.sln` passed with zero warnings/errors; `dotnet test backend/Nidhi.sln` passed all 27 tests (6 unit, 21 integration), with zero failures/skips, after starting the stopped local Docker/PostgreSQL service and running outside the socket-restricted sandbox. Local Markdown file links (160) and `git diff --check` passed. Session 5 is ready to commit; no commit was created.
