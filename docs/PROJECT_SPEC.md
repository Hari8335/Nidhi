# Nidhi: Project Specification

## 1. Overview
Nidhi is a mobile-first educational fintech prototype focused on digital gold micro-savings, tailored for the Sri Lankan context. It allows users to simulate saving small amounts of Sri Lankan Rupees (LKR), converting them into fractional grams of gold, and tracking their savings progress.

> [!WARNING]
> **IMPORTANT DISCLAIMER**
> This project is an **educational portfolio prototype only**. It does NOT accept real money, purchase real gold, custody physical gold, provide real investment services, or act as a licensed financial service.

## 2. Core Objectives
- Demonstrate strong software engineering principles for an internship portfolio project.
- Implement a simulated digital gold micro-savings flow.
- Ensure strict financial engineering constraints (precision, auditability, idempotency).

## 3. Key Features
- **Micro-Savings Simulation**: Save LKR 100, 500, or 1,000.
- **Gold Conversion**: Convert LKR to fractional grams of gold using simulated market prices.
- **Holdings Tracking**: Monitor simulated gold holdings and LKR equivalent.
- **Savings Goals**: Set up custom target amounts and track progress.
- **Recurring Savings**: Simulate auto-deposit plans.
- **Transaction History**: View detailed logs of all conversions and simulated deposits.

## 4. Technical Stack
- **Language**: TypeScript (Strict mode across all environments)
- **Mobile App**: React Native with Expo & Expo Router
- **Web/Admin**: Next.js (App Router)
- **Backend API**: NestJS (REST API)
- **Database**: PostgreSQL
- **ORM**: Prisma ORM
- **Package Manager**: pnpm (Workspaces)
- **Infrastructure**: Docker (Local development environment)
- **Documentation**: OpenAPI / Swagger
- **CI/CD**: GitHub Actions (planned)

## 5. Modules
The backend will be designed as a **Modular Monolith**, containing the following domains:
- Auth & Users
- KYC Simulation
- Wallet (LKR balance tracking)
- Gold Pricing
- Gold Holdings
- Transactions & Ledger (Double-entry bookkeeping)
- Savings Goals
- Recurring Savings
- Notifications
- Admin & Audit Logs
