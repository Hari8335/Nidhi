# Foundation migration

Historical record of Foundation Milestone 1. Validation and preservation statements below refer to that milestone. Session 2 revises the product documents; consult the [current product definition](PROJECT_SPEC.md) and [documentation audit](requirements/DOCUMENTATION_AUDIT.md) for current requirements.

## Original state

The pnpm workspace contained a NestJS API returning only `Hello World!`, its starter tests, two placeholder shared TypeScript packages, an Expo starter, and a custom Next.js landing-page prototype with a calculator. No backend Nidhi business logic, database, authentication, or financial persistence existed.

Documentation described Prisma, Expo Router, packages/db, packages/shared, packages/config, and Docker Compose that were not present. The mobile app used a basic App.tsx entry point. The web README was generated starter documentation. The old environment example included obsolete database/JWT placeholders, and both frontend and backend defaulted to port 3000.

`apps/web` and `apps/mobile` were Git links (mode 160000), without nested .git metadata or a .gitmodules file. Their source therefore was not ordinary parent-repository content. The foundation uses ordinary files under frontend/web; no repository was initialized and no Git history was rewritten.

## Preservation and removal

At the foundation milestone, all four original product planning documents were preserved unchanged. Session 2 subsequently revises their stack, roadmap and money representation, retaining useful reasoning and recording changes in the documentation audit. AGENTS.md governs engineering discipline.

A source archive was created during restructuring, but `docs/archive/original-clients.tar.gz` was already absent from the workspace at the final review. The old client commit objects (`bb1a34597840d1f25eae5f6dac5ba56136e20a63` for web and `1f059b50fcde502de31ccdcf60c1884d3308fb65` for mobile) are not present in the local repository. The historical Git links alone do not preserve the client source here; recovery requires an external backup or the original client repositories.

Removed the NestJS starter, its framework README/config/tests, placeholder types/validation packages, old client Git links, and obsolete workspace scripts. The new frontend is intentionally minimal. No product features were ported or added.

## Dependency rationale

The frontend retains Next.js, React, React DOM, TypeScript, their type definitions, Tailwind CSS with its PostCSS plugin, ESLint, and eslint-config-next. Lucide is unnecessary for the foundation and was removed. pnpm-lock.yaml records the resolved dependency graph.

The backend uses Microsoft.AspNetCore.OpenApi for REST documentation. Tests use Microsoft.NET.Test.Sdk, xunit, and xunit.runner.visualstudio for discovery/execution; Microsoft.AspNetCore.Mvc.Testing supplies the integration test host. No EF Core, database driver, authentication package, or architecture framework is introduced. Project files specify exact direct package versions.

## Tooling observations

Validation used Node.js 24.14.0, pnpm 9.0.0, and .NET SDK 10.0.401 / runtime 10.0.12. The shell initially lacked pnpm and dotnet, so temporary tools were installed outside the repository. Developers still need to install the prerequisites normally.

Next.js scripts explicitly use the supported Webpack bundler because Turbopack's CSS worker could not bind a local port in this execution environment, including after a permission retry. Webpack completes the same production compilation and TypeScript checks without disabling validation. The option can be revisited when the development environment supports Turbopack.

pnpm 9 emits a Node.js url.parse deprecation warning on Node 24; installation also reports ESLint 9 as deprecated. Existing frontend major versions were retained for this foundation migration.

## Validation results

- Backend restore: passed for all six projects.
- Backend build: passed, zero compiler warnings and errors.
- Backend tests: passed, one unit test and four integration cases, none skipped.
- Frontend pnpm install: passed; lockfile updated for frontend/web only.
- Frontend pnpm lint, pnpm typecheck, and pnpm build: passed, including the final pre-commit rerun.
- Frontend development server: ready at localhost:3000; GET / returned HTTP 200 with the expected title, heading, and foundation content. Stopped with Ctrl+C after verification. Visual browser verification was unavailable because Computer Use permission was not granted.
- Git diff whitespace check: passed. Original product documents were byte-for-byte unchanged at the foundation milestone.

The restricted shell required network permission for dependency downloads and socket permission for the .NET test runner. Backend validation used `-m:1` (single MSBuild worker), and build/test used `-p:UseSharedCompilation=false` to avoid compiler-server IPC restrictions. These flags do not disable compilation, analyzers, or tests. A sandbox-only CSSM_ModuleLoad diagnostic did not recur in the successful elevated test run.

## Review before committing

Review these files, as well as the deletions under apps/ and packages/:

- `AGENTS.md`, `.editorconfig`, `.gitignore`, `.env.example`, `README.md`
- `package.json`, `pnpm-workspace.yaml`, `pnpm-lock.yaml`
- `backend/Nidhi.sln`
- `backend/src/Nidhi.Api/Nidhi.Api.csproj`
- `backend/src/Nidhi.Application/Nidhi.Application.csproj`
- `backend/src/Nidhi.Domain/Nidhi.Domain.csproj`
- `backend/src/Nidhi.Infrastructure/Nidhi.Infrastructure.csproj`
- `backend/src/Nidhi.Api/Program.cs`
- `backend/src/Nidhi.Api/Controllers/HealthController.cs`
- `backend/src/Nidhi.Api/Properties/launchSettings.json`
- `backend/src/Nidhi.Api/appsettings.json`
- `backend/tests/Nidhi.UnitTests/Nidhi.UnitTests.csproj`
- `backend/tests/Nidhi.UnitTests/HealthControllerTests.cs`
- `backend/tests/Nidhi.IntegrationTests/Nidhi.IntegrationTests.csproj`
- `backend/tests/Nidhi.IntegrationTests/ApiTests.cs`
- `frontend/web/package.json`, `frontend/web/tsconfig.json`
- `frontend/web/next.config.ts`, `frontend/web/eslint.config.mjs`, `frontend/web/postcss.config.mjs`
- `frontend/web/src/app/page.tsx`, `frontend/web/src/app/layout.tsx`, `frontend/web/src/app/globals.css`
- `frontend/web/AGENTS.md`, `frontend/web/CLAUDE.md`, `frontend/web/README.md`
- `docs/FOUNDATION_MIGRATION.md`

Suggested commit: `chore: establish Next.js and ASP.NET Core foundation`
