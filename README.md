# Nidhi

Nidhi is a digital gold micro-savings product. The first version will provide a public marketing website, authenticated customer application, and admin portal within one Next.js application, backed by ASP.NET Core.

Financial activity is initially simulated. This foundation accepts no real money and implements no financial operations.

## Current stack

- Frontend: Next.js, React, strict TypeScript, App Router, Tailwind CSS, ESLint.
- Backend: C#, .NET 10, ASP.NET Core controllers, REST and OpenAPI; a modular monolith.
- Tests: xUnit unit tests and ASP.NET Core integration tests.
- Planned: PostgreSQL with Entity Framework Core; Docker / Docker Compose; frontend tests, selected Playwright flows, and GitHub Actions later.

Database configuration, EF Core, authentication, product functionality, containers, and CI/CD are intentionally not part of this foundation.

## Structure

```text
frontend/web/                 Next.js app (minimal foundation page)
backend/
  Nidhi.sln
  src/
    Nidhi.Api/               HTTP controllers and composition
    Nidhi.Application/       Future application logic
    Nidhi.Domain/            Future domain logic; no project dependencies
    Nidhi.Infrastructure/    Future external services and persistence
  tests/
    Nidhi.UnitTests/
    Nidhi.IntegrationTests/
docs/                        Preserved product planning documents
AGENTS.md                    Current engineering rules
```

References: Application → Domain; Infrastructure → Application → Domain; Api → Application and Infrastructure. The inner projects are deliberately empty until requirements justify code. Business logic and authorization belong in ASP.NET Core, not Next.js routes or controllers.

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

No secrets or database are needed. `.env.example` documents optional API process environment settings. ASP.NET Core does not automatically load `.env` files; the launch profile supplies local defaults. There is no frontend/API data integration yet.

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

Integration tests use an in-memory ASP.NET Core test server without PostgreSQL. They verify health routing and OpenAPI exposure by environment.

## Historical documentation

The original PROJECT_SPEC, ARCHITECTURE, DATABASE_DESIGN, and ROADMAP documents are preserved unchanged. Their NestJS, Prisma, React Native, directory layout, and integer-money guidance describe the earlier plan and are not current implementation instructions. Follow this README and AGENTS.md for the current stack and decimal precision rules. See [foundation migration notes](docs/FOUNDATION_MIGRATION.md) for the inspection findings and client-source preservation status.
