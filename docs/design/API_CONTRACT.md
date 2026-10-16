# Nidhi v1 REST API Contract

This document specifies the versioned REST API surface (`/api/v1/...`) for Nidhi v1. It implements the requirements in [OPEN_DECISIONS.md](../requirements/OPEN_DECISIONS.md), [FUNCTIONAL_REQUIREMENTS.md](../requirements/FUNCTIONAL_REQUIREMENTS.md), and [USER_STORIES.md](../requirements/USER_STORIES.md).

---

## 1. Architectural & Protocol Conventions

1. **Base Path**: All endpoints are prefixed with `/api/v1`.
2. **Data Format**: Requests and responses use `application/json; charset=utf-8`.
3. **Identifiers**: All primary resources use **UUIDv7** represented as standard 36-character hyphenated UUID strings (e.g. `018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b`).
4. **Numeric Precision**:
   - LKR currency values: serialized as JSON numbers with 2 decimal places (e.g. `100.00`).
   - Gold prices: serialized as JSON numbers with 4 decimal places (e.g. `30000.0000`).
   - Gold grams: serialized as JSON numbers with 8 decimal places (e.g. `0.00333333`).
5. **Date-Time Representation**: All timestamps are formatted as ISO 8601 UTC strings (`YYYY-MM-DDTHH:mm:ss.fffZ`).
6. **Authentication & Authorization**:
   - Authentication uses ASP.NET Core Identity with secure, `HttpOnly`, `SameSite=Strict` cookies.
   - Browser JWTs and `localStorage` token storage are prohibited (OD-018).
   - Roles: `CUSTOMER` and `ADMIN`.
   - Customer ownership is **strictly derived from the authenticated session principal** (`User.FindFirstValue(ClaimTypes.NameIdentifier)`). Client requests cannot supply arbitrary `customerId` parameters to access customer data.
   - State-changing requests (`POST`, `PUT`, `PATCH`, `DELETE`) from web clients include an Anti-Forgery / CSRF validation header (`X-CSRF-TOKEN`).
   - The antiforgery request token is obtained explicitly via `GET /api/v1/auth/antiforgery` (ODQ-003).
   - Neither authentication session tokens nor antiforgery tokens may ever be stored in `localStorage` or `sessionStorage` (OD-018, ODQ-003).
7. **Pagination**:
   - Query parameters: `page` (default `1`), `pageSize` (default `20`, maximum `100`).
   - Response envelope contains items and pagination metadata (`page`, `pageSize`, `totalCount`, `totalPages`).

---

## 2. API Summary by Group

| Group | Endpoint Count | Purpose |
|---|---|---|
| **Public / Authentication** | 10 | Visitor landing disclosures, registration, login, logout, verification, password recovery, antiforgery token, current user. |
| **Customer** | 12 | Dashboard, profile, wallet, funding, price, gold saving, holdings, transactions, savings goal. |
| **Administrator** | 9 | Operational dashboard, customer inspection, transaction/ledger review, price publication, audit trail. |
| **Total** | **30** | Complete v1 capability set without speculative endpoints. |

---

The contract contains 8 implemented authentication endpoints plus 1 planned public endpoint, 12 planned customer endpoints and 9 planned administrator endpoints. The total of 30 is the v1 design surface, not the count of implemented business endpoints.

## 3. Public & Authentication Endpoints

### 3.1. `POST /api/v1/auth/register`
- **Description**: Registers a new customer account. Creates an identity user and associated `CustomerProfile`, `Wallet` (0.00 LKR), and `GoldHolding` (0.00000000 g). Public registration creates `CUSTOMER` accounts only (OD-011).
- **Authentication**: None (Anonymous).
- **Request Body**:
  ```json
  {
    "email": "customer@example.com",
    "password": "SecurePassword123!",
    "displayName": "Amara Silva"
  }
  ```
  *(Note: `displayName` is optional. Phone number is excluded under OD-001).*
- **Responses**:
  - `201 Created`:
    ```json
    {
      "customerId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
      "email": "customer@example.com",
      "displayName": "Amara Silva",
      "isEmailVerified": false,
      "message": "Registration successful. Please verify your email before using simulated financial features."
    }
    ```
  - `400 Bad Request`: `VALIDATION_ERROR` (invalid email format, weak password, displayName > 100 chars).
  - `409 Conflict`: `EMAIL_ALREADY_EXISTS` for a verified account; `EMAIL_VERIFICATION_REQUIRED` for a pending unverified account. Repeated signup never replaces the existing password, profile, or balances. The frontend offers verification/resend for a pending account.

---

### 3.2. `POST /api/v1/auth/verify-email`
- **Description**: Verifies a customer's email address using a one-time token (OD-002, FR-AUTH-005).
- **Authentication**: None.
- **Request Body**:
  ```json
  {
    "userId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
    "token": "CfDJ8...token..."
  }
  ```
- **Responses**:
  - `200 OK`: `{"message": "Email successfully verified. You are now eligible to use simulated financial features."}`
  - `400 Bad Request`: `VALIDATION_ERROR` or `INVALID_TOKEN` (token expired, reused, or invalid).

---

### 3.2a. `POST /api/v1/auth/resend-verification`

- **Authentication**: Anonymous; paired antiforgery cookie and `X-CSRF-TOKEN` required.
- **Request**: `{ "email": "customer@example.com" }`; unsupported fields are rejected.
- **Response**: Generic `200 OK` with `message` for pending, verified, unknown addresses, and delivery failures. Only pending accounts receive a fresh one-hour verification link. No user IDs or tokens are returned.
- **Errors**: `400 VALIDATION_ERROR` / `CSRF_VALIDATION_FAILED`; `429 RATE_LIMITED` with `Retry-After`. Default limit: five requests per remote IP per 60 seconds.
- Resend never changes passwords, profiles, sessions, or balances. Prior unexpired verification links remain valid until confirmation succeeds; successful confirmation rejects subsequent use. Email links carry credentials in a URL fragment and require an explicit CSRF-protected POST to confirm.

### 3.3. `POST /api/v1/auth/login`
- **Description**: Authenticates a user (Customer or Admin) and issues a secure `HttpOnly` session cookie (OD-018).
- **Authentication**: None.
- **Request Body**:
  ```json
  {
    "email": "customer@example.com",
    "password": "SecurePassword123!"
  }
  ```
- **Responses**:
  - `200 OK`: (Sets `Set-Cookie: .Nidhi.Session=...; HttpOnly; Secure; SameSite=Strict; Path=/`)
    ```json
    {
      "userId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
      "email": "customer@example.com",
      "role": "CUSTOMER",
      "isEmailVerified": true
    }
    ```
  - `401 Unauthorized`: `INVALID_CREDENTIALS` (does not disclose whether email exists).

---

### 3.4. `POST /api/v1/auth/logout`
- **Description**: Revokes all sessions for the authenticated user and clears the current session cookie (OD-018, FR-AUTH-003).
- **Authentication**: Authenticated (`CUSTOMER` or `ADMIN`).
- **Request Body**: None.
- **Responses**:
  - `204 No Content`: (Sets `Set-Cookie: .Nidhi.Session=; Max-Age=0; Path=/`)

---

### 3.5. `POST /api/v1/auth/forgot-password`
- **Description**: Initiates password recovery via a one-time email token (OD-002, FR-AUTH-006). Returns a non-enumerating generic response.
- **Authentication**: None.
- **Request Body**:
  ```json
  {
    "email": "customer@example.com"
  }
  ```
- **Responses**:
  - `200 OK`:
    ```json
    {
      "message": "If the email is registered, password reset instructions have been sent."
    }
    ```

---

### 3.6. `POST /api/v1/auth/reset-password`
- **Description**: Completes password recovery using the one-time reset token (FR-AUTH-006).
- **Authentication**: None.
- **Request Body**:
  ```json
  {
    "userId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
    "token": "CfDJ8...resetToken...",
    "newPassword": "NewSecurePassword123!"
  }
  ```
- **Responses**:
  - `200 OK`: `{"message": "Password has been successfully reset. You may now log in."}`
  - `400 Bad Request`: `INVALID_TOKEN` or `VALIDATION_ERROR`.

---

### 3.7. `GET /api/v1/public/gold-price`
- **Description**: Publicly accessible quote of the current simulated gold price for the marketing landing page.
- **Authentication**: None.
- **Responses**:
  - `200 OK`:
    ```json
    {
      "priceVersionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
      "pricePerGramLkr": 30000.0000,
      "publishedAtUtc": "2026-09-24T06:00:00.000Z",
      "isFresh": true
    }
    ```
  - `503 Service Unavailable`: `PRICE_UNAVAILABLE` (if production has not published its first price yet, OD-017).

---

### 3.8. `GET /api/v1/auth/antiforgery`
- **Description**: Returns an antiforgery request token for use in the `X-CSRF-TOKEN` header on state-changing requests (ODQ-003). Sets and maintains the associated ASP.NET Core antiforgery cookie.
- **Authentication**: None (Anonymous / Authenticated).
- **Responses**:
  - `200 OK`:
    ```json
    {
      "token": "CfDJ8...antiforgeryRequestToken..."
    }
    ```

---

### 3.9. `GET /api/v1/auth/me` — Session 5 contract addition
- **Description**: Returns the current authenticated identity, for CUSTOMER or ADMIN. No Identity internals are exposed.
- **Authentication**: Valid `.Nidhi.Session` cookie required; CUSTOMER or ADMIN, including unverified customers. No CSRF header is required for this GET.
- **Response DTO**: `CurrentUserResponse`: `userId` (UUID), `email` (string), `displayName` (nullable string), `isEmailVerified` (boolean), `roles` (string array containing CUSTOMER or ADMIN).
- **Responses**:
  - `200 OK`: `{ "userId": "<uuid>", "email": "customer@example.com", "displayName": null, "isEmailVerified": false, "roles": ["CUSTOMER"] }`
  - `401 Unauthorized`: `AUTHENTICATION_REQUIRED`.
  - `500 Internal Server Error`: safe `INTERNAL_ERROR` Problem Details on unexpected failure.
- Only the five DTO fields above are returned. Password hashes, security/concurrency stamps, verification/reset tokens and other Identity internals are excluded. `displayName` is nullable, including for an ADMIN without a customer profile.

### Session 5 security semantics

All auth responses have `Cache-Control: no-store`. All unsafe `/api` requests, including anonymous auth POSTs, require the `X-CSRF-TOKEN` header and paired antiforgery cookie. Missing/invalid evidence returns 400 `CSRF_VALIDATION_FAILED`; rate limits return 429 `RATE_LIMITED` and `Retry-After`. Auth failures use the standard Problem Details envelope. Registration can return 503 `IDENTITY_EMAIL_UNAVAILABLE`, rolling back the account for retry. Unexpected failures return safe 500 `INTERNAL_ERROR`.

The session cookie is nonpersistent with a fixed eight-hour encrypted ticket lifetime, no sliding renewal and no remember-me. Secure is required outside Development (localhost HTTP remains supported in Development). Logout revokes all sessions for the current user through the Identity security stamp. Reset revokes existing sessions too. Fetch fresh antiforgery state after login/logout/reset or session expiration. Registration does not sign the user in. Verification/reset tokens expire after one hour and cannot be reused after success. Unverified users can login and use `me`; future financial APIs require the verified-CUSTOMER policy (authenticated CUSTOMER plus email confirmation checked in the database). ADMIN does not implicitly grant CUSTOMER access.

See [DEVELOPMENT_AUTHENTICATION.md](../DEVELOPMENT_AUTHENTICATION.md) for local email pickup, production sender limitations, password/lockout/rate-limit settings, operational admin provisioning and API testing.

---

## 4. Customer Endpoints

Customer identity is derived from the authenticated session principal. Session 9 holdings, transaction history and transaction detail, and Session 10 savings-goal reads and creation require `AuthPolicies.VerifiedCustomer` (authenticated CUSTOMER plus current database email confirmation), consistent with FR-AUTH-004. ADMIN is not implicitly a CUSTOMER. Existing Session 7–8 dashboard, wallet summary and gold-price reads retain `AuthPolicies.Customer`; this historical exception is preserved without widening the new read endpoints. Financial mutations retain `AuthPolicies.VerifiedCustomer`.

### 4.1. `GET /api/v1/customer/profile`
- **Description**: Returns the authenticated customer's profile.
- **Responses**:
  - `200 OK`:
    ```json
    {
      "customerId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
      "email": "customer@example.com",
      "displayName": "Amara Silva",
      "isEmailVerified": true,
      "createdAtUtc": "2026-09-24T05:00:00.000Z"
    }
    ```

---

### 4.2. `PATCH /api/v1/customer/profile`
- **Description**: Updates editable profile attributes. In v1, **only `displayName` is editable** (OD-009).
- **Request Body**:
  ```json
  {
    "displayName": "Amara S."
  }
  ```
- **Responses**:
  - `200 OK`: Returns updated profile object.
  - `400 Bad Request`: `VALIDATION_ERROR` (displayName exceeds 100 characters).

---

### 4.3. `GET /api/v1/customer/dashboard`
- **Description**: Aggregate summary for the customer dashboard (wallet balance, gold holding, indicative valuation, active price, recent transactions, and goal progress, FR-HOLDING-002).
- **Responses**:
  - `200 OK`:
    ```json
    {
      "walletBalanceLkr": 45000.00,
      "goldHoldingGrams": 0.50000000,
      "indicativePortfolioValueLkr": 15000.00,
      "activePrice": {
        "priceVersionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
        "pricePerGramLkr": 30000.0000,
        "publishedAtUtc": "2026-09-24T06:00:00.000Z",
        "isFresh": true
      },
      "activeGoal": {
        "goalId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8c",
        "targetGrams": 1.00000000,
        "progressRatio": 0.5000,
        "progressPercentage": 50.0,
        "isCompleted": false
      },
      "recentTransactions": [
        {
          "transactionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8d",
          "type": "GOLD_PURCHASE",
          "amountLkr": 10000.00,
          "goldQuantityGrams": 0.33333333,
          "createdAtUtc": "2026-09-24T07:30:00.000Z"
        }
      ]
    }
    ```

---

Session 10 extends the existing dashboard response with `activeGoal`: null or the complete §4.11 goal DTO, including current holding, UTC target date and capped visual percentage. It uses the same persisted holding snapshot as the dashboard quantity. The existing dashboard read policy is preserved. Goals-page reads and creation require VerifiedCustomer.

### 4.4. `GET /api/v1/customer/wallet`
- **Description**: Returns wallet balance, caps, and remaining funding allowance for the current UTC day (FR-WALLET-001).
- **Responses**:
  - `200 OK`:
    ```json
    {
      "balanceLkr": 45000.00,
      "maxBalanceCapLkr": 5000000.00,
      "minFundingAmountLkr": 100.00,
      "maxFundingAmountLkr": 1000000.00,
      "successfulFundingCountToday": 2,
      "maxDailyFundingOperations": 20,
      "remainingFundingOperationsToday": 18
    }
    ```

---

### 4.5. `POST /api/v1/customer/wallet/fundings`
- **Description**: Deposits simulated LKR into the customer's wallet (OD-003, FR-WALLET-002).
- **Headers**: `Idempotency-Key: {uuid}` (Required).
- **Preconditions**: Customer email must be verified (`isEmailVerified == true`).
- **Request Body**:
  ```json
  {
    "amountLkr": 5000.00
  }
  ```
- **Responses**:
  - `201 Created` (or `200 OK` on idempotency replay):
    ```json
    {
      "transactionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8e",
      "type": "WALLET_FUNDING",
      "status": "COMPLETED",
      "amountLkr": 5000.00,
      "postOperationWalletBalanceLkr": 50000.00,
      "createdAtUtc": "2026-09-24T08:00:00.000Z"
    }
    ```
  - `400 Bad Request`: `VALIDATION_ERROR` (amount < 100.00 or > 1,000,000.00, scale > 2).
  - `403 Forbidden`: `EMAIL_NOT_VERIFIED`.
  - `409 Conflict`: `IDEMPOTENCY_CONFLICT` (same key used with different amount).
  - `422 Unprocessable Entity`: `WALLET_LIMIT_EXCEEDED` (balance would exceed 5M LKR) or `DAILY_FUNDING_LIMIT_EXCEEDED` (already 20 operations today).

---

### 4.6. `GET /api/v1/customer/gold-price`
- **Description**: Returns the active simulated gold price and freshness evaluation (OD-006, FR-GOLD-001).
- **Responses**:
  - `200 OK`:
    ```json
    {
      "priceVersionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
      "pricePerGramLkr": 30000.0000,
      "publishedAtUtc": "2026-09-24T06:00:00.000Z",
      "isFresh": true,
      "isPurchasingEnabled": true
    }
    ```
  - `503 Service Unavailable`: `PRICE_UNAVAILABLE` (no usable published price currently exists).

---

### 4.7. `POST /api/v1/customer/gold-purchases`
- **Description**: Saves simulated LKR into simulated gold (OD-004, OD-005, OD-006, FR-GOLD-002).
- **Headers**: `Idempotency-Key: {uuid}` (Required).
- **Preconditions**: Customer email must be verified.
- **Request Body**:
  ```json
  {
    "amountLkr": 100.00,
    "priceVersionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b"
  }
  ```
- **Responses**:
  - `201 Created` (or `200 OK` on idempotency replay):
    ```json
    {
      "transactionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8f",
      "type": "GOLD_PURCHASE",
      "status": "COMPLETED",
      "amountLkr": 100.00,
      "priceVersionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
      "appliedPricePerGramLkr": 30000.0000,
      "goldQuantityGrams": 0.00333333,
      "conversionResidualLkr": 0.000100000000,
      "postOperationWalletBalanceLkr": 49900.00,
      "postOperationGoldHoldingGrams": 0.50333333,
      "createdAtUtc": "2026-09-24T08:15:00.000Z"
    }
    ```
  - `400 Bad Request`: `VALIDATION_ERROR` (amount out of range, scale > 2).
  - `403 Forbidden`: `EMAIL_NOT_VERIFIED`.
  - `409 Conflict`: `PRICE_CHANGED` (submitted `priceVersionId` is no longer active; requires reconfirmation) or `IDEMPOTENCY_CONFLICT`.
  - `422 Unprocessable Entity`: `INSUFFICIENT_SIMULATED_FUNDS` or `GOLD_QUANTITY_ZERO`.
  - `503 Service Unavailable`: `PRICE_STALE` (active price is > 24 hours old) or `PRICE_UNAVAILABLE` (no usable price published).

---

### 4.8. `GET /api/v1/customer/holdings`
- **Description**: Returns the authenticated customer's persisted `GoldHolding.QuantityGrams` and current indicative simulation valuation (FR-HOLDING-001). Wallet balance remains in the existing `/customer/wallet` response; no wallet field is added here.
- **Session 9 reconciliation**: Latest persisted price by `PublishedAtUtc` supplies current context. Only a price no older than 24 hours is usable for valuation. `indicativePortfolioValueLkr` is `quantityGrams * activePricePerGramLkr`, rounded to two decimal places using decimal midpoint-to-even for presentation only. It is not cost basis, profit/loss or a receipt value. Missing price returns HTTP 200 with quantity preserved, both price fields and valuation `null`, and `isPriceFresh: false`. Stale price preserves price/version context but returns valuation `null` and `isPriceFresh: false`. Zero quantity with a fresh price returns zero valuation. No current projection or historical receipt is mutated.
- All Session 9 successful reads return `Cache-Control: no-store`; GET requires no CSRF header.
- **Responses**:
  - `200 OK`:
    ```json
    {
      "quantityGrams": 0.50333333,
      "indicativePortfolioValueLkr": 15099.99,
      "activePricePerGramLkr": 30000.0000,
      "priceVersionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
      "isPriceFresh": true
    }
    ```

---

### 4.9. `GET /api/v1/customer/transactions`
- **Description**: Paginated transaction history for the authenticated customer (FR-TRANSACTION-001), queried from persisted `FinancialTransaction` rows. Deterministic newest-first ordering is `createdAtUtc DESC, transactionId DESC` (UUID tie-break).
- **Session 9 reconciliation**: No filters or alternate sort modes are defined or implemented. `page` must be an integer >= 1; `pageSize` must be 1–100. Invalid or unparseable values return HTTP 400 `VALIDATION_ERROR` Problem Details. Beyond-last pages return an empty `items` array while retaining requested page and actual totals; empty history has `totalPages: 0`. Offset pages reflect current data, so concurrent new operations can shift later pages; this is not a snapshot/cursor contract.
- Each item exposes exactly `transactionId`, `type`, `status` (`COMPLETED`), `amountLkr`, nullable `goldQuantityGrams`, nullable `appliedPricePerGramLkr`, `postOperationWalletBalanceLkr`, and `createdAtUtc`. Nullable gold fields are `null` for funding. Status is added to the earlier example to satisfy FR-TRANSACTION-001. Dashboard `recentTransactions` uses the same item DTO/order with at most five rows. Existing dashboard balance fields remain unchanged.
- **Query Parameters**:
  - `page`: int (default `1`)
  - `pageSize`: int (default `20`, max `100`)
- **Responses**:
  - `200 OK`:
    ```json
    {
      "items": [
        {
          "transactionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8f",
          "type": "GOLD_PURCHASE",
          "status": "COMPLETED",
          "amountLkr": 100.00,
          "goldQuantityGrams": 0.00333333,
          "appliedPricePerGramLkr": 30000.0000,
          "postOperationWalletBalanceLkr": 49900.00,
          "createdAtUtc": "2026-09-24T08:15:00.000Z"
        }
      ],
      "page": 1,
      "pageSize": 20,
      "totalCount": 1,
      "totalPages": 1
    }
    ```

---

### 4.10. `GET /api/v1/customer/transactions/{id}`
- **Description**: Detailed immutable receipt for a single customer transaction (FR-TRANSACTION-002, FR-GOLD-003).
- **Responses**:
  - `200 OK`: Exactly the original funding receipt fields from §4.5 or purchase receipt fields from §4.7, discriminated by `type`; gold fields are omitted for funding. All fields come from immutable `FinancialTransaction` snapshots. Purchase residual is derived only from original amount, grams and applied price (up to 12 decimals), never current price or balances. No ledger rows, customer ID, internal account IDs or idempotency metadata are exposed.
  - `404 Not Found`: `RESOURCE_NOT_FOUND` with the same safe "Transaction not found." detail for nonexistent and foreign-owned transaction IDs. No existence leak.

---

### 4.11. `GET /api/v1/customer/savings-goal`
- **Description**: Retrieves the customer's active savings goal and calculated progress (OD-007, OD-008, FR-GOAL-002).
- **Responses**:
  - `200 OK`:
    ```json
    {
      "goalId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8c",
      "targetGrams": 1.00000000,
      "targetDateUtc": "2026-12-31T00:00:00.000Z",
      "currentHoldingGrams": 0.50333333,
      "progressRatio": 0.5033,
      "progressPercentage": 50.33,
      "visualProgressPercentage": 50.33,
      "isCompleted": false,
      "createdAtUtc": "2026-09-24T05:30:00.000Z"
    }
    ```
  - `200 OK` (with `null` goal if no active goal exists): `{"goal": null}`
  - `503 Service Unavailable`: `HOLDING_UNAVAILABLE`.
  - Both goal endpoints require verified CUSTOMER; anonymous/unverified/ADMIN requests receive existing 401/403 Problem Details. Creation requires the existing CSRF token.

---

### 4.12. `POST /api/v1/customer/savings-goal`
- **Description**: Creates one active savings goal (OD-008, FR-GOAL-001, US-GOAL-001). An existing active goal, including an achieved goal, returns `409 ACTIVE_GOAL_ALREADY_EXISTS`; no replacement, editing or deletion exists in v1 (MVP_SCOPE).
- **Request Body**:
  ```json
  {
    "targetGrams": 1.00000000,
    "targetDateUtc": "2026-12-31T00:00:00.000Z"
  }
  ```
  `targetGrams` must be positive, less than 1,000,000,000,000 and have at most 8 decimal places, preserving decimal(20,8). Excess scale is rejected, including excess trailing zero places. JSON decimal numbers and exact decimal strings are accepted for this field so browser clients can submit the full precision without floating-point conversion. Unsupported properties, including customer identity, are rejected.

  `targetDateUtc` is optional/null. When supplied, it must be a valid UTC timestamp ending in `Z`; no date-only value or timezone conversion is accepted by the API. The date is advisory: past dates are allowed, there is no future-date restriction in the approved requirements, and completion depends only on current holding. The browser date picker sends the chosen calendar date at UTC midnight and renders it in UTC.

  Completion is derived without changing `Status = ACTIVE`; an achieved goal remains visible and prevents another creation. Progress uses decimal current persisted holding / target, with actual percentage uncapped and visual percentage capped at 100. No financial mutation, reservation or financial idempotency record is created.
- **Responses**:
  - `201 Created`: Returns the newly active savings goal object.
  - `400 Bad Request`: `VALIDATION_ERROR` (invalid target precision/range, UTC timestamp or unsupported properties).
  - `409 Conflict`: `ACTIVE_GOAL_ALREADY_EXISTS` (including concurrent creation).
  - `503 Service Unavailable`: `HOLDING_UNAVAILABLE` (missing holding is not treated as zero).

---

## 5. Administrator Endpoints

All nine admin endpoints are implemented in Session 11 using the existing `AuthPolicies.Admin` policy (authenticated ADMIN). Anonymous receives 401 `AUTHENTICATION_REQUIRED`; CUSTOMER receives 403 `FORBIDDEN`. ADMIN remains excluded from CUSTOMER financial actions. All responses are `no-store`; price publication uses the shared antiforgery cookie and `X-CSRF-TOKEN`.

Session 11 resolves underspecified DTO/query details without adding routes:

- All admin collections use the §1 pagination envelope. `page` >= 1, `pageSize` 1–100; malformed inputs return 400 `VALIDATION_ERROR`. Empty history has `totalPages: 0`; beyond-last pages preserve totals and return empty items. Offset pagination is not a multi-request snapshot.
- Customers: each item is exactly `id`, `email`, `displayName`, `isEmailVerified`, `createdAtUtc`. Search is case-insensitive literal substring matching of Identity email or canonical customer UUID (not display name), maximum 256 characters. Ordering is `createdAtUtc DESC, id DESC`. Only persisted CustomerProfile identities are included; operational ADMIN identities are excluded.
- Customer detail adds only `walletBalanceLkr` and `goldHoldingGrams` to the summary. These are current persisted values; a missing projection is null/unavailable rather than invented zero. Unknown customer returns 404 `RESOURCE_NOT_FOUND`. Goals/recent activity are not additional detail fields; customer-specific transactions use the existing transaction filter.
- Admin transaction list/detail share `transactionId`, `customerId`, `type`, `status`, `amountLkr`, `priceVersionId`, `appliedPricePerGramLkr`, `goldQuantityGrams`, `conversionResidualLkr`, `postOperationWalletBalanceLkr`, `postOperationGoldHoldingGrams`, `createdAtUtc`. Nullable gold fields are null for funding. Values are immutable transaction snapshots; residual is derived from original amount/grams/price only. `type` accepts exactly WALLET_FUNDING/GOLD_PURCHASE; `customerId` must parse as UUID. No date/status filters. Ordering: `createdAtUtc DESC, transactionId DESC`. Unknown transaction returns 404 `RESOURCE_NOT_FOUND`.
- Ledger remains transaction-scoped §5.6; no global ledger route is added. Each posting also exposes persisted `accountName`, `unit`, and `createdAtUtc`. The two/four posting set orders by `createdAtUtc ASC, entryId ASC`, and balancing is calculated independently per unit using decimal. Funding has an empty balanced goldBook. Unknown transaction returns 404 `RESOURCE_NOT_FOUND`.
- Price/history/current selection all use `publishedAtUtc DESC, priceVersionId DESC`, including customer price, purchase execution and holdings valuation. This applies the UUID tie-break already approved in CONCURRENCY §2.4. `activeGoldPrice` is null before first publication; no fallback price.
- Publication accepts only `pricePerGramLkr` and `reason`. Positive price must fit numeric(18,4), strictly below 100,000,000,000,000; scale >4 (including excess trailing zeros) is rejected. Exact decimal JSON numbers or strings are accepted to preserve browser precision. Reason must be nonblank, at most 500 characters, and is trimmed. Actor, UUIDv7 version and UTC time are server-owned; unknown request fields are rejected. One PostgreSQL SaveChanges transaction inserts the new immutable version and correlated `PUBLISH_PRICE` AuditEvent. An audit failure rolls back both.
- Audit ordering: `timestampUtc DESC, id DESC`. `actorId` is an optional UUID; `action` is an optional exact string of at most 100 characters. `details` is structured safe metadata: PUBLISH_PRICE exposes only version, decimal price, reason and admin ID; ADMIN_PROVISIONED exposes only source. Unknown/malformed/non-object metadata is hidden as `{}`. Existing actions are not backfilled. Operational free-text reasons must not contain credentials or tokens.
- No update/delete, suspension, impersonation, reversal or manual balance/ledger operations exist. Immutability is enforced by application write paths and absent mutation APIs; privileged SQL remains outside these guarantees.

### 5.1. `GET /api/v1/admin/dashboard`
- **Description**: High-level operational metrics (FR-ADMIN-001).
- **Responses**:
  - `200 OK`:
    ```json
    {
      "totalCustomersCount": 142,
      "totalCompletedTransactionsCount": 1289,
      "activeGoldPrice": {
        "priceVersionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8b",
        "pricePerGramLkr": 30000.0000,
        "publishedAtUtc": "2026-09-24T06:00:00.000Z",
        "isFresh": true
      }
    }
    ```

---

### 5.2. `GET /api/v1/admin/customers`
- **Description**: Paginated customer list with search by email or customer ID (FR-ADMIN-002).
- **Query Parameters**: `page`, `pageSize`, `search` (optional substring).
- **Responses**:
  - `200 OK`: Paginated list of customer summary records (id, email, displayName, isEmailVerified, createdAtUtc).

---

### 5.3. `GET /api/v1/admin/customers/{id}`
- **Description**: Detailed customer profile, verified status, current wallet balance, and gold holding (FR-ADMIN-003). Does not expose credentials or password hashes.
- **Responses**:
  - `200 OK`: Detailed customer record.
  - `404 Not Found`: Customer does not exist.

---

### 5.4. `GET /api/v1/admin/transactions`
- **Description**: Paginated transaction list filterable by `customerId` or `type` (FR-ADMIN-004).
- **Query Parameters**: `page`, `pageSize`, `customerId` (optional), `type` (optional).
- **Responses**:
  - `200 OK`: Paginated list of all system transactions in newest-first order.

---

### 5.5. `GET /api/v1/admin/transactions/{id}`
- **Description**: Inspect a specific transaction's full details and immutable receipt fields (FR-ADMIN-004).
- **Responses**:
  - `200 OK`: Transaction details object.
  - `404 Not Found`: Transaction does not exist.

---

### 5.6. `GET /api/v1/admin/transactions/{id}/ledger`
- **Description**: Inspects double-entry ledger entries associated with a transaction (FR-ADMIN-004, NFR-DATA-005).
- **Responses**:
  - `200 OK`:
    ```json
    {
      "transactionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a8f",
      "lkrBook": {
        "isBalanced": true,
        "entries": [
          {
            "entryId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a90",
            "accountNumber": "2001-LKR-CUST-...",
            "direction": "DEBIT",
            "amount": 100.00
          },
          {
            "entryId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a91",
            "accountNumber": "2002-LKR-SYS-CLEARING",
            "direction": "CREDIT",
            "amount": 100.00
          }
        ]
      },
      "goldBook": {
        "isBalanced": true,
        "entries": [
          {
            "entryId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a92",
            "accountNumber": "1501-GOLD-SYS-ISSUANCE",
            "direction": "DEBIT",
            "amount": 0.00333333
          },
          {
            "entryId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a93",
            "accountNumber": "2501-GOLD-CUST-...",
            "direction": "CREDIT",
            "amount": 0.00333333
          }
        ]
      }
    }
    ```

---

### 5.7. `GET /api/v1/admin/prices`
- **Description**: Paginated history of published simulated gold price versions (FR-ADMIN-005).
- **Query Parameters**: `page`, `pageSize`.
- **Responses**:
  - `200 OK`: Paginated price history items with `priceVersionId`, `pricePerGramLkr`, `publishedAtUtc`, `publishedByAdminId`, and `reason`.

---

### 5.8. `POST /api/v1/admin/prices`
- **Description**: Publishes a new immutable simulated gold price version (OD-006, FR-ADMIN-006, US-ADMIN-006). Atomically creates an audit event.
- **Request Body**:
  ```json
  {
    "pricePerGramLkr": 31500.0000,
    "reason": "Market simulation update reflecting international gold tick"
  }
  ```
- **Responses**:
  - `201 Created`:
    ```json
    {
      "priceVersionId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a94",
      "pricePerGramLkr": 31500.0000,
      "publishedAtUtc": "2026-09-24T09:00:00.000Z",
      "publishedByAdminId": "018f3a5b-7c2a-7182-9a3b-8c4d5e6f7a95",
      "reason": "Market simulation update reflecting international gold tick"
    }
    ```
  - `400 Bad Request`: `VALIDATION_ERROR` (price <= 0, scale > 4, missing reason).

---

### 5.9. `GET /api/v1/admin/audit-events`
- **Description**: Paginated audit log review (FR-AUDIT-002).
- **Query Parameters**: `page`, `pageSize`, `actorId` (optional), `action` (optional).
- **Responses**:
  - `200 OK`: Paginated audit log records (`id`, `actorId`, `actorRole`, `action`, `entityType`, `entityId`, `details`, `timestampUtc`).
