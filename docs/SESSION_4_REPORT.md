# Session 4 persistence implementation report

Status: implementation and validation complete. After Docker became available, a private ignored `.env` was created and PostgreSQL 18.6 started successfully. The existing service on port 5432 was preserved; Nidhi's local `.env` uses port **5433**. The initial migration applied to the clean development database, and the real PostgreSQL round-trip/constraint/concurrency test passed.

Branch: `feat/data-foundation`. No commit was created.

## Implementation

Ten domain entities: CustomerProfile, Wallet, GoldHolding, GoldPrice, FinancialTransaction, LedgerAccount, LedgerEntry, SavingsGoal, IdempotencyRecord, AuditEvent. Application-generated IDs use UUIDv7; CustomerProfile shares its Identity user ID. Domain is independent of Identity/EF. Constructors and private setters restrict history mutation; there are no use-case services or endpoints.

Infrastructure contains `ApplicationUser : IdentityUser<Guid>` and `NidhiDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`. Ten domain configurations and seven Identity configurations are applied via `ApplyConfigurationsFromAssembly`. Identity retains credential/authentication properties; CustomerProfile does not duplicate email. No roles, administrator credentials, or ledger accounts are seeded.

The API calls `AddInfrastructure(configuration)`. Context configuration uses `ConnectionStrings:NidhiDb` lazily. Startup does not migrate or contact PostgreSQL, and `/health` remains database independent. No authentication handlers, cookies, auth endpoints, financial orchestration, repositories, or frontend changes were introduced.

| Dependency | Exact version | Purpose |
|---|---|---|
| Microsoft.EntityFrameworkCore | 10.0.12 | Infrastructure persistence/model mapping |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 | Guid-key Identity storage |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL provider |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | Private API startup tooling dependency |
| Repository-local dotnet-ef | 10.0.12 | Migration commands |

Compose uses `postgres:18`, environment-driven database/user/password, localhost port 5432 (configurable), named `postgres_data` volume at `/var/lib/postgresql`, and `pg_isready` health checks. `.env.example` contains development placeholders only. Exact setup, environment-variable configuration, migration commands, inspection, testing, and shutdown commands are in [DEVELOPMENT_DATABASE.md](DEVELOPMENT_DATABASE.md).

## Mappings and constraints

Explicit table/column mappings preserve approved names, types, nullability, lengths, precision, keys, and indexes. LKR uses numeric(18,2), prices numeric(18,4), gold numeric(20,8); ledger's shared amount column uses numeric(20,8) as specified by the ERD. No residual is stored.

Implemented checks cover wallet nonnegativity/cap, holding nonnegativity, positive prices, transaction types/completed status/amount range/purchase-field consistency, ledger units/classifications/directions/positive amounts, goal positivity/status, and idempotency status. Uniqueness covers wallet/holding ownership, transaction idempotency, scoped idempotency keys, account numbers, normalized Identity email/username/role name, and the partial one-active-goal index. History, price, audit, account, and relationship indexes are present. Domain relationships use restrictive deletes.

Wallet and GoldHolding map uint versions through `.IsRowVersion()` to system `xmin`. C# migration metadata includes `xmin` with `rowVersion: true`; the reviewed Npgsql SQL does **not** create a column for it. Executed metadata/SQL tests verify this. Actual Wallet and GoldHolding optimistic-concurrency conflicts were verified against PostgreSQL 18.6.

Migration: `20260924103810_InitialDataFoundation` (Infrastructure, API startup). Generated migration, snapshot, and SQL were inspected. No pending model changes remain.

Tables defined by the migration:

- Identity: `asp_net_users`, `asp_net_roles`, `asp_net_user_roles`, `asp_net_user_claims`, `asp_net_user_logins`, `asp_net_user_tokens`, `asp_net_role_claims`.
- Domain: `customer_profiles`, `wallets`, `gold_holdings`, `gold_prices`, `financial_transactions`, `ledger_accounts`, `ledger_entries`, `savings_goals`, `idempotency_records`, `audit_events`.
- EF also maintains `__EFMigrationsHistory`.

## Validation results

| Check | Result |
|---|---|
| Local dotnet tool restore | Passed |
| Backend restore | Passed |
| Backend build | Passed, 0 warnings / 0 errors |
| Backend tests | 10 passed, 0 skipped, 0 failed |
| Migration generation and SQL review | Passed |
| EF pending-model check | No changes since migration |
| PostgreSQL Compose start/health | Passed; container healthy on localhost:5433 |
| Clean database migration application | Passed on clean nidhi_dev |
| Applied migration history/status | InitialDataFoundation confirmed applied |
| Drop/recreate migration round-trip | Passed; two clean applications in disposable database |
| Live PostgreSQL constraints and xmin | Passed against PostgreSQL 18.6, including both concurrency tokens |
| API startup and GET /health | HTTP 200, `{"status":"ok"}`, no database configuration |
| Frontend lint / typecheck / build | All passed |
| git diff --check | Passed |
| Frontend diff | Empty |
| Secrets review | No actual secrets added; .env ignored; only .env.example tracked |

The health smoke test used localhost:5052 because an existing API process occupied 5050. The task's temporary API was stopped; the pre-existing instance was preserved. Build used one MSBuild worker and disabled shared compilation for local sandbox compatibility. Test/EF host IPC required execution outside the sandbox. Packages/tools were restored to temporary caches; no global tool installation was performed.

New tests in `PersistenceModelTests.cs` cover precision/no residual, both xmin mappings and generated SQL, idempotency scope, partial goal index, ledger/account unit relationship, restrictive domain deletes, and Identity/profile separation. `PostgresPersistenceTests.cs` owns a random disposable database and is designed to apply migrations twice across a drop/recreate cycle, check migration history/table count, exercise valid funding/purchase storage, reject invalid constraints/duplicates, permit inactive goals and separate idempotency operations, and verify actual Wallet/GoldHolding concurrency conflicts. It refuses a PostgreSQL major version other than 18. The test passed with PostgreSQL configured. Direct schema inspection confirmed 18 tables including EF history, both `xmin` attributes as system columns (`attnum = -2`, type `xid`), the scoped idempotency and partial active-goal indexes, no residual columns, and no leftover test databases.

## Reconciled design clarifications

1. The reconciled ERD and domain/database design assign email only to Identity. Normalized Identity email is unique.
2. Completed-only financial history follows the ERD/transaction model; the reconciled domain overview also permits only COMPLETED financial transactions; failed attempts are observed separately.
3. Explicit null guards are required in purchase CHECK expressions: PostgreSQL otherwise accepts an unknown/null result.
4. The ledger unit invariant requires a supporting `(id, unit)` alternate key and `(account_id, unit)` foreign key now enumerated in the reconciled ERD.
5. `audit_log_id` remains a correlation ID; system-capable audit actors have no mandatory user FK, following the detailed table definitions.
6. Standard Identity includes its built-in phone/2FA fields; no phone/2FA product flow was introduced. Domain identifiers are UUIDs; standard Identity claim-row IDs remain integer identity columns.
7. Database constraints do not yet guarantee append-only privileged SQL, ledger balancing, cent granularity for LKR entries in the shared ledger column, or cross-record business consistency. These limits and later transactional responsibilities are documented in the development guide.

## Files created and review inventory

- `.config/dotnet-tools.json`
- `backend/src/Nidhi.Domain/Entities/AuditEvent.cs`
- `backend/src/Nidhi.Domain/Entities/ControlledValues.cs`
- `backend/src/Nidhi.Domain/Entities/CustomerProfile.cs`
- `backend/src/Nidhi.Domain/Entities/FinancialTransaction.cs`
- `backend/src/Nidhi.Domain/Entities/GoldHolding.cs`
- `backend/src/Nidhi.Domain/Entities/GoldPrice.cs`
- `backend/src/Nidhi.Domain/Entities/IdempotencyRecord.cs`
- `backend/src/Nidhi.Domain/Entities/LedgerAccount.cs`
- `backend/src/Nidhi.Domain/Entities/LedgerEntry.cs`
- `backend/src/Nidhi.Domain/Entities/SavingsGoal.cs`
- `backend/src/Nidhi.Domain/Entities/Wallet.cs`
- `backend/src/Nidhi.Infrastructure/DependencyInjection.cs`
- `backend/src/Nidhi.Infrastructure/Identity/ApplicationUser.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/CustomerProfileConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/FinancialTransactionConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/GoldHoldingConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/GoldPriceConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/IdempotencyRecordConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/IdentityRoleClaimConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/IdentityRoleConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/IdentityUserClaimConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/IdentityUserLoginConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/IdentityUserRoleConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/IdentityUserTokenConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/LedgerAccountConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/LedgerEntryConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/SavingsGoalConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Configurations/WalletConfiguration.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Migrations/20260924103810_InitialDataFoundation.Designer.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Migrations/20260924103810_InitialDataFoundation.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/Migrations/NidhiDbContextModelSnapshot.cs`
- `backend/src/Nidhi.Infrastructure/Persistence/NidhiDbContext.cs`
- `backend/tests/Nidhi.IntegrationTests/Persistence/PersistenceModelTests.cs`
- `backend/tests/Nidhi.IntegrationTests/Persistence/PostgresPersistenceTests.cs`
- `compose.yaml`
- `docs/DEVELOPMENT_DATABASE.md`
- `docs/SESSION_4_REPORT.md`

## Files modified

- `.env.example`
- `README.md`
- `backend/src/Nidhi.Api/Nidhi.Api.csproj`
- `backend/src/Nidhi.Api/Program.cs`
- `backend/src/Nidhi.Infrastructure/Nidhi.Infrastructure.csproj`

Review the migration and snapshot, FinancialTransaction/Wallet/GoldHolding/SavingsGoal/IdempotencyRecord/LedgerEntry configurations, domain constructors, DbContext/registration, both persistence test files, Compose, and the development guide before committing. The inventory above gives exact paths for every changed file.

## Completion and suggested commit

PostgreSQL remains running locally on port 5433. The ignored `.env` has owner-only permissions and a generated local password; no actual credentials appear in tracked files or this report. Compose interpolation was validated with `docker compose config --quiet` to avoid displaying the resolved password. The API still needs its connection-string environment variable set when persistence is used; see the development guide. No authentication or business-use-case implementation was started, and no Git commit was made.

Suggested Conventional Commit: `feat: establish PostgreSQL and EF Core data foundation`.
