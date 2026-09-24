# Nidhi

Nidhi is a digital gold micro-savings product. The first version will provide a public marketing website, authenticated customer application, and admin portal within one Next.js application, backed by ASP.NET Core.

Financial activity is initially simulated. This foundation accepts no real money and implements no financial operations.

## Current stack

- Frontend: Next.js, React, strict TypeScript, App Router, Tailwind CSS, ESLint.
- Backend: C#, .NET 10, ASP.NET Core controllers, REST and OpenAPI; a modular monolith.
- Tests: xUnit unit tests and ASP.NET Core integration tests.
- Persistence: PostgreSQL 18 in Docker Compose, EF Core 10, Npgsql 10, and ASP.NET Core Identity storage.
- Planned: frontend tests, selected Playwright flows, and GitHub Actions.

Persistence storage is implemented. Authentication flows, product use cases, and CI/CD remain future work.

## Structure

```text
frontend/web/                 Next.js app (minimal foundation page)
backend/
  Nidhi.sln
  src/
    Nidhi.Api/               HTTP controllers and composition
    Nidhi.Application/       Future application logic
    Nidhi.Domain/            Domain entities; no project dependencies
    Nidhi.Infrastructure/    Identity and EF Core persistence
  tests/
    Nidhi.UnitTests/
    Nidhi.IntegrationTests/
docs/                        Product, architecture and requirements documents
AGENTS.md                    Current engineering rules
```

References: Application → Domain; Infrastructure → Application → Domain; Api → Application and Infrastructure. Application use cases remain unimplemented. Business logic and authorization belong in ASP.NET Core, not Next.js routes or controllers.

## Prerequisites

Install the .NET 10 SDK, Node.js 22 or newer, and pnpm 9.0.0 (pinned in package.json). If Corepack is available, `corepack enable` enables the pnpm command; alternatively use `corepack pnpm` in place of `pnpm` below.

## Run locally

From the repository root:

```sh
pnpm install
pnpm dev:web
```

The frontend runs at http://localhost:3000. Scripts explicitly use the supported Webpack bundler; see the migration notes for the environment limitation behind this choice. In a second terminal:

```sh
dotnet run --project backend/src/Nidhi.Api
```

The development launch profile serves http://localhost:5050:

- `GET /health` returns `{"status":"ok"}` (process liveness only).
- `GET /openapi/v1.json` exposes the API description in Development only.

Health and OpenAPI require no database. For persistence, follow the [local database setup](docs/DEVELOPMENT_DATABASE.md). ASP.NET Core does not automatically load `.env`. There is no frontend/API data integration yet.

## Validate

```sh
cd backend
dotnet restore
dotnet build
dotnet test
cd ..
pnpm install
pnpm lint
pnpm typecheck
pnpm build
```

HTTP integration tests use an ASP.NET Core test server without PostgreSQL. Model tests inspect Npgsql metadata. The separately configured PostgreSQL integration test uses a disposable real database; see [database validation](docs/DEVELOPMENT_DATABASE.md).

## Product requirements

The technical foundation is implemented; the v1 product capabilities are planned. Start with the [product definition](docs/PROJECT_SPEC.md), [MVP scope](docs/requirements/MVP_SCOPE.md), [functional requirements](docs/requirements/FUNCTIONAL_REQUIREMENTS.md), [non-functional requirements](docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md), and [user stories](docs/requirements/USER_STORIES.md).

The [gold-saving flow](docs/requirements/GOLD_SAVING_FLOW.md) and [simulated wallet flow](docs/requirements/SIMULATED_WALLET_FLOW.md) define the financial simulation. The [decision log](docs/requirements/OPEN_DECISIONS.md) records approved product rules. Only OD-013–015 remain open for launch/deployment policy; they do not block conceptual domain/API design.

See [architecture](docs/ARCHITECTURE.md), [conceptual data design](docs/DATABASE_DESIGN.md), and [roadmap](docs/ROADMAP.md) for the design sequence. The [documentation audit](docs/requirements/DOCUMENTATION_AUDIT.md) explains which earlier assumptions were superseded. [Foundation migration notes](docs/FOUNDATION_MIGRATION.md) preserve the historical inspection/validation results and client-source preservation limitation. AGENTS.md remains the engineering authority.
