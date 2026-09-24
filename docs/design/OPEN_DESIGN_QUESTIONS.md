# Technical Design Decisions & Questions — Session 3

This document logs the technical decisions and design questions addressed during the Session 3 domain, data, and API modeling exercise. It is distinct from [OPEN_DECISIONS.md](../requirements/OPEN_DECISIONS.md) (which tracks business and launch policy decisions).

---

## Approved Technical Decisions

### ODQ-001 — DECIDED: Conversion Residual Storage

- **Decision**: Conversion residual is **dynamically derived** rather than stored as a redundant database column.
- **Immutable Source Values**:
  1. Requested LKR amount (`amount_lkr`, `decimal(18,2)`)
  2. Credited gold quantity (`gold_quantity_grams`, `decimal(20,8)`)
  3. Applied price per gram (`applied_price_per_gram_lkr`, `decimal(18,4)`)
  4. Price version identifier (`price_version_id`, `uuid`)
- **Conceptual Calculation**:
  $$\text{conversionResidualLkr} = \text{amountLkr} - (\text{goldQuantityGrams} \times \text{appliedPricePerGramLkr})$$
- **Relational Schema Impact**: Do not add `conversion_residual_lkr` to the v1 relational schema.
- **Precision Guidance**: Reconciliation and audit calculations may require sub-cent decimal precision (up to 12 decimal places) even though customer-visible LKR amounts use two decimal places.
- **Future Reporting**: If future reporting requires indexed residual queries, a PostgreSQL generated column or dedicated reporting view can be introduced without altering the core domain model.

---

### ODQ-002 — DECIDED: EF Core Optimistic Concurrency Mechanism

- **Decision**: Use PostgreSQL's `xmin` system column as the EF Core optimistic concurrency token where optimistic concurrency is needed.
- **Implementation**: Model through the supported Npgsql / EF Core `IsRowVersion()` mapping (`builder.Property<uint>("xmin").IsRowVersion()`). Do not create a custom integer concurrency version column.
- **Financial Concurrency Context**: `xmin` does **not** replace financial transaction concurrency controls. Critical financial mutations continue to rely on:
  1. PostgreSQL database transactions (`BeginTransactionAsync`)
  2. Wallet row locking (`SELECT ... FOR UPDATE`)
  3. Relational database check constraints (nonnegative balance, caps)
  4. Scoped idempotency records (`idempotency_records`)
- **Role of `xmin`**: Provides optimistic concurrency protection for suitable mutable records and defense-in-depth where appropriate.

---

### ODQ-003 — DECIDED: Anti-Forgery (CSRF) Pattern for Cookie Authentication

- **Decision**: Use an **explicit ASP.NET Core antiforgery-token endpoint** (`GET /api/v1/auth/antiforgery`) rather than requiring the frontend to read a shared XSRF cookie.
- **Conceptual Flow**:
  1. The client obtains an antiforgery request token from ASP.NET Core via `GET /api/v1/auth/antiforgery`.
  2. ASP.NET Core issues and maintains the associated antiforgery cookie token (`HttpOnly; Secure; SameSite=Strict`).
  3. Next.js sends the antiforgery request token in the standardized request header:
     ```text
     X-CSRF-TOKEN: {token}
     ```
     for all unsafe state-changing HTTP requests (`POST`, `PUT`, `PATCH`, `DELETE`).
  4. ASP.NET Core Antiforgery middleware validates the token against the cookie token.
  5. The frontend obtains a fresh antiforgery request token after authentication identity changes (login, logout, session expiration).
- **Security Invariants**:
  - Authentication tokens and antiforgery tokens must **never** be stored in `localStorage` or `sessionStorage`.
  - Safe HTTP methods (`GET`, `HEAD`, `OPTIONS`) do not require CSRF token validation.
