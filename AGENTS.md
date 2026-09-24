# Nidhi engineering instructions

These rules apply throughout the repository. Historical material in docs/ does not override them.

## Product
Nidhi is a digital gold micro-savings product. V1 will contain a public marketing website, customer application, admin portal, ASP.NET Core API, and PostgreSQL database. All three web experiences initially share one Next.js application.
Initially, financial activity must be simulated. Do not add real-money payment integrations unless explicitly requested.

## Architecture
Use a modular monolith with Nidhi.Api, Nidhi.Application, Nidhi.Domain, and Nidhi.Infrastructure. Do not introduce microservices.
Project references: Application → Domain; Infrastructure → Application → Domain; Api → Application and Infrastructure. Domain must not depend on any other project, especially Infrastructure.
Business logic must not live inside controllers. Avoid unnecessary enterprise abstractions and tutorial-driven patterns.
Do not introduce CQRS, MediatR, AutoMapper, generic repositories, Unit of Work abstractions, Redis, Kafka, or message queues unless explicitly requested.

## Financial engineering
Never use floating-point types such as float or double for money or gold quantities. Use decimal types with explicit database precision.
Operations affecting multiple financial records must be transactional when implemented. Transaction and ledger history must not be silently changed; corrections require explicit auditable records.

## Frontend
Use Next.js App Router, Tailwind CSS, and strict TypeScript.
Do not create a second backend using Next.js API routes for Nidhi business logic.
Business rules, authentication, authorization, persistence, transactions, and financial operations belong in ASP.NET Core.

## Security
Never commit secrets. Use environment variables.
Authorization must be enforced by ASP.NET Core. Hiding an admin button in the frontend is not authorization.

## Development discipline
Before implementing a feature:
1. Understand requirements.
2. Identify affected components.
3. Implement the smallest appropriate solution.
4. Add relevant tests.
5. Run validation.
6. Do not modify unrelated code.

Do not add dependencies without explaining why they are required.
Run backend restore, build, and tests from backend/; run pnpm install, lint, and build from the repository root.
Do not automatically make Git commits.
