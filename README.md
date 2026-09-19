# Foci Solutions To-Do App — Coding Challenge

A to-do list application built for the Foci Solutions take-home coding challenge. See [`docs/requirements/requirements.md`](docs/requirements/requirements.md) for the full assignment.

**Status:** In planning/design. Architecture and standards are fully specced (see [Documentation Map](#documentation-map) below); application code has not been written yet.

## Stack

- **Backend:** ASP.NET Core Minimal API, .NET 10, CQRS without a mediator library, EF Core + SQLite
- **Frontend:** React (Vite) + React Router
- **API contract:** OpenAPI spec generated from the backend at build time, consumed by a generated TypeScript client (orval) — no hand-written API client code

See [`docs/architecture/overview-architecture.md`](docs/architecture/overview-architecture.md) for the full system design.

## Build & Run

**TODO — not yet available.** No application code exists yet; this section will be filled in once the backend and frontend projects are scaffolded. Planned shape, per the architecture docs:

- Backend: `dotnet run` from `backend/src/TodoApi.Gateway/` (ASP.NET Core Minimal API, EF Core migrations auto-applied at startup against a local SQLite file)
- Frontend: `npm install && npm run dev` from `frontend/` (Vite dev server)

## Running Tests

**TODO — not yet available.** No tests exist yet. Testing strategy (per-layer approach: service tier, CQRS handlers, repository) is still an open item — see [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md) Open Questions.

## Design Choices

Full rationale for every architectural decision lives under [`docs/`](docs/) — this is a summary, not the source of truth. See the [Documentation Map](#documentation-map) below for where to go deeper.

**Backend architecture** ([`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md)):

- **Modular monolith** — a thin `TodoApi.Gateway` host references a `TodoApi.Todos` feature library. Designed as a monolith first, but the feature boundary already exists as a project reference, so a feature could be extracted into its own service later without restructuring.
- **Minimal API, endpoint-per-class** — each HTTP operation (Add, List, View, Update, Complete, Incomplete, Delete) is its own class implementing a shared `IEndpoint` interface, not a controller action. Single responsibility at the endpoint level.
- **CQRS without a mediator library** — commands and queries are separated, but dispatched via direct DI injection of a named interface per handler (e.g. `IAddTodoCommandHandler`), not a mediator like MediatR. See [`docs/architecture/backend/cqrs.md`](docs/architecture/backend/cqrs.md) for why (licensing) and how.
- **DTOs never cross below the service tier** — the service tier validates the incoming DTO and maps it to a domain Model; everything below (CQRS handlers, repository, persistence) only ever sees Models. Mapping is via Mapster's source generator (`Mapster.SourceGenerator`), not AutoMapper (licensing) and not Mapster's default runtime-reflection mode.
- **Persistence: EF Core + SQLite** — a deliberate choice beyond the requirements' "file-based or in-memory is sufficient" minimum, to demonstrate real ORM usage. See [`docs/architecture/backend/overview.md#persistence`](docs/architecture/backend/overview.md#persistence).
- **Error responses:** RFC 9457 Problem Details, ASP.NET Core's built-in convention — no custom error DTO.
- **API contract:** OpenAPI spec generated at build time from endpoint metadata, committed to the repo, consumed by a generated TypeScript client (orval) on the frontend side. No hand-written API client.

**Frontend architecture:** doc not yet written — see [Documentation Map](#documentation-map).

**Testing strategy:** not yet written up — open item in [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md).

## Assumptions

The original requirements didn't specify everything needed to build this app. Rather than guess silently, the assumptions below were made deliberately and are documented in full in [`docs/requirements/requirements.md`](docs/requirements/requirements.md) (see "Decisions Made") and [`docs/requirements/requirements-qa-internal.md`](docs/requirements/requirements-qa-internal.md) (the full list of ambiguities considered). A trimmed set of genuinely load-bearing questions was sent to Foci — see [`docs/requirements/requirements-qa.md`](docs/requirements/requirements-qa.md).

Key assumptions:

- **Single-user, no auth** is the baseline (phase 1). Multi-user/auth is a stretch goal if time allows — see [`docs/architecture/authentication.md`](docs/architecture/authentication.md).
- **Commit history** is squashed before pushing (feature-by-feature commits during development, squashed into a clean history before the branch is pushed) — not kept as raw WIP commits.
- **TLS everywhere, from the start** — local dev uses ASP.NET Core's dev HTTPS cert; the demo deployment uses a Let's Encrypt cert for `foci-todo.thecameraeye.ca`.

## Trade-offs

- **EF Core + SQLite instead of the simplest option.** The requirements say file-based or in-memory storage is sufficient. EF Core + SQLite goes beyond that minimum specifically to demonstrate real ORM usage (migrations, change tracking, LINQ) — at the cost of more setup than a hand-rolled JSON file would need.
- **Modular monolith (Gateway + feature library) for a single-feature app.** Reflects a pattern used for multi-service work — the payoff (a feature boundary that already exists as a project reference) is mostly about not having to restructure later, not about anything gained today with only one feature.
- **CQRS with named interfaces per handler, no mediator library.** Gets shape/consistency and decorator-readiness without a runtime dispatch mechanism this project's scale doesn't need — see [`docs/architecture/backend/cqrs.md`](docs/architecture/backend/cqrs.md) for what's explicitly given up by not using a mediator (automatic pipeline behaviors, full caller/handler decoupling).

## Documentation Map

This repo's design decisions are documented as they were made, not written up after the fact — each doc below reflects real reasoning, not just a description of what exists.

**Requirements**

- [`docs/requirements/requirements.md`](docs/requirements/requirements.md) — the source assignment, plus decisions made and open questions
- [`docs/requirements/requirements-qa.md`](docs/requirements/requirements-qa.md) — trimmed questions sent to Foci
- [`docs/requirements/requirements-qa-internal.md`](docs/requirements/requirements-qa-internal.md) — full internal brainstorm of ambiguities (internal only, not sent to Foci)

**Architecture**

- [`docs/architecture/overview-architecture.md`](docs/architecture/overview-architecture.md) — system-level architecture, component diagram, repo layout, OpenAPI/client-generation pipeline
- [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md) — backend design: directory structure, endpoint pattern, mapping, bootstrapping, persistence, n-tier layering, error contract
- [`docs/architecture/backend/cqrs.md`](docs/architecture/backend/cqrs.md) — CQRS without a mediator library
- [`docs/architecture/authentication.md`](docs/architecture/authentication.md) — rough notes on optional multi-user/auth

**Standards**

- [`docs/standards/aspnet-web-api-guidelines.md`](docs/standards/aspnet-web-api-guidelines.md) — backend coding standards (DI, single responsibility, cyclomatic complexity, error responses, style)

**Infrastructure**

- [`docs/infra/harbor-registry-setup.md`](docs/infra/harbor-registry-setup.md) — container registry setup (optional enhancement)
- [`docs/infra/deployment.md`](docs/infra/deployment.md) — deployment target and TLS strategy
- [`docs/infra/versioning.md`](docs/infra/versioning.md) — semantic version generation and CI wiring

**For AI agents working in this repo:** see [`CLAUDE.md`](CLAUDE.md).
