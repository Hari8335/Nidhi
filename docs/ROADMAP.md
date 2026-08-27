# Nidhi: 14-Day MVP Implementation Roadmap

## Phase 1: Foundation (Days 1-3)
- **Day 1**: Initialize pnpm workspace, configure ESLint/Prettier, set up `packages/config` and `packages/shared`.
- **Day 2**: Setup local Docker infrastructure (PostgreSQL). Initialize Prisma in `packages/db`, define the core schema, and generate migrations.
- **Day 3**: Scaffold `apps/api` with NestJS. Setup global exception filters, validation pipes, and OpenAPI (Swagger). Connect to Prisma.

## Phase 2: Core Domain & Auth (Days 4-6)
- **Day 4**: Implement Auth Module (JWT) and Users Module.
- **Day 5**: Implement KYC Simulation Module and Audit Logs Module.
- **Day 6**: Scaffold `apps/mobile` with Expo Router and `apps/web` with Next.js App Router. Implement basic login screens.

## Phase 3: Financial Engine (Days 7-9)
- **Day 7**: Implement Gold Pricing Module (simulated price fetching/generation).
- **Day 8**: Implement Wallet and Gold Holdings logic.
- **Day 9**: Implement the Transactions & Ledger modules. Ensure idempotency middleware is functioning.

## Phase 4: Savings Features & Mobile UI (Days 10-12)
- **Day 10**: Implement Savings Goals API.
- **Day 11**: Implement Recurring Savings API (Cron jobs).
- **Day 12**: Build out Mobile UI for Wallet, Gold Conversion, and Goals. Connect Mobile app to the API.

## Phase 5: Polish & Admin (Days 13-14)
- **Day 13**: Build Next.js Admin Dashboard to view system metrics, user balances, and audit logs.
- **Day 14**: End-to-end testing, error handling review, documentation updates, and final portfolio presentation prep.
