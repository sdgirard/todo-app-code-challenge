# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

To-do list application built for the Foci Solutions take-home coding challenge. See [`docs/requirements/requirements.md`](docs/requirements/requirements.md) for the full assignment.

Stack: ASP.NET Core Minimal API backend (.NET 10), React (Vite) + React Router frontend. Backend lives under `backend/` (`TodoApi.Gateway` host + `TodoApi.Todos` feature library, plus test projects) — all six feature endpoints implemented (`POST /todos`, `GET /todos`, `GET /todos/{id}`, `PUT /todos/{id}`, `PATCH /todos/{id}`, `DELETE /todos/{id}`). Frontend lives under `frontend/` (Vite + React 19 + React Router + Tailwind CSS v4 + Vitest/RTL/MSW) and is feature-complete — two routes (`/` and `/todos/:id`) cover all seven requirement-level operations (Add, List, View, Update, Complete, Incomplete, Delete), backed by an orval-generated TypeScript client (`src/api/generated/`, regenerated automatically via `predev`/`prebuild`).

## Backend Build/Test Commands

- Build: `dotnet build` from `backend/`
- Run: `dotnet run --project backend/src/TodoApi.Gateway/`
- Test: `dotnet test TodoApi.slnx` from `backend/` (plain `dotnet test` with no target can silently pick up only one test project — always target the solution explicitly)
- Test with coverage: `dotnet test TodoApi.slnx --collect:"XPlat Code Coverage" --settings tests.runsettings --results-directory ./TestResults` from `backend/` — `tests.runsettings` excludes generated code (e.g. EF Core migrations under `TodoApi.Todos/Persistence/Migrations/`) from coverage. Generate the HTML report from the results with `reportgenerator -reports:"TestResults/*/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html` (install once with `dotnet tool install -g dotnet-reportgenerator-globaltool`). Both `TestResults/` and `coverage-report/` are gitignored, regenerated output.

All projects build with `TreatWarningsAsErrors` (set in `backend/Directory.Build.props`) — see [`docs/standards/aspnet-web-api-guidelines.md`](docs/standards/aspnet-web-api-guidelines.md#warnings-as-errors).

## Frontend Build/Test Commands

- Install: `npm install` from `frontend/`
- Dev server: `npm run dev` from `frontend/` (Vite)
- Build: `npm run build` from `frontend/` (`tsc -b && vite build`)
- Lint: `npm run lint` from `frontend/` (oxlint)
- Test: `npm run test` from `frontend/` (Vitest)

Two routes (`TodoListRoute`, `TodoDetailRoute`) cover all seven requirement-level operations; the orval pre-build generation step described in [Architecture Notes](#architecture-notes) below is wired in via `predev`/`prebuild` npm scripts.

## Keep README.md in Sync

[`README.md`](README.md) is the reviewer-facing entry point (Foci's technical team reads it first) — distinct from this file, which is agent-facing. It summarizes design choices, assumptions, and trade-offs, and links into `docs/` for full depth.

**Update `README.md` whenever a change here would make it stale:**

- A new architectural decision is made or an existing one changes (update the relevant bullet under "Design Choices").
- A new doc is added under `docs/` (add it to the "Documentation Map").
- An assumption is made, changed, or resolved by Foci's answer to a question in `requirements-qa.md` (update "Assumptions").
- Build, run, or test instructions become real (replace the "TODO — not yet available" placeholders once the backend/frontend projects exist and those commands actually work).
- The stack itself changes.

Keep the README's summaries brief — it links to the authoritative doc for detail, it doesn't duplicate it. If a design-choice bullet and its source doc drift apart, the doc under `docs/` is the source of truth; fix the README to match it, not the other way around.

## Docs

Read these before making architectural decisions or writing backend code — they contain real, considered decisions, not just notes:

- [`docs/requirements/requirements.md`](docs/requirements/requirements.md) — source requirements from Foci, plus decisions made and open questions
- [`docs/requirements/requirements-qa.md`](docs/requirements/requirements-qa.md) — trimmed list of questions sent to Foci (public version)
- [`docs/requirements/requirements-qa-internal.md`](docs/requirements/requirements-qa-internal.md) — full internal brainstorm of ambiguities; not sent to Foci, most are treated as documented assumptions instead
- [`docs/architecture/overview-architecture.md`](docs/architecture/overview-architecture.md) — system-level architecture, component diagram, OpenAPI spec → TypeScript client generation pipeline (orval)
- [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md) — backend design: directory structure (Gateway + feature-library modular monolith), Minimal API endpoint-per-class pattern, DTO/Model boundary, hand-written DTO⇄Model mapping, n-tier layering, bootstrapping (`ConfigureServices`/`ConfigureApps`)
- [`docs/architecture/backend/cqrs.md`](docs/architecture/backend/cqrs.md) — CQRS without a mediator library (MediatR ruled out, same licensing issue as AutoMapper); DI + a named interface per handler (`IAddTodoCommandHandler : ICommandHandler<TCommand, TResult>`) instead
- [`docs/architecture/frontend/overview.md`](docs/architecture/frontend/overview.md) — frontend design: React Router data APIs (loaders/actions) as the data layer, no state-management library, two routes covering all seven requirement-level operations, Tailwind CSS, generated-client integration, Vitest + RTL + MSW testing
- [`docs/architecture/authentication.md`](docs/architecture/authentication.md) — rough notes on optional multi-user/auth (Clerk vs. self-hosted Authentik); phase 1 is single-user, no auth
- [`docs/standards/aspnet-web-api-guidelines.md`](docs/standards/aspnet-web-api-guidelines.md) — backend coding standards (DI, single responsibility, cyclomatic complexity, minimal `else`, .NET conventions)
- [`docs/infra/harbor-registry-setup.md`](docs/infra/harbor-registry-setup.md) — Harbor container registry setup, if/when containerization happens (optional enhancement, not core scope)
- [`docs/infra/dockerfile-organization.md`](docs/infra/dockerfile-organization.md) — `docker/` layout, per-service image naming, path-filtered CI matrix build (backend vs. frontend only rebuild/publish their own image)
- [`docs/infra/container-image.md`](docs/infra/container-image.md) — backend (`todo-app-gateway`) container image design
- [`docs/infra/container-image-frontend.md`](docs/infra/container-image-frontend.md) — frontend (`todo-app-web`) container image design: Node build + nginx runtime, nginx-terminated TLS, SPA fallback, `version.json`
- [`docs/infra/deployment.md`](docs/infra/deployment.md) — deployment target (home lab Kubernetes) and TLS strategy (dev certs locally, Let's Encrypt for the demo); cluster/ingress details still TODO
- [`docs/infra/versioning.md`](docs/infra/versioning.md) — semantic version generation (`VERSION` file + auto-incrementing patch from git tags) and CI wiring, shared across both images

## Coding Standards

Backend code must follow [`docs/standards/aspnet-web-api-guidelines.md`](docs/standards/aspnet-web-api-guidelines.md). Key points, in brief:

- Use dependency injection wherever a dependency is needed — no inline `new` for services/repositories.
- Single responsibility per class/method — one endpoint class per route, one CQRS handler per command/query.
- Keep cyclomatic complexity below 15 per method/class.
- Prefer guard clauses / early returns over `else`.
- Otherwise follow standard .NET/C# and ASP.NET Core conventions.

## Architecture Notes

See [`docs/architecture/overview-architecture.md`](docs/architecture/overview-architecture.md) for the system-level component diagram (SPA ⇄ API ⇄ persistence) before diving into backend-specific details below.

- **Backend layout: `TodoApi.Gateway` (thin host) + `TodoApi.Todos` (feature library).** Modular monolith, designed to allow a feature to be extracted into its own service later without restructuring — one feature library per feature, referenced by the Gateway host, which holds no feature logic itself. `backend/` and `frontend/` are siblings at the repo root. See [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md#directory-structure) for the full tree.
- **Persistence: EF Core + SQLite**, a deliberate choice beyond the requirements' "file-based/in-memory is sufficient" minimum, to demonstrate real ORM usage. `ITodoRepository` wraps `TodoDbContext` directly, no separate Unit of Work layer (DbContext already is one). Migrations auto-apply at startup. SQLite file lives on a Kubernetes PersistentVolume in the home-lab demo deployment (ephemeral container filesystem would lose data on restart). See [`docs/architecture/backend/overview.md#persistence`](docs/architecture/backend/overview.md#persistence).
- **DTOs never cross below the service tier.** The service tier validates the incoming DTO and maps it to a domain Model; CQRS handlers, the repository, and persistence only ever operate on Models. See [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md#layered-view-n-tier) for the full layered diagram.
- **Mapping: hand-written extension methods, not a library.** AutoMapper requires a paid license for new/updated versions as of late 2024. Mapster was also considered — the specific package originally planned (`Mapster.SourceGenerator`, a supposed Roslyn incremental generator) doesn't exist on NuGet, and the real alternative (`Mapster.Tool` CLI codegen) has a post-build/build-lag mechanic not worth taking on for a model this small — so DTO⇄Model mapping is just plain static `ToModel()`/`ToResponse()` extension methods, no dependency, no codegen step. See [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md#mapping-dto--model).
- **Endpoint pattern:** each HTTP operation is its own class implementing a shared `IEndpoint` interface with a static `Map` method; `Request`/`Response`/`RequestValidator` live in the same file. `Endpoints.cs` is just the registration index — it maps every endpoint but contains no logic itself. See [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md#endpoint-pattern).
- **Bootstrapping split:** `ConfigureServices` (DI registration) and `ConfigureApps` (middleware pipeline) as separate static extension classes, keeping `Program.cs` to a few lines of orchestration. See [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md#bootstrapping).
- Phase 1 is single-user with no auth. Multi-user/auth is a stretch goal — see [`docs/architecture/authentication.md`](docs/architecture/authentication.md) — and would need the diagrams in `overview-architecture.md`/`backend/overview.md` updated if built.
- **TLS from the start, everywhere.** The app should never run over plain HTTP in any environment — local dev uses ASP.NET Core's dev HTTPS cert, the home lab demo deployment uses a Let's Encrypt cert. Applies to the frontend's nginx container too (PEM cert, no exception for "it's just static files") — see [`docs/infra/deployment.md`](docs/infra/deployment.md).
- **Two container images, one CI workflow each, path-filtered.** `docker/Dockerfile.gateway` (backend) and `docker/Dockerfile.app` (frontend, Node build → nginx runtime) each build to their own Harbor image (`todo-app-gateway`, `todo-app-web`). `backend-ci.yml` and `frontend-ci.yml` each own their own test → build → push pipeline, scoped by GitHub Actions' `paths:` filter to their own service's files — a frontend-only push never even triggers `backend-ci.yml`, and vice versa. There's no shared orchestrator workflow and no shared version number between the two — each generates its own independent version via `scripts/generate-version.sh <gateway|web>` (a fix after a real bug: an earlier shared-workflow design silently skipped the frontend image on a genuine frontend-only push). See [`docs/infra/dockerfile-organization.md`](docs/infra/dockerfile-organization.md), [`docs/infra/versioning.md`](docs/infra/versioning.md), and [`docs/infra/container-image-frontend.md`](docs/infra/container-image-frontend.md).
- **No hand-written frontend API client, generation wired into both builds.** `Microsoft.AspNetCore.OpenApi` generates the spec from the Minimal API endpoints as a backend post-build step (written to a committed `openapi.json`); **orval** generates a TypeScript client from that committed spec as a frontend pre-build step (`frontend/src/api/generated/`, committed, not a published package). Not a manual/on-demand step — automatic on every build so the client can't silently drift from the spec. `openapi-generator` was considered (multi-language, used before in a prior project) but ruled out for now — single TS consumer doesn't need it; revisit only if a real non-TS client requirement appears. See [`docs/architecture/overview-architecture.md`](docs/architecture/overview-architecture.md#api-contract--client-generation).
