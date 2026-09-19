# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

To-do list application built for the Foci Solutions take-home coding challenge. See [`docs/requirements/requirements.md`](docs/requirements/requirements.md) for the full assignment.

Stack: ASP.NET Core Minimal API backend (.NET 10), React (Vite) + React Router frontend. No application code has been written yet — the repo currently contains only planning/architecture docs and CI scaffolding. This file will need build/test/lint commands added once the projects are scaffolded.

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
- [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md) — backend design: directory structure (Gateway + feature-library modular monolith), Minimal API endpoint-per-class pattern, DTO/Model boundary, Mapster (codegen mode) for mapping, n-tier layering, bootstrapping (`ConfigureServices`/`ConfigureApps`)
- [`docs/architecture/backend/cqrs.md`](docs/architecture/backend/cqrs.md) — CQRS without a mediator library (MediatR ruled out, same licensing issue as AutoMapper); DI + a named interface per handler (`IAddTodoCommandHandler : ICommandHandler<TCommand, TResult>`) instead
- [`docs/architecture/authentication.md`](docs/architecture/authentication.md) — rough notes on optional multi-user/auth (Clerk vs. self-hosted Authentik); phase 1 is single-user, no auth
- [`docs/standards/aspnet-web-api-guidelines.md`](docs/standards/aspnet-web-api-guidelines.md) — backend coding standards (DI, single responsibility, cyclomatic complexity, minimal `else`, .NET conventions)
- [`docs/infra/harbor-registry-setup.md`](docs/infra/harbor-registry-setup.md) — Harbor container registry setup, if/when containerization happens (optional enhancement, not core scope)
- [`docs/infra/deployment.md`](docs/infra/deployment.md) — deployment target (home lab Kubernetes) and TLS strategy (dev certs locally, Let's Encrypt for the demo); cluster/ingress details still TODO
- [`docs/infra/versioning.md`](docs/infra/versioning.md) — semantic version generation (`VERSION` file + auto-incrementing patch from git tags) and CI wiring

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
- **Mapping: `Mapster.SourceGenerator`, not AutoMapper.** AutoMapper requires a paid license for new/updated versions as of late 2024. Using Mapster's Roslyn incremental source generator (not Mapster's default runtime-reflection mode, and not the older `Mapster.Tool` CLI codegen path) — mapping is real compiler-generated code, runs automatically on every build, no `IMapper` service, no reflection. Call sites use `.Adapt<T>()`. See [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md#mapping-dto--model).
- **Endpoint pattern:** each HTTP operation is its own class implementing a shared `IEndpoint` interface with a static `Map` method; `Request`/`Response`/`RequestValidator` live in the same file. `Endpoints.cs` is just the registration index — it maps every endpoint but contains no logic itself. See [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md#endpoint-pattern).
- **Bootstrapping split:** `ConfigureServices` (DI registration) and `ConfigureApps` (middleware pipeline) as separate static extension classes, keeping `Program.cs` to a few lines of orchestration. See [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md#bootstrapping).
- Phase 1 is single-user with no auth. Multi-user/auth is a stretch goal — see [`docs/architecture/authentication.md`](docs/architecture/authentication.md) — and would need the diagrams in `overview-architecture.md`/`backend/overview.md` updated if built.
- **TLS from the start, everywhere.** The app should never run over plain HTTP in any environment — local dev uses ASP.NET Core's dev HTTPS cert, the home lab demo deployment uses a Let's Encrypt cert. See [`docs/infra/deployment.md`](docs/infra/deployment.md).
- **No hand-written frontend API client, generation wired into both builds.** `Microsoft.AspNetCore.OpenApi` generates the spec from the Minimal API endpoints as a backend post-build step (written to a committed `openapi.json`); **orval** generates a TypeScript client from that committed spec as a frontend pre-build step (`frontend/src/api/generated/`, committed, not a published package). Not a manual/on-demand step — automatic on every build so the client can't silently drift from the spec. `openapi-generator` was considered (multi-language, used before in a prior project) but ruled out for now — single TS consumer doesn't need it; revisit only if a real non-TS client requirement appears. See [`docs/architecture/overview-architecture.md`](docs/architecture/overview-architecture.md#api-contract--client-generation).
