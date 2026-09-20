# Foci Solutions To-Do App — Coding Challenge

A to-do list application built for the Foci Solutions take-home coding challenge. See [`docs/requirements/requirements.md`](docs/requirements/requirements.md) for the full assignment.

**Status:** Feature-complete end to end. Backend CRUD + completion-status endpoints implemented and tested (`TodoApi.Gateway` + `TodoApi.Todos`) — Add, List, View, Update, Delete, and Complete/Incomplete all working against SQLite via EF Core. Frontend (Vite + React 19 + React Router + Tailwind CSS v4) implements all seven requirement-level operations across two routes, backed by an orval-generated TypeScript client and tested with Vitest/RTL/MSW. See [Documentation Map](#documentation-map) below for the full design.

## Stack

- **Backend:** ASP.NET Core Minimal API, .NET 10, CQRS without a mediator library, EF Core + SQLite
- **Frontend:** React (Vite) + React Router
- **API contract:** OpenAPI spec generated from the backend at build time, consumed by a generated TypeScript client (orval) — no hand-written API client code

See [`docs/architecture/overview-architecture.md`](docs/architecture/overview-architecture.md) for the full system design.

## Build & Run

**Backend:** all six feature endpoints implemented (`POST /todos`, `GET /todos`, `GET /todos/{id}`, `PUT /todos/{id}`, `PATCH /todos/{id}`, `DELETE /todos/{id}`) — see [Feature Specs](#documentation-map) below for each one's contract.

- Build: `dotnet build` from `backend/`
- Run: `dotnet run --project backend/src/TodoApi.Gateway/` (serves HTTPS-only, per [`docs/infra/deployment.md`](docs/infra/deployment.md); the OpenAPI spec regenerates to `backend/src/TodoApi.Gateway/openapi.json` on every build)
- Interactive API UI (Development only): `https://localhost:7020/scalar` via [Scalar](https://github.com/scalar/scalar) — raw spec served alongside it at `/openapi/v1.json`. See [`docs/architecture/backend/overview.md#interactive-ui-for-manual-testing`](docs/architecture/backend/overview.md#interactive-ui-for-manual-testing) for why Scalar.
- **Container:** `docker build -f docker/Dockerfile.gateway -t todo-app-gateway .` from the repo root. `docker build -f docker/Dockerfile.gateway --build-arg ENABLE_DEBUG=true -t todo-app-gateway:debug .` for a variant with `vsdbg` remote debugging. See [`docs/infra/container-image.md`](docs/infra/container-image.md) for the backend image's full design and [`docs/infra/harbor-registry-setup.md`](docs/infra/harbor-registry-setup.md) for pushing to the registry.
- **Pre-built image (no build needed):** `docker pull docker.thecameraeye.ca/todo-app/todo-app-gateway:latest` — CI publishes here on every push to `main` that touches `backend/**`. The `todo-app` project is public (pull only, no login) specifically so this image is reviewer-accessible without Harbor credentials — see [`docs/infra/harbor-registry-setup.md#project-visibility-public-dedicated-project`](docs/infra/harbor-registry-setup.md#project-visibility-public-dedicated-project).

**Frontend:** two routes cover all seven requirement-level operations — `/` (List, Add) and `/todos/:id` (View, Update, Complete/Incomplete, Delete). Requires the backend running locally first (see above) so `predev`/`prebuild` can generate the API client from its `openapi.json`.

- Install: `npm install` from `frontend/`
- Run: `npm run dev` from `frontend/` (Vite dev server, `https://localhost:5173`; `predev` runs orval automatically first — see [API Contract & Client Generation](#documentation-map))
- Build: `npm run build` from `frontend/` (`prebuild` regenerates the API client, then `tsc -b && vite build`)
- Lint: `npm run lint` from `frontend/` (oxlint)
- **Container:** `docker build -f docker/Dockerfile.app -t todo-app-web .` from the repo root — Node build stage, nginx (HTTPS-only, same TLS-everywhere convention as the backend) runtime stage. See [`docs/infra/container-image-frontend.md`](docs/infra/container-image-frontend.md) for the full design.
- **Pre-built image (no build needed):** `docker pull docker.thecameraeye.ca/todo-app/todo-app-web:latest` — CI publishes here on every push to `main` that touches `frontend/**`.

## Running Tests

**Backend:** `TodoApi.Todos.Tests` (validators, mapping, CQRS handlers) and `TodoApi.Gateway.Tests` (full-pipeline integration tests via `WebApplicationFactory`, with `ITodoRepository` mocked) — 79 tests, all passing.

- Run: `dotnet test TodoApi.slnx` from `backend/` — always target the solution explicitly; a bare `dotnet test` can silently pick up only one of the two test projects.
- Test with coverage: `dotnet test TodoApi.slnx --collect:"XPlat Code Coverage" --settings tests.runsettings --results-directory ./TestResults` from `backend/`, then generate an HTML report with `reportgenerator` — see [`CLAUDE.md`](CLAUDE.md) for the full command.

Per-layer testing strategy: `RequestValidator` rules, `TodoMapping` methods, and CQRS command/query handlers (mocked `ITodoRepository` via Moq) are unit tested in `TodoApi.Todos.Tests`; each endpoint's full HTTP contract (status codes, response shape, validation ordering) is integration tested in `TodoApi.Gateway.Tests`. Every feature spec under [`docs/features/`](docs/features/) documents its own Tests section following this split.

**Frontend:** Vitest + React Testing Library + MSW, 56 tests across 7 files, all passing.

- Run: `npm run test` from `frontend/`

Mirrors the backend's per-layer split: `lib/problemDetails.ts` is unit tested directly; `components/*` (`TodoList`, `TodoForm`, `TodoDetail`, `ConfirmDialog`) are tested in isolation with RTL; `routes/*` (`TodoListRoute`, `TodoDetailRoute`) are tested end-to-end within the test process — loader → render → user interaction → action → re-render — via `createMemoryRouter` with MSW stubbing the generated client's HTTP calls at the network layer, the closest frontend analogue to the backend's `WebApplicationFactory` integration tests.

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

**Frontend architecture** ([`docs/architecture/frontend/overview.md`](docs/architecture/frontend/overview.md)):

- **React Router's data APIs (loaders/actions) are the data layer** — no separate state-management or data-fetching library (e.g. TanStack Query). A route's loader fetches its data, its action handles mutations, and the router re-runs the loader after an action so the list refreshes itself with no manual cache invalidation. Chosen because it's a capability of a dependency already committed to, not a new one to learn/explain for a six-endpoint CRUD app.
- **Two routes cover all seven requirement-level operations** — `/` (List, Add) and `/todos/:id` (View, Update, Complete/Incomplete, Delete), the same Complete/Incomplete-collapsing precedent the backend already set for its own endpoint. A route with more than one kind of mutation dispatches by an `intent` field in the submitted form data rather than growing extra routes or actions.
- **Tailwind CSS, no component library** — Ant Design and MUI were both considered and ruled out as more dependency than a list/form/button UI needs, especially given the requirements explicitly de-emphasize visual polish.
- **Generated API client only** — orval generates a typed client from the backend's committed `openapi.json` as a frontend pre-build step; loaders/actions call it directly, no hand-written `fetch` calls.
- **Testing:** Vitest + React Testing Library, with MSW mocking HTTP calls at the network layer — the frontend equivalent of the backend's `Mock<ITodoRepository>` boundary. Route-level RTL+MSW tests exercise the full loader → render → action cycle, the closest frontend analogue to the backend's `WebApplicationFactory` integration tests. No Playwright/E2E, ruled out for this project's time budget.
- **Local dev runs the Vite server over HTTPS too** (`https://localhost:5173`, same dev cert as the backend) — TLS-everywhere applies to the frontend dev server, not just the API. This is also the origin the backend's CORS policy allows, resolving a placeholder the backend's Add Todo spec had left open.

**Testing strategy:** per-layer — validators, mapping, and CQRS handlers unit tested with mocked (Moq) dependencies in `TodoApi.Todos.Tests`; each endpoint's full HTTP contract integration tested via `WebApplicationFactory` (with `ITodoRepository` mocked, real EF Core/SQLite migrations applied at startup) in `TodoApi.Gateway.Tests`. See [Running Tests](#running-tests) above.

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
- [`docs/architecture/frontend/overview.md`](docs/architecture/frontend/overview.md) — frontend design: routing/data layer, styling, generated-client integration, testing strategy

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

- [`docs/infra/dockerfile-organization.md`](docs/infra/dockerfile-organization.md) — `docker/` layout for multiple Dockerfiles, per-service image naming, per-service path-filtered CI (optional enhancement)
- [`docs/infra/container-image.md`](docs/infra/container-image.md) — backend (`todo-app-gateway`) container image design: Kestrel-terminated TLS, SQLite volume mount, cache-optimized build stages, multi-arch (`linux/amd64`+`linux/arm64`) build, `ENABLE_DEBUG` remote-debugging build arg (optional enhancement)
- [`docs/infra/container-image-frontend.md`](docs/infra/container-image-frontend.md) — frontend (`todo-app-web`) container image design: Node build stage + nginx runtime stage, nginx-terminated TLS, SPA fallback routing, `version.json` (optional enhancement)
- [`docs/infra/harbor-registry-setup.md`](docs/infra/harbor-registry-setup.md) — container registry setup, including Harbor's built-in (Trivy) vulnerability scanning (optional enhancement)
- [`docs/infra/deployment.md`](docs/infra/deployment.md) — deployment target and TLS strategy
- [`docs/infra/versioning.md`](docs/infra/versioning.md) — semantic version generation and CI wiring
- [`docs/infra/outstanding-items.md`](docs/infra/outstanding-items.md) — deferred infra decisions tracked for later (resource limits, image scanning)

**For AI agents working in this repo:** see [`CLAUDE.md`](CLAUDE.md).
