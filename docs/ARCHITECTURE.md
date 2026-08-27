# Nidhi: Architecture

## 1. Architecture Overview
Nidhi will be implemented as a **Modular Monolith** rather than a microservices architecture. This avoids unnecessary enterprise complexity while keeping the codebase well-organized and scalable.

## 2. Monorepo Directory Structure
We will use a `pnpm` workspace to manage the monorepo. 

```text
nidhi/
├── apps/
│   ├── api/          # NestJS backend REST API
│   ├── mobile/       # Expo React Native mobile application
│   └── web/          # Next.js web application and admin dashboard
├── packages/
│   ├── db/           # Prisma schema, migrations, and generated client
│   ├── shared/       # Shared TypeScript types, DTOs, and utility functions
│   └── config/       # Shared ESLint, Prettier, and TSConfig settings
├── docs/             # Project documentation
├── docker-compose.yml # Local infrastructure (PostgreSQL)
└── package.json      # Workspace root
```

## 3. Backend Architecture (NestJS)
The backend in `apps/api` will use a modular design. Each domain (e.g., `Users`, `Wallet`, `Ledger`) will have its own module encapsulating controllers, services, and repositories.

### Key Principles
- **RESTful API**: Standard REST endpoints documented via OpenAPI/Swagger.
- **Service Layer**: Business logic lives strictly in services.
- **Idempotency**: Payment-like operations (simulated deposits, gold conversions) will require idempotency keys to prevent duplicate processing.
- **Immutability**: Historical financial records will never be silently modified or deleted. Reversing transactions will require compensating entries.

## 4. Frontend Architecture
- **Mobile (Expo)**: Built with Expo Router for file-based routing. Uses a modern, mobile-first UI framework (e.g., NativeWind or Restyle) tailored for a premium feel.
- **Web (Next.js)**: Uses App Router. Serves as a public landing page and a protected Admin Dashboard for viewing system metrics and audit logs.

## 5. Data Access
The `packages/db` package will house the Prisma schema. The generated Prisma Client will be exported and consumed by the NestJS backend.
