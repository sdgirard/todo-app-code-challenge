# Foci Solutions To-Do App — Coding Challenge

A to-do list application built for the Foci Solutions take-home coding challenge. See [`docs/requirements/requirements.md`](docs/requirements/requirements.md) for the full assignment.

**Status:** Backend project skeleton scaffolded (`TodoApi.Gateway` + `TodoApi.Todos`, builds and tests run, no feature endpoints yet). Frontend not yet scaffolded. Architecture and standards are fully specced — see [Documentation Map](#documentation-map) below.

## Stack

- **Backend:** ASP.NET Core Minimal API, .NET 10, CQRS without a mediator library, EF Core + SQLite
- **Frontend:** React (Vite) + React Router
- **API contract:** OpenAPI spec generated from the backend at build time, consumed by a generated TypeScript client (orval) — no hand-written API client code

See [`docs/architecture/overview-architecture.md`](docs/architecture/overview-architecture.md) for the full system design.

## Build & Run

**Backend:** project skeleton exists (`TodoApi.Gateway` + `TodoApi.Todos`, no feature endpoints yet).

- Build: `dotnet build` from `backend/`
- Run: `dotnet run --project backend/src/TodoApi.Gateway/` (serves HTTPS-only, per [`docs/infra/deployment.md`](docs/infra/deployment.md); the OpenAPI spec regenerates to `backend/src/TodoApi.Gateway/openapi.json` on every build)
- Interactive API UI (Development only): `https://localhost:7020/scalar` via [Scalar](https://github.com/scalar/scalar) — raw spec served alongside it at `/openapi/v1.json`. See [`docs/architecture/backend/overview.md#interactive-ui-for-manual-testing`](docs/architecture/backend/overview.md#interactive-ui-for-manual-testing) for why Scalar.
- **Container:** `docker build -f docker/Dockerfile.gateway -t todo-app-gateway .` from the repo root. `docker build -f docker/Dockerfile.gateway --build-arg ENABLE_DEBUG=true -t todo-app-gateway:debug .` for a variant with `vsdbg` remote debugging. A second image for the frontend (`docker/Dockerfile.app`) is planned once `frontend/` is scaffolded — see [`docs/infra/dockerfile-organization.md`](docs/infra/dockerfile-organization.md). See [`docs/infra/container-image.md`](docs/infra/container-image.md) for the backend image's full design and [`docs/infra/harbor-registry-setup.md`](docs/infra/harbor-registry-setup.md) for pushing to the registry.

**Frontend:** **TODO — not yet available.** Not yet scaffolded. Planned: `npm install && npm run dev` from `frontend/` (Vite dev server).

## Running Tests

**Backend:** test project skeleton exists (`TodoApi.Todos.Tests`, `TodoApi.Gateway.Tests`), no tests written yet.

- Run: `dotnet test` from `backend/`

Testing strategy (per-layer approach: service tier, CQRS handlers, repository) is still an open item — see [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md) Open Questions.

**Frontend:** **TODO — not yet available.**

## Design Choices

Full rationale for every architectural decision lives under [`docs/`](docs/) — this is a summary, not the source of truth. See the [Documentation Map](#documentation-map) below for where to go deeper.

**Backend architecture** ([`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md)):

- **Modular monolith** — a thin `TodoApi.Gateway` host references a `TodoApi.Todos` feature library. Designed as a monolith first, but the feature boundary already exists as a project reference, so a feature could be extracted into its own service later without restructuring.
- **Minimal API, endpoint-per-class** — each HTTP operation (Add, List, View, Update, Complete/Incomplete, Delete) is its own class implementing a shared `IEndpoint` interface, not a controller action. Complete and Incomplete share one endpoint (`PATCH /todos/{id}`, driven by an `isCompleted` value) rather than two, since both are the same "set completion status to X" operation. Single responsibility at the endpoint level.
- **CQRS without a mediator library** — commands and queries are separated, but dispatched via direct DI injection of a named interface per handler (e.g. `IAddTodoCommandHandler`), not a mediator like MediatR. See [`docs/architecture/backend/cqrs.md`](docs/architecture/backend/cqrs.md) for why (licensing) and how.
- **DTOs never cross below the service tier** — the service tier validates the incoming DTO and maps it to a domain Model; everything below (CQRS handlers, repository, persistence) only ever sees Models. Mapping is hand-written (`ToModel()`/`ToResponse()` extension methods) — no AutoMapper (licensing) and no Mapster (the source-generator package it originally called for doesn't exist on NuGet; the real CLI-codegen alternative wasn't worth the build-lag trade-off for a model this small).
- **Persistence: EF Core + SQLite** — a deliberate choice beyond the requirements' "file-based or in-memory is sufficient" minimum, to demonstrate real ORM usage. See [`docs/architecture/backend/overview.md#persistence`](docs/architecture/backend/overview.md#persistence).
- **Error responses:** RFC 9457 Problem Details, ASP.NET Core's built-in convention — no custom error DTO.
- **API contract:** OpenAPI spec generated at build time from endpoint metadata, committed to the repo, consumed by a generated TypeScript client (orval) on the frontend side. No hand-written API client. **Scalar** provides the interactive UI for manually exercising the API in Development (Swagger UI's replacement now that it's out of the default .NET template).

**Frontend architecture:** doc not yet written — see [Documentation Map](#documentation-map).

**Testing strategy:** not yet written up — open item in [`docs/architecture/backend/overview.md`](docs/architecture/backend/overview.md).

## Assumptions

The original requirements didn't specify everything needed to build this app. Rather than guess silently, the assumptions below were made deliberately and are documented in full in [`docs/requirements/requirements.md`](docs/requirements/requirements.md) (see "Decisions Made") and [`docs/requirements/requirements-qa-internal.md`](docs/requirements/requirements-qa-internal.md) (the full list of ambiguities considered). A trimmed set of genuinely load-bearing questions was sent to Foci — see [`docs/requirements/requirements-qa.md`](docs/requirements/requirements-qa.md).

Key assumptions:

- **Single-user, no auth** is the baseline (phase 1). Multi-user/auth is a stretch goal if time allows — see [`docs/architecture/authentication.md`](docs/architecture/authentication.md).
- **Commit history** is squashed before pushing (feature-by-feature commits during development, squashed into a clean history before the branch is pushed) — not kept as raw WIP commits.
- **TLS everywhere, from the start, terminated by Kestrel itself (not the ingress)** — the cert (PFX, dev cert locally / Let's Encrypt for the demo) is mounted directly into the container and Kestrel does the handshake, so no hop (including ingress→pod, inside the cluster) is ever plaintext. Also keeps the door open for gRPC later without revisiting the TLS story. See [`docs/infra/container-image.md#tls-kestrel-terminates-it-directly`](docs/infra/container-image.md#tls-kestrel-terminates-it-directly).

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

**Feature Specs**

- [`docs/features/add-todo/spec.md`](docs/features/add-todo/spec.md) — `POST /todos`: full `TodoModel` schema, DTO contract, validation, mapping, CQRS, persistence, and test plan for the first endpoint
- [`docs/features/list-todos/spec.md`](docs/features/list-todos/spec.md) — `GET /todos`: first read endpoint and first CQRS query; bare-array contract, `IQueryHandler<,>`, and the ordering/filtering deferral
- [`docs/features/get-todo-by-id/spec.md`](docs/features/get-todo-by-id/spec.md) — `GET /todos/{id}`: the View requirement; first route parameter, first `404` outcome, and the RFC 9457 not-found contract
- [`docs/features/update-todo/spec.md`](docs/features/update-todo/spec.md) — `PUT /todos/{id}`: the Update requirement; full-resource replacement (including `isCompleted`), first CQRS command with a real existence-check rule
- [`docs/features/delete-todo/spec.md`](docs/features/delete-todo/spec.md) — `DELETE /todos/{id}`: the Delete requirement; hard delete, `204` on success
- [`docs/features/update-completion-status/spec.md`](docs/features/update-completion-status/spec.md) — `PATCH /todos/{id}`: the Complete/Incomplete requirements, collapsed into one endpoint that dispatches through `UpdateTodo`'s existing command rather than a new write path

**Infrastructure**

- [`docs/infra/dockerfile-organization.md`](docs/infra/dockerfile-organization.md) — `docker/` layout for multiple Dockerfiles, per-service image naming, CI matrix build (optional enhancement)
- [`docs/infra/container-image.md`](docs/infra/container-image.md) — backend (`todo-app-gateway`) container image design: Kestrel-terminated TLS, SQLite volume mount, cache-optimized build stages, multi-arch (`linux/amd64`+`linux/arm64`) build, `ENABLE_DEBUG` remote-debugging build arg (optional enhancement)
- [`docs/infra/harbor-registry-setup.md`](docs/infra/harbor-registry-setup.md) — container registry setup, including Harbor's built-in (Trivy) vulnerability scanning (optional enhancement)
- [`docs/infra/deployment.md`](docs/infra/deployment.md) — deployment target and TLS strategy
- [`docs/infra/versioning.md`](docs/infra/versioning.md) — semantic version generation and CI wiring
- [`docs/infra/outstanding-items.md`](docs/infra/outstanding-items.md) — deferred infra decisions tracked for later (resource limits, image scanning)

**For AI agents working in this repo:** see [`CLAUDE.md`](CLAUDE.md).
