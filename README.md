# Nidhi

Nidhi is a mobile-first educational fintech prototype focused on digital gold micro-savings, tailored for the Sri Lankan context. It allows users to simulate saving small amounts of Sri Lankan Rupees (LKR), converting them into fractional grams of gold, and tracking their savings progress.

**IMPORTANT DISCLAIMER:**
This project is an **educational portfolio prototype only**. It does NOT accept real money, purchase real gold, custody physical gold, provide real investment services, or act as a licensed financial service.

## Technology Stack
- **Mobile**: React Native, Expo, Expo Router, TypeScript
- **Web/Admin**: Next.js (App Router), Tailwind CSS, TypeScript
- **Backend API**: NestJS, Swagger/OpenAPI, TypeScript
- **Workspace**: pnpm monorepo

## Project Structure
- `apps/mobile`: Expo mobile application
- `apps/web`: Next.js web application
- `apps/api`: NestJS backend API
- `packages/types`: Shared TypeScript types
- `packages/validation`: Shared validation logic
- `docs/`: Architectural documentation

## How to Run

1. Install dependencies:
   ```bash
   pnpm install
   ```

2. Start the API (NestJS):
   ```bash
   pnpm run dev:api
   ```

3. Start the Web App (Next.js):
   ```bash
   pnpm run dev:web
   ```

4. Start the Mobile App (Expo):
   ```bash
   pnpm run dev:mobile
   ```
