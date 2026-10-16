# Authentication foundation development

Session 5 implements backend authentication only. Apply the existing migration using [DEVELOPMENT_DATABASE.md](DEVELOPMENT_DATABASE.md). No new migration, frontend auth UI, business endpoints, real payments, financial postings, or default prices are introduced.

## Configuration and security choices

ASP.NET Core Identity uses the existing Guid-keyed PostgreSQL store and built-in password hashing. Emails are unique after Identity normalization. Passwords require 12–128 characters for public requests, uppercase, lowercase, a digit, and a nonalphanumeric character. Login locks an account for 15 minutes after five failed attempts. There are only `CUSTOMER` and `ADMIN` roles; their creation is concurrency-safe and part of registration/provisioning transactions.

`.Nidhi.Session` is an HttpOnly, SameSite=Strict cookie, with no Domain attribute, Path=/, and no remember-me/persistent browser expiry. The encrypted authentication ticket has an absolute eight-hour lifetime with no sliding renewal. Both Identity and antiforgery cookies require Secure outside Development; Development permits localhost HTTP and uses Secure on HTTPS. Deploy behind HTTPS with a same-origin `/api` proxy. No permissive CORS policy or untrusted forwarded-header processing is enabled. Separate browser origins and trusted proxy configuration must be designed explicitly when deployment is selected.

Each authenticated request validates the Identity security stamp against PostgreSQL. Logout changes that stamp before clearing the cookie; **all sessions for that user are revoked**, including copied cookies. Successful password reset also revokes all existing sessions. This deliberate foundation tradeoff avoids a new session store at the cost of a database lookup per authenticated request. Persist and protect the ASP.NET Core Data Protection key ring in a deployment-specific location before deployment; multiple API instances need the same protected key ring. This session does not choose hosting or a key-management service.

Verification and reset tokens use Identity's Data Protection provider with a one-hour lifetime. Verification rejects already-confirmed accounts; successful resets change the security stamp, preventing reset-token reuse. Tokens are account-bound. Password recovery does not confirm email. Production responses/logs never contain these tokens or passwords.

## Local identity email pickup

Set an **absolute, private directory outside the repository** in the API process:

```sh
export IdentityEmail__PickupDirectory="$HOME/.local/share/nidhi/identity-emails"
# ConnectionStrings__NidhiDb must already be configured securely.
cd backend
dotnet run --project src/Nidhi.Api
```

In Development only, the sender creates one randomly named JSON file for each verification/reset message, containing `purpose`, `userId`, `email`, and `token`. On Unix the directory is owner-only (0700) and files are created owner-only (0600). On Windows, configure an owner-only ACL on the pickup directory before use. Treat these files as credentials: inspect locally, do not paste into issues/logs or serve them over HTTP, and delete after testing. No tokens are printed by the API. `.identity-emails/` is also ignored as a guard against accidental local pickup under the repository.

Registration writes the verification message before committing the transaction. If delivery fails, the user/profile/wallet/holding/role-membership changes roll back; the same registration can be retried. A file written just before a failed database commit is unusable because its user was not committed. This implementation does not provide an email outbox. Pending accounts can request another message through `POST /api/v1/auth/resend-verification`.

Forgot-password always returns the same generic 200 message for known/unknown accounts and delivery failure. Failures produce a safe warning without address/token/exception data; retry the request after delivery is restored. Response equivalence is not a constant-time delivery guarantee.

The default `IdentityEmail:Provider=Pickup` sender refuses to write pickup files outside Development. With that provider, production registration returns `503 IDENTITY_EMAIL_UNAVAILABLE`; recovery remains generic and logs a safe delivery warning. Gmail delivery is available through the configuration below. No paid provider or notification platform is integrated.

## Gmail delivery

Set `IdentityEmail:Provider=Gmail` to send verification and password reset messages through Gmail instead of pickup files. Messages contain a styled Verify email or Reset password button and a plain-text fallback link. `IdentityEmail:PublicBaseUrl` must be an explicitly configured trusted frontend origin (HTTPS, or localhost HTTP). Tokens are URL-encoded in the link fragment, which is not sent to the web server; the browser removes it from the address bar and retains it only in memory. Opening a link does not mutate an account; the user confirms verification or submits a new password through the existing CSRF-protected POST endpoint. The sender connects to `smtp.gmail.com:587` with required STARTTLS and normal certificate validation, authenticates as the sender address, and uses a 15-second cancellation deadline. Delivery failures return the existing sanitized `503 IDENTITY_EMAIL_UNAVAILABLE`; registration rolls back as before. An SMTP server accepting a message does not guarantee inbox delivery. If the database commit fails after acceptance, the message cannot verify an uncommitted account.

For local development:

1. Enable 2-Step Verification for the Google account that will send messages, then create an app password named `Nidhi` at <https://myaccount.google.com/apppasswords>. See [Google's app password instructions](https://support.google.com/accounts/answer/185833). Some account policies do not permit app passwords.
2. From the repository root, run `python3 scripts/configure-gmail.py`. Enter the sender address and the app password at the prompts. The password input is hidden. Do not use your normal Google password or paste credentials into chat.
3. Restart the Development API with its existing `ConnectionStrings__NidhiDb` configuration. The setup script saves email settings to ASP.NET Core user-secrets, outside the repository; ASP.NET Core loads those settings in Development. User-secrets are local development storage, not an encrypted production secret manager.
4. Register a new test account and check its inbox/spam folder. Select Verify email in the message, then confirm on the verification page. Pending accounts can request a fresh link from `/verify-email`; repeated signup reports `EMAIL_VERIFICATION_REQUIRED` and offers that page rather than suggesting signup completed. The initial password is unchanged. Password recovery sends a reset link through the same Gmail sender and does not confirm email.

For deployments, supply `IdentityEmail__Provider=Gmail`, `IdentityEmail__Gmail__Address`, `IdentityEmail__Gmail__AppPassword`, and `IdentityEmail__PublicBaseUrl` through protected environment/secret configuration. Do not put the app password in tracked files. To return to private local pickup, set `IdentityEmail:Provider` to `Pickup` and configure its absolute pickup directory.

The SMTP implementation uses MailKit 4.17.0 for authenticated TLS delivery. `System.Security.Cryptography.Pkcs` is pinned to 10.0.13 to override the vulnerable transitive version brought by MimeKit.

Certificate revocation checks are enabled by default. If local certificate validation fails specifically because revocation status cannot be obtained, first restore access to the certificate authority's revocation service. An explicit local exception is available with `IdentityEmail:Gmail:CheckCertificateRevocation=false`, after accepting that a revoked certificate may go undetected. This setting is rejected outside Development. TLS encryption and standard hostname, validity, and trust-chain checks remain enabled. Never install an accept-all certificate callback.

## CSRF and API flow

All unsafe `/api` requests require ASP.NET Core antiforgery validation, **including anonymous registration, login, verification and password recovery**. Fetch `GET /api/v1/auth/antiforgery`, retain both its HttpOnly `.Nidhi.Antiforgery` cookie and JSON request token, and send the request token as `X-CSRF-TOKEN`. Tokens belong only in memory, never localStorage/sessionStorage. All auth responses use `Cache-Control: no-store`.

Successful login, logout and reset clear the antiforgery cookie. Obtain a fresh token after each transition, or session expiration; an anonymous token cannot be reused after login. Missing/invalid tokens return 400 `CSRF_VALIDATION_FAILED`. Authentication/authorization failures return JSON 401/403, never an HTML redirect. The verified-customer policy consults current database email state, so an existing session can use it immediately after successful verification.

For a local API testing client, enable its cookie jar, disable persistent request/history storage for passwords and tokens, and use this sequence. Supply values locally without recording actual secrets in scripts or source:

| Step | Request | Expected result |
|---|---|---|
| 1 | `GET /api/v1/auth/antiforgery` | 200 `{ "token": "<request-token>" }`, antiforgery cookie |
| 2 | `POST /api/v1/auth/register` with email/password/optional displayName | 201, CUSTOMER and zero balances; verification pickup file |
| 3 | `POST /api/v1/auth/verify-email` with userId/token from pickup | 200; repeat/invalid token returns 400 `INVALID_TOKEN` |
| 4 | `POST /api/v1/auth/login` with email/password | 200, session cookie; fetch fresh antiforgery token |
| 5 | `GET /api/v1/auth/me` | 200 safe user DTO |
| 6 | `POST /api/v1/auth/logout` with fresh CSRF header | 204; fetch fresh anonymous antiforgery token |
| 7 | `GET /api/v1/auth/me` | 401; replaying the old cookie also returns 401 |
| 8 | `POST /api/v1/auth/forgot-password` with email | Generic 200; reset pickup file for existing account |
| 9 | `POST /api/v1/auth/reset-password` with userId/token/newPassword | 200; fetch fresh antiforgery token |
| 10 | `POST /api/v1/auth/login` with new password | 200; old password no longer works |

Every POST in this table includes `X-CSRF-TOKEN` and the cookie jar. Body shapes are in [API_CONTRACT.md](design/API_CONTRACT.md); Development OpenAPI is `/openapi/v1.json`. Registration rejects extra properties, including role/roles/phone; login rejects remember-me and other unsupported properties. Duplicate normalized email returns 409 `EMAIL_ALREADY_EXISTS`.

## Authorization policies

Use `[Authorize(Policy = AuthPolicies.Customer)]` for authenticated CUSTOMER access, `AuthPolicies.Admin` for ADMIN, and `AuthPolicies.VerifiedCustomer` for authenticated CUSTOMER plus confirmed email. An ADMIN is not implicitly a CUSTOMER. Unverified customers may login/use `me` and verification/recovery; future financial endpoints must use the verified policy and derive ownership from the authenticated user ID. No production probe, admin dashboard or financial endpoint is added.

## Operational ADMIN provisioning

Run only as an authorized operator against the intended, migrated database. Set `ConnectionStrings__NidhiDb`, `AdminProvisioning__Email`, and `AdminProvisioning__Password` from a secret manager/environment (or `AdminProvisioning:Email` and `AdminProvisioning:Password` through ASP.NET Core user-secrets). Do not pass credentials in command-line arguments or put them in appsettings files. Example zsh input avoids shell history and terminal echo:

```sh
read -rs 'AdminProvisioning__Email?Admin email: '
printf '\n'
read -rs 'AdminProvisioning__Password?Unique admin password: '
printf '\n'
export AdminProvisioning__Email AdminProvisioning__Password
cd backend
dotnet run --project src/Nidhi.Api --no-launch-profile -- provision-admin
unset AdminProvisioning__Email AdminProvisioning__Password
```

The command uses the real PostgreSQL/Identity store and exits without opening an HTTP listener. It takes a PostgreSQL transaction advisory lock for concurrent operational retries, ensures the two roles, creates an email-confirmed ADMIN, and writes `ADMIN_PROVISIONED` audit evidence in the same transaction. No customer balances/profile are created. Provisioning assumes the operator has verified the supplied admin email through the operational process. Logs contain only success/already-provisioned/failure outcomes.

Repeating the command for an existing ADMIN is a no-op: it does not change the password or append another audit event. It refuses to promote a CUSTOMER or an account with any unexpected role set, and exits nonzero on failure. Password rotation is a separate operation; rerunning provisioning is not a password reset. Never publish this operation as an HTTP endpoint. The API project includes a nonsecret UserSecretsId for optional local `dotnet user-secrets` usage.

## Abuse protection

Built-in in-process fixed-window rate limits are partitioned by the connection's remote IP and policy. No queued requests are accepted. Defaults per 60 seconds:

| Policy | Requests |
|---|---:|
| register | 5 |
| login | 10 |
| forgot-password | 5 |
| resend-verification | 5 |
| tokens (verify-email/reset-password combined) | 20 |

`GET /api/v1/auth/antiforgery` has no application rate-limit policy and no global limiter is configured. Token refreshes after login/logout/reset or session expiration do not consume the `tokens` allowance; that shared 20/minute allowance applies only to verification/reset POSTs. Normal auth flows can refresh CSRF tokens without application throttling. `me` and logout also have no endpoint rate-limit policy.

Configure `Auth:RateLimits:<policy>:PermitLimit` and `Auth:RateLimits:<policy>:WindowSeconds` through .NET configuration; positive integers are required. Environment names use double underscores, for example `Auth__RateLimits__login__PermitLimit`. For the hyphenated policy, use a configuration provider or `env 'Auth__RateLimits__forgot-password__PermitLimit=5' ...`. Rejections return 429 `RATE_LIMITED` with `Retry-After`. Limits reset on process restart and are per API instance. The deployment must account for shared-IP clients and trusted proxies; this foundation does not claim cluster-wide limits.

## Automated validation

Follow the database guide to configure `NIDHI_TEST_POSTGRES` securely, then run from `backend/`:

```sh
dotnet restore Nidhi.sln
dotnet build Nidhi.sln
dotnet test Nidhi.sln
```

The authentication suite creates only a random `nidhi_auth_test_*` database, applies the real migration, hosts the actual API with a capturing test email sender, tests cookie/CSRF flows and authorization using test-only probe controllers, and drops its own database at teardown. The existing persistence suite also validates PostgreSQL 18. Without `NIDHI_TEST_POSTGRES`, database-dependent tests explicitly skip; that is not PostgreSQL validation. Local pickup behavior is also tested separately without emailing anyone.

Session 5 resolves two contract omissions: `/auth/me` supplies the requested safe current-user endpoint; CSRF/rate-limit/email-delivery/internal-failure error codes are added to the standard envelope. Verification and reset retain the approved `INVALID_TOKEN` code. No repository-local standalone SRS currently exists; no `.docx` or duplicate requirements document is introduced. Existing requirements/design documents remain authoritative for now; `PROJECT_SPEC.md` and the current requirements/design documents supply the repository requirements, consistent with the existing historical missing-source note.

## Pending accounts and resend

Registration stores an unverified Identity account and zero balances atomically. A pending account is not email-confirmed and cannot use simulated financial APIs. Its email remains unique; another signup cannot silently overwrite its password or profile. `EMAIL_VERIFICATION_REQUIRED` lets the frontend guide the user back to verification.

Anonymous `POST /api/v1/auth/resend-verification` accepts only `{ "email": "..." }`, requires antiforgery, and is limited to five requests per IP per minute. It returns the same generic 200 message for pending, verified, unknown, and delivery-failure cases; only pending accounts receive mail. The UI adds a 60-second resend cooldown; server rate limiting remains authoritative. Resend creates a fresh one-hour Identity token without changing credentials, sessions, or financial records. Earlier unexpired links remain usable until verification succeeds, after which all verification links are rejected. No claim of constant-time responses or distributed throttling is made.
