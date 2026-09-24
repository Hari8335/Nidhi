# Nidhi architecture

## Current foundation and target v1

Nidhi uses a modular monolith with pragmatic clean-architecture boundaries. The existing foundation contains a minimal frontend, a controller-based health endpoint, Development-only OpenAPI, and xUnit unit/integration projects. Session 4 adds domain entities, EF Core 10 / PostgreSQL 18 persistence, Identity storage, reviewed migrations, and local PostgreSQL Compose configuration. Authentication workflows, product flows and CI/CD remain future work defined by [requirements](requirements/MVP_SCOPE.md).

| Concern | Technology / responsibility |
|---|---|
| Web | One Next.js app, strict TypeScript, App Router, Tailwind CSS; public, Customer and Administrator experiences |
| API | C#, .NET 10, ASP.NET Core Web API controllers; REST and OpenAPI |
| Data | PostgreSQL 18 through Entity Framework Core 10 and reviewed migrations |
| Tests | xUnit backend tests; frontend tests and selected Playwright flows later |
| Delivery | Local PostgreSQL Docker Compose; GitHub Actions CI/CD, deployment and operational checks remain planned |

## Repository boundaries

```text
frontend/web/                       Next.js app; pnpm workspace member
backend/Nidhi.sln                   .NET solution; independent of pnpm
backend/src/Nidhi.Api/
backend/src/Nidhi.Application/
backend/src/Nidhi.Domain/
backend/src/Nidhi.Infrastructure/
backend/tests/Nidhi.UnitTests/
backend/tests/Nidhi.IntegrationTests/
docs/requirements/                 Product requirements and decisions
```

No apps/*, shared TypeScript backend packages or native mobile application are part of the active workspace. Existing Webpack frontend scripts and tooling details are documented in [foundation migration](FOUNDATION_MIGRATION.md).

## Project responsibilities and dependencies

| Project | Responsibility | Direct project references |
|---|---|---|
| Nidhi.Domain | Core domain concepts, invariants and business rules independent of HTTP/storage | None |
| Nidhi.Application | Use cases and orchestration of validation, business operations and required external capabilities | Nidhi.Domain |
| Nidhi.Infrastructure | EF Core/PostgreSQL implementation and other justified external concerns | Nidhi.Application (Domain reachable transitively) |
| Nidhi.Api | HTTP configuration, thin controllers, middleware, dependency registration, authentication and authorization configuration | Nidhi.Application, Nidhi.Infrastructure |

Infrastructure is wired at the API composition boundary; this reference is not permission to put business operations in controllers. Introduce interfaces only when a use case needs a boundary. No generic repository or custom Unit of Work wrapper is implied; select transaction handling during domain/data design.

Next.js renders experiences and calls the API. It must not implement a competing business backend using API routes, own persistence, or decide financial outcomes. Frontend visibility and route guards improve usability but never replace ASP.NET Core authentication, role and ownership checks. Web v1 uses ASP.NET Core Identity with Secure HttpOnly cookie authentication and no remember-me (OD-018). Browser JWT/localStorage authentication and token/mobile authentication are excluded. Verification and one-time password-reset emails are identity infrastructure; require CSRF/cookie/origin defenses and login/registration abuse controls in later security design. Identity persistence is installed, including its standard phone-number and two-factor columns. Nidhi v1 exposes no phone or 2FA workflow; cookie authentication and token flows are not yet implemented. Identity owns email; CustomerProfile does not duplicate it.

## Business and financial boundaries

Application use cases orchestrate [simulated funding](requirements/SIMULATED_WALLET_FLOW.md) and [gold-saving](requirements/GOLD_SAVING_FLOW.md). Domain rules validate approved amounts, eligibility, precision and conservation; Infrastructure supplies EF Core persistence and PostgreSQL transactions; future application orchestration must use them to enforce financial atomicity. Request idempotency, concurrency and receipt replay must survive restarts. Historical transactions, ledger entries and price versions are not silently edited.

Two roles only: CUSTOMER and ADMIN. Public registration cannot provision ADMIN. Administrative reads cover Customer records, transactions, ledger, prices and audits. Price publication is the only v1 administrative business mutation and must commit its audit evidence atomically. No suspension, manual correction, balance-edit or role-management UI is implied. ADMIN is provisioned through controlled operational bootstrap with protected configuration/secrets, no hard-coded/shared credential and practical audit evidence. Registered email-verified Customers may use customer features; no suspension/status workflow is modeled. Retention remains a launch decision, not a conceptual-design blocker. See the [decision log](requirements/OPEN_DECISIONS.md).

## Deliberate exclusions

No microservices, CQRS for appearance, automatic MediatR/AutoMapper adoption, generic repositories, custom Unit of Work abstractions, Redis, Kafka, event buses or message queues without a demonstrated approved requirement. Real payment and gold integrations, native mobile and recurring deductions are outside v1.

## Preserved reasoning

The earlier architecture's modular monolith, REST contract, service/use-case separation, idempotency and immutable financial history remain valuable. Its NestJS/Prisma implementation, mobile-first delivery and packages/db layout were superseded by the foundation. See the [documentation audit](requirements/DOCUMENTATION_AUDIT.md) for the change rationale; earlier text remains in Git history.
