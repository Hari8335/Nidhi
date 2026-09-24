# Local persistence development

Install Docker with Compose and the .NET 10 SDK using your preferred local setup. Run these commands from the repository root. PostgreSQL runs in Docker; the API and frontend run as local processes.

## Configure and start PostgreSQL

```sh
cp .env.example .env
# Edit .env: replace the password placeholder with a local-only password.
docker compose up -d
docker compose ps
docker compose exec postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

`.env` is ignored. Compose requires explicit database/user/password values, binds the port only to localhost, and persists PostgreSQL 18 data in a named volume mounted at `/var/lib/postgresql`. PostgreSQL 18 uses a version-specific data directory under that mount. Do not use production credentials or data here.

ASP.NET Core does not load `.env`. In the terminal running the API and EF tools, set the connection string without including the secret in shell history (zsh):

```sh
read -rs 'ConnectionStrings__NidhiDb?Enter local PostgreSQL connection string: '
printf '\n'
export ConnectionStrings__NidhiDb
```

Enter a connection string in this shape, replacing all placeholders:
`Host=localhost;Port=5432;Database=<local database>;Username=<local user>;Password=<local password>`.
Use the port and credentials configured in your `.env`. Do not paste actual credentials into tracked files, issue descriptions, or logs.

## Tools and migrations

```sh
dotnet tool restore
cd backend
dotnet restore Nidhi.sln
dotnet build Nidhi.sln
cd ..
dotnet ef database update --project backend/src/Nidhi.Infrastructure --startup-project backend/src/Nidhi.Api
dotnet ef migrations list --project backend/src/Nidhi.Infrastructure --startup-project backend/src/Nidhi.Api
```

The initial migration is `InitialDataFoundation`. For a later approved model change:

```sh
dotnet ef migrations add DescriptiveChangeName --project backend/src/Nidhi.Infrastructure --startup-project backend/src/Nidhi.Api --output-dir Persistence/Migrations
```

Always review the generated migration before applying it. The API never automatically applies migrations.

```sh
dotnet run --project backend/src/Nidhi.Api
curl --fail http://localhost:5050/health
```

`/health` remains process liveness and works without database configuration. DbContext resolution requires `ConnectionStrings:NidhiDb`; configuration is checked only when persistence is used.

## Real PostgreSQL validation

Use only a local development role with `CREATEDB` privileges. The test creates a random `nidhi_test_*` database, migrates it, verifies financial constraints and `xmin`, drops/recreates it, repeats validation, and finally drops only that test database. It does not drop the database named in your connection string.

```sh
export NIDHI_TEST_POSTGRES="$ConnectionStrings__NidhiDb"
cd backend
dotnet test Nidhi.sln
cd ..
unset NIDHI_TEST_POSTGRES
```

Without this variable the PostgreSQL test is explicitly skipped. Passing metadata or HTTP tests is not evidence that PostgreSQL constraints work.

Inspect the development database after migration:

```sh
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "\\dt"'
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "\\d wallets"'
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "\\d savings_goals"'
docker compose stop
```

`docker compose down` removes the container/network while retaining the named volume. Do not remove the persistent volume unless its contents are disposable.

## Design reconciliation and limits

- Session 4 explicitly delegates email and authentication fields to Identity. `CustomerProfile` therefore has no email field, as reflected in the reconciled ERD. The Identity normalized-email index is unique, satisfying the approved email uniqueness requirement.
- `TransactionStatus` contains only `COMPLETED`, following the detailed ERD and transaction model. The reconciled domain overview agrees; failed command attempts may be logged separately but are not financial history rows.
- Purchase consistency uses explicit `IS NOT NULL` checks for required nullable numeric fields. PostgreSQL CHECK rejects only FALSE and accepts UNKNOWN/NULL; the reconciled ERD includes these guards to prevent incomplete purchases.
- Ledger entry/account unit equality uses a composite foreign key `(account_id, unit)` to the account's alternate key `(id, unit)`. This enforces the ledger document's approved invariant; the reconciled ERD enumerates both supporting key and composite FK.
- `audit_log_id` is a correlation identifier as described in the detailed ERD. Audit actors may represent a system actor, so `actor_id` is not forced to reference an Identity user.
- Wallet and gold-holding `uint` versions use Npgsql `.IsRowVersion()` and PostgreSQL system `xmin`. The generated C# migration retains `xmin` row-version metadata; Npgsql suppresses that system column from the emitted `CREATE TABLE` SQL. No user-defined version column is created. This does not implement or replace financial row locking or transactional orchestration.
- Public constructors plus private setters support materialization and append-oriented history. No general mutation services exist. PostgreSQL does not yet prohibit privileged SQL updates to historical records. Idempotency completion/association APIs are deferred to the transactional use case; EF can persist those changes in that future transaction.
- Ledger balancing, LKR-entry cent granularity within the shared `numeric(20,8)` column, price freshness, daily limits, successful idempotency lifecycle, and cross-record snapshot consistency remain application responsibilities. No speculative triggers, accounts, roles, or credentials are seeded.

Dependencies: EF Core, EF Design, and Identity EF storage 10.0.12; Npgsql EF provider 10.0.3; repository-local `dotnet-ef` 10.0.12. Domain has no persistence dependencies.
