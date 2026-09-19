# Backend Architecture

Covers the ASP.NET Core Minimal API backend described in [`../overview-architecture.md`](../overview-architecture.md).

**Target framework: .NET 10** (current LTS release).

## Approach

- **Minimal API** — each endpoint is a small, single-responsibility handler (single responsibility principle applied at the endpoint level, not just the class level). No controller classes; each route maps directly to a thin handler that delegates out immediately.
- **CQRS** — commands and queries are separated, with no mediator library. See [`cqrs.md`](./cqrs.md) for the full pattern.
- **Two layers of validation, two different jobs:**
  - **Service tier (DTO-level validation):** shape/presence checks on the incoming request — is `title` present, is `dueDate` a parseable date, is the request well-formed. This is "is the request valid input," not "is this allowed."
  - **CQRS handlers (business logic / stricter rules):** domain rules enforced here — e.g. rules that depend on existing state or domain invariants, not just the shape of one request. This is "is this operation allowed to happen."
- **DTOs never cross below the service tier.** The service tier is the boundary: it validates the incoming DTO, then maps it to a domain Model before handing anything to CQRS. Commands, queries, handlers, and the repository/persistence layer only ever see Models — never the API's DTO types. This keeps the API contract (DTO shape) decoupled from the domain model, so either can change without forcing a change in the other.

The split matters because it keeps cheap, request-shaped checks (DTO validation) from being tangled up with the kind of check that needs to know about the rest of the system (business rules). Endpoint handlers stay dumb; CQRS handlers stay focused on one command or query each, working only with domain Models.

## Directory Structure

Modular monolith: a thin **Gateway** host project references one **class library per feature**. Same shape I use for multi-service work — designed as a monolith first, but the feature boundary already exists as a project reference, so a feature can be pulled out into its own service later without restructuring, if that's ever needed. Right now there's one feature (Todos), so the payoff is mostly about not having to restructure later, not about splitting anything today.

```text
backend/
├── TodoApi.slnx                          # .slnx (XML), not the classic GUID-heavy .sln — diff-friendly
├── src/
│   ├── TodoApi.Gateway/                  # thin host — API entry point, no feature logic
│   │   ├── Program.cs
│   │   ├── ConfigureServices.cs
│   │   ├── ConfigureApps.cs
│   │   ├── Endpoints.cs                  # registration index only, see Endpoint Pattern below
│   │   ├── openapi.json                  # generated spec output, see API Contract / OpenAPI below
│   │   └── TodoApi.Gateway.csproj
│   │
│   └── TodoApi.Todos/                    # feature library — everything Todos-specific
│       ├── Endpoints/
│       │   ├── AddTodoEndpoint.cs
│       │   ├── ListTodosEndpoint.cs
│       │   ├── GetTodoByIdEndpoint.cs
│       │   ├── UpdateTodoEndpoint.cs
│       │   ├── UpdateCompletionStatusEndpoint.cs # Complete + Incomplete, one endpoint — see features/update-completion-status/spec.md
│       │   ├── DeleteTodoEndpoint.cs
│       │   ├── IEndpoint.cs
│       │   └── ValidationFilter.cs
│       ├── Commands/                     # CQRS commands + handlers, one file each — see cqrs.md
│       │   ├── AddTodoCommand.cs
│       │   ├── UpdateTodoCommand.cs      # also backs UpdateCompletionStatusEndpoint — no separate Complete/Incomplete command
│       │   ├── DeleteTodoCommand.cs
│       │   └── ICommandHandler.cs
│       ├── Queries/                      # CQRS queries + handlers — see cqrs.md
│       │   ├── ListTodosQuery.cs
│       │   ├── GetTodoByIdQuery.cs
│       │   └── IQueryHandler.cs
│       ├── Models/
│       │   └── TodoModel.cs
│       ├── Dtos/                         # endpoint-facing response DTOs shared across endpoints — see features/get-todo-by-id/spec.md
│       │   └── TodoResponse.cs
│       ├── Persistence/                  # repository interface + EF Core implementation, see Persistence below
│       │   ├── ITodoRepository.cs
│       │   ├── TodoRepository.cs
│       │   ├── TodoDbContext.cs
│       │   └── Migrations/               # EF Core migrations
│       ├── TodoMapping.cs                # hand-written DTO<->Model mapping, see Mapping section above
│       ├── Extensions/
│       │   └── ServiceCollectionExtensions.cs  # AddTodosServices(), pulled into Gateway's ConfigureServices
│       └── TodoApi.Todos.csproj
│
└── tests/
    ├── TodoApi.Todos.Tests/              # RequestValidator, TodoMapping, and CQRS handler unit tests (Moq)
    └── TodoApi.Gateway.Tests/            # full-pipeline integration tests via WebApplicationFactory (ITodoRepository mocked)
```

See [`../overview-architecture.md#repo-layout`](../overview-architecture.md#repo-layout) for where `backend/` sits relative to `frontend/` and the rest of the repo.

**Persistence lives inside `TodoApi.Todos`** (a `Persistence/` folder), not a separate `TodoApi.Data` project — with a single feature, a standalone data project would be ceremony with no payoff since nothing else needs to share it. Worth revisiting only if a second feature needs to share the same `DbContext`.

## Persistence

**EF Core + SQLite.** The requirements doc says file-based storage or an in-memory store is sufficient — a full database isn't required. EF Core is a deliberate choice beyond that minimum, to demonstrate real ORM usage (DbContext, migrations, change tracking, LINQ queries) rather than the simplest thing that satisfies the requirement. SQLite keeps that choice compatible with "file-based storage is sufficient": the whole database is one file, no separate DB server to run or deploy.

Considered and ruled out:

- **Hand-rolled file-based storage (JSON/CSV)** — satisfies the requirements literally, but doesn't demonstrate anything about EF Core, data modeling, or migrations.
- **EF Core In-Memory provider** — satisfies "in-memory store is acceptable" literally, but Microsoft explicitly documents it as unsuited for real application use (no real relational semantics, some LINQ translations behave differently from a real provider) — it's a testing tool, not a persistence choice for a running app.
- **EF Core + Postgres/SQL Server** — a real server-based database is feasible given the home lab, but adds real infra (a running DB service, connection management, backups) beyond what this project's scope calls for.

### Repository shape

`ITodoRepository` is a thin wrapper directly over `TodoDbContext`/`DbSet<TodoModel>` — no separate Unit of Work abstraction on top. `DbContext` already *is* a unit of work (it tracks changes and commits them together via `SaveChangesAsync`), so adding another layer on top would duplicate what EF Core already provides. Appropriate because there's a single entity and no cross-repository transactions to coordinate; would be worth revisiting only if a second feature's repository needed to commit alongside `ITodoRepository` in the same transaction.

The actual interface, as it shipped (see [`../../features/update-todo/spec.md#repository`](../../features/update-todo/spec.md#repository) and [`../../features/delete-todo/spec.md#repository`](../../features/delete-todo/spec.md#repository) for why `GetByIdAsync`/`GetTrackedByIdAsync` are two separate methods rather than one with a `track: bool` flag):

```csharp
public interface ITodoRepository
{
    Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoModel>> ListAsync(CancellationToken cancellationToken);
    Task<TodoModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TodoModel?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TodoModel> UpdateAsync(TodoModel todo, CancellationToken cancellationToken);
    Task DeleteAsync(TodoModel todo, CancellationToken cancellationToken);
}

public sealed class TodoRepository(TodoDbContext dbContext) : ITodoRepository
{
    public async Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken)
    {
        dbContext.Todos.Add(todo);
        await dbContext.SaveChangesAsync(cancellationToken);
        return todo;
    }

    public Task<TodoModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    // ListAsync / GetTrackedByIdAsync / UpdateAsync / DeleteAsync follow the same shape —
    // EF Core specifics fully contained here, CQRS handlers only see ITodoRepository.
}
```

### Migrations

Applied automatically at app startup (`dbContext.Database.Migrate()`, called from `ConfigureApps`), not via a separate CI/CD or manual step. Simplest option for a single-deployment demo app — no extra pipeline step to remember, no risk of forgetting to migrate before a demo. This trades away the safety a separate migration step gives on a multi-replica production deployment (avoiding two instances racing to migrate at once); not a concern here since the demo runs a single replica.

### Where the SQLite file lives

For the home-lab Kubernetes demo deployment (see [`../../infra/deployment.md`](../../infra/deployment.md)), the SQLite file is written to `/data/todo.db`, a path backed by a **PersistentVolume** mounted into the container, not the container's own ephemeral filesystem — otherwise the to-do list would reset every time the pod restarts or redeploys. The connection string is supplied via config/env (`ConnectionStrings__TodoDb=Data Source=/data/todo.db`), not hardcoded, so it can point at different paths per environment without a rebuild. Full detail (mount conventions, local-dev fallback) in [`../../infra/container-image.md#persistence-sqlite-path-via-data`](../../infra/container-image.md#persistence-sqlite-path-via-data). The PVC/StorageClass definition itself is still TODO in `deployment.md`'s Cluster/Ingress section.

Locally, the file just lives on disk in the working directory (relative path, via `appsettings.Development.json`) — no PVC or mount needed outside the cluster.

## Mapping (DTO ⇄ Model)

**Not using AutoMapper** — as of late 2024 AutoMapper requires a paid commercial license for new/updated versions, which isn't appropriate to pull into this project.

**Not using Mapster either — hand-written mapping methods instead.** History of this decision, since it changed twice while implementing the first endpoint ([`../../features/add-todo/spec.md`](../../features/add-todo/spec.md)):

1. Originally specified a package named `Mapster.SourceGenerator` (a supposed true Roslyn incremental source generator). **That package does not exist on NuGet** — verified directly against the NuGet API and search.
2. Considered `Mapster.Tool` (real CLI codegen) as the closest actual match — but its mechanic is a *post-build* MSBuild target: `dotnet mapster mapper -a <built.dll>` runs after a build completes, reflecting over the already-compiled assembly to emit `.g.cs` files that only take effect on the **next** build. Not a hard failure (Mapster's runtime `.Adapt<T>()` fallback covers the gap), but real build-lag/onboarding friction for a project this small.
3. **Chosen: hand-written mapping**, plain static extension methods, no library at all. For a `TodoModel` with 6 fields and a single feature, any mapping library (codegen or runtime-reflection) is more indirection than the problem justifies — a few explicit lines are just as easy to write, have zero dependency/build-order quirks, and are trivially unit-testable. Revisit only if a second feature or a genuinely complex mapping (nested objects, nontrivial transforms) makes hand-written mapping actually tedious.

- One static class per feature, named `{Feature}Mapping`, living alongside that feature's endpoints (not a separate top-level `Mapping/` folder) — e.g. `TodoMapping` next to the `Todos` endpoint classes. Same co-location principle Mapster's config would have followed.
- Each conversion is an explicit extension method: `ToModel()` for DTO → Model, `ToResponse()` for Model → DTO. No attribute magic, no runtime type discovery — just a method that sets each field.
- Only trivial, mostly 1:1 mappings are expected here (DTO fields to Model fields, same names) — if a mapping ever needs real logic, that logic belongs in the service tier or the mapping method itself, not smuggled into a CQRS handler.

The actual `TodoMapping.cs`, as it shipped — `ToResponse()` targets the shared `TodoResponse` DTO (see [`../../features/get-todo-by-id/spec.md#shared-todoresponse-dto`](../../features/get-todo-by-id/spec.md#shared-todoresponse-dto)), not a per-endpoint `Response` record, and `ApplyTo()` was added by [`../../features/update-todo/spec.md#mapping-todomapping`](../../features/update-todo/spec.md#mapping-todomapping) to mutate an existing tracked `TodoModel` in place rather than construct a new one:

```csharp
// TodoMapping.cs — hand-written, no codegen, no library.
public static class TodoMapping
{
    public static TodoModel ToModel(this AddTodoEndpoint.Request request) => new()
    {
        Id = Guid.NewGuid(),
        Title = request.Title,
        Description = request.Description,
        DueDate = request.DueDate,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = null,
    };

    public static void ApplyTo(this UpdateTodoEndpoint.Request request, TodoModel model)
    {
        model.Title = request.Title;
        model.Description = request.Description;
        model.DueDate = request.DueDate;
        model.IsCompleted = request.IsCompleted;
        model.UpdatedAt = DateTime.UtcNow;
    }

    public static TodoResponse ToResponse(this TodoModel model) => new(
        model.Id,
        model.Title,
        model.Description,
        model.DueDate,
        model.IsCompleted,
        model.CreatedAt,
        model.UpdatedAt);
}
```

```csharp
// Call site — same shape regardless of whether the mapping is a straight
// property copy or needs field-level logic (both are just C# in the method body).
var model = request.ToModel();
var response = result.Todo.ToResponse();
```

## Endpoint Pattern

Reusing a pattern I've used before on another Minimal API project: each endpoint is its own class, not a controller action. One class = one route = one responsibility.

- Each endpoint class implements a shared `IEndpoint` interface with a static `Map(IEndpointRouteBuilder app)` method that registers its own route, name, and DTO validation filter.
- The endpoint's `Request`/`Response` records and its `RequestValidator` (DTO-level validation) live right next to the `Map`/`Handle` methods, in the same file — everything about "how this one HTTP call is shaped" stays together.
- The `Handle` method is a thin static handler: bind request → call the CQRS command/query handler → map the result to a typed HTTP response (`TypedResults.Ok`, `TypedResults.NotFound`, etc.) → done. No business logic here.
- Endpoints are grouped by feature and registered from a single place (`app.MapGroup("/todos")...MapEndpoint<AddTodoEndpoint>()...`), so the route table is readable in one spot without hunting through controller classes.

Each CRUD/status operation gets its own endpoint class under this pattern, rather than one `TodosController` with several actions — with one deliberate exception: Complete and Incomplete share a single `UpdateCompletionStatusEndpoint` class (`PATCH /todos/{id}`, driven by an `isCompleted` value in the request body) rather than two near-identical classes differing only in a hardcoded `true`/`false`. See [`../../features/update-completion-status/spec.md#scope`](../../features/update-completion-status/spec.md#scope) for the reasoning. Six endpoint classes cover the seven requirement-level operations (Add, List, View, Update, Complete/Incomplete, Delete).

The actual `AddTodoEndpoint.cs`, as it shipped — note `Response` doesn't exist as a per-endpoint record; `Handle` returns the shared `TodoResponse` DTO instead (see [`../../features/get-todo-by-id/spec.md#shared-todoresponse-dto`](../../features/get-todo-by-id/spec.md#shared-todoresponse-dto)), and `DueDate` binds as `DateTime?`, not `DateOnly?`:

```csharp
public sealed class AddTodoEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/", Handle)
            .WithName("AddTodo")
            .WithSummary("Add a new to-do item")
            .AddEndpointFilter<ValidationFilter<Request>>();

    public sealed record Request(string Title, string? Description, DateTime? DueDate);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
            RuleFor(x => x.DueDate)
                .GreaterThanOrEqualTo(DateTime.UtcNow.Date)
                .When(x => x.DueDate is not null)
                .WithMessage("DueDate must not be in the past.");
        }
    }

    private static async Task<Created<TodoResponse>> Handle(
        [FromBody] Request request,
        [FromServices] IAddTodoCommandHandler handler, // named interface, see cqrs.md
        CancellationToken cancellationToken)
    {
        // DTO -> Model mapping happens here, at the service tier boundary,
        // via a hand-written extension method (see Mapping section below).
        // The CQRS handler below only ever sees TodoModel, never Request/Response.
        var model = request.ToModel();

        var result = await handler.HandleAsync(new AddTodoCommand(model), cancellationToken);

        var response = result.Todo.ToResponse();
        return TypedResults.Created($"/todos/{response.Id}", response);
    }
}
```

### `Endpoints.cs` — the route registration index

Each endpoint class owns its own route (via its `Map` method), but something still has to call `Map` for every one of them at startup. That's the only job of `Endpoints.cs`: it's a single, static file that registers every endpoint onto the app, grouped by feature/route prefix. It holds no request handling, no validation, no business logic — just the list of "these are all the endpoints that exist, and here's the route group each one lives under."

Having one file like this means the full route table is visible in one place without opening every endpoint class, while the endpoint classes themselves stay independent and self-contained.

The actual `Endpoints.cs`, as it shipped (also registers a `VersionEndpoint` outside the `/todos` group, omitted here — see [`../overview-architecture.md`](../overview-architecture.md)):

```csharp
public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        var todos = app.MapGroup("/todos")
            .WithTags("Todos");

        todos
            .MapEndpoint<AddTodoEndpoint>()
            .MapEndpoint<ListTodosEndpoint>()
            .MapEndpoint<GetTodoByIdEndpoint>()
            .MapEndpoint<UpdateTodoEndpoint>()
            .MapEndpoint<UpdateCompletionStatusEndpoint>()
            .MapEndpoint<DeleteTodoEndpoint>();
    }

    private static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app)
        where TEndpoint : IEndpoint
    {
        TEndpoint.Map(app);
        return app;
    }
}
```

`ConfigureApps.Configure()` calls `app.MapEndpoints()` once during pipeline setup — see Bootstrapping below.

## Bootstrapping

App startup logic is split across two static classes, keeping `Program.cs` itself minimal:

- **`ConfigureServices`** — everything that registers services into the DI container (`builder.Services.Add...`): persistence/repository registration, validators, CORS policy, Swagger, logging. One method (`AddServices(this WebApplicationBuilder builder)`) as the entry point, broken into small private helpers per concern.
- **`ConfigureApps`** — everything that builds the middleware pipeline on the built `WebApplication` (`app.Use...`, `app.Map...`): Swagger UI, CORS, endpoint mapping. One method (`Configure(this WebApplication app)`) as the entry point.

`Program.cs` itself just does `builder.AddServices()` → `builder.Build()` → `app.Configure()` → `app.Run()`. Keeps startup wiring out of the file that's supposed to just be the entry point, and keeps "what's registered" separate from "what's in the pipeline."

## Request Flow

```mermaid
graph LR
    Client[React SPA] -->|HTTP/JSON| Endpoint[Minimal API Endpoint]
    Endpoint -->|DTO| Service[Service Tier<br/>DTO validation + DTO→Model mapping]
    Service -->|Model| CQRS[CQRS Handler<br/>Business rules]
    CQRS -->|Model| Repo[(Repository /<br/>Persistence)]
```

1. Endpoint receives the HTTP request, binds it to a DTO.
2. Service tier validates the DTO (required fields, format) — rejects early with `400` if invalid.
3. Service tier maps the validated DTO to a domain Model.
4. Service tier dispatches a command or query — built from the Model, not the DTO — to its CQRS handler.
5. CQRS handler applies business rules against the Model, talks to the repository/persistence layer (also Model-typed).
6. Result flows back up through the service tier, which maps the resulting Model back to a response DTO, to the endpoint, which maps it to an HTTP response.

Full detail on how commands/queries are dispatched (no mediator library) is in [`cqrs.md`](./cqrs.md).

## Layered View (N-Tier)

The DTO/Model split above is really about which types are allowed to exist at each layer. Everything at or below the service tier only knows about domain Models — the DTO is strictly an API-contract type that lives at the edge.

```mermaid
graph TB
    subgraph Presentation
        EP[Minimal API Endpoints<br/>DTOs in/out]
    end

    subgraph "Service Tier (boundary)"
        SVC["Service Layer<br/>DTO validation<br/>DTO ⇄ Model mapping"]
    end

    subgraph Application
        CQ[CQRS Command / Query Handlers<br/>Model only — business rules]
    end

    subgraph Domain
        MD[Domain Models]
    end

    subgraph Infrastructure
        REPO[Repository Interface]
        STORE[(Persistence<br/>File-based / In-memory)]
    end

    EP -->|DTO| SVC
    SVC -->|Model| CQ
    CQ --> MD
    CQ -->|Model| REPO
    REPO --> STORE
    SVC -.->|Model → DTO| EP
```

- **Presentation (Endpoints):** only tier that knows about DTOs and HTTP concerns (status codes, routes).
- **Service Tier:** the boundary/translation layer — DTO validation, and the only place DTO ⇄ Model mapping happens.
- **Application (CQRS):** business rules, working exclusively in domain Models. No knowledge of DTOs or HTTP. See [`cqrs.md`](./cqrs.md) for the dispatch mechanism.
- **Domain:** the Model types themselves — plain, framework-agnostic where possible.
- **Infrastructure (Repository/Persistence):** also Model-typed; swapping file-based for in-memory (or a real DB later) shouldn't ripple up past this layer.

## Resolved (formerly Open Questions / TODO)

All six feature endpoints are implemented; the items originally tracked here are resolved:

- ~~Exact command/query list per CRUD operation~~ — resolved: `AddTodoCommand`, `UpdateTodoCommand` (also backs Complete/Incomplete — see [`../../features/update-completion-status/spec.md#cqrs`](../../features/update-completion-status/spec.md#cqrs), no separate command was added), `DeleteTodoCommand`; `ListTodosQuery`, `GetTodoByIdQuery`. Full detail in [`cqrs.md`](./cqrs.md#registration) and each feature's own spec.
- ~~`IEndpoint` interface shape and shared endpoint-mapping helper~~ — resolved: `IEndpoint.cs` (a `static abstract void Map(IEndpointRouteBuilder app)` member) and the private `MapEndpoint<TEndpoint>()` helper in `Endpoints.cs`, both shown above, are what shipped.
- ~~SQLite package/connection string configuration, exact PVC mount path, local dev file path~~ — resolved: see [Where the SQLite file lives](#where-the-sqlite-file-lives) above. The PVC/StorageClass definition itself remains TODO in `deployment.md`'s Cluster/Ingress section — an infra item, not a backend-code one.
- ~~Testing strategy per layer~~ — resolved: `RequestValidator` rules, `TodoMapping` methods, and CQRS command/query handlers are unit tested with `Mock<ITodoRepository>` (Moq) in `TodoApi.Todos.Tests`; each endpoint's full HTTP contract is integration tested via `WebApplicationFactory` (repository still mocked) in `TodoApi.Gateway.Tests`. See [`../../standards/aspnet-web-api-guidelines.md#testing`](../../standards/aspnet-web-api-guidelines.md#testing) for the full writeup, including the real/mocked persistence trade-off this approach accepts.
- ~~`Mapster.SourceGenerator` NuGet package reference/version~~ — resolved: no mapping library used at all; hand-written `{Feature}Mapping` extension methods instead (see Mapping section above).
- CQRS dispatch mechanism specifics — see [`cqrs.md`](./cqrs.md) for the final registration list and the `IUpdateTodoCommandHandler`-has-two-callers exception.

## Error Response Contract

Decided: use ASP.NET Core's built-in RFC 9457 Problem Details format for all error responses — `TypedResults.NotFound()`, `TypedResults.Problem(...)`, `TypedResults.ValidationProblem(...)`, with `app.UseExceptionHandler()` catching unhandled exceptions as 500-level Problem Details. No custom error DTO. Full detail in [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md#error-responses).

This was an open question sent to Foci (see [`../../requirements/requirements-qa.md`](../../requirements/requirements-qa.md)) — Problem Details is a reasonable, framework-native default regardless of their answer, so it's safe to build against now rather than wait.

## API Contract / OpenAPI

Endpoint metadata (`.WithName()`, `.WithSummary()`, request/response types) feeds `Microsoft.AspNetCore.OpenApi`'s spec generation directly — the OpenAPI spec is generated from the endpoints themselves, not maintained by hand. Generated at **build time** (via `Microsoft.Extensions.ApiDescription.Server`, not `Microsoft.AspNetCore.OpenApi`'s runtime-only default) into `TodoApi.Gateway/openapi.json`, committed to the repo. See [`../overview-architecture.md#api-contract--client-generation`](../overview-architecture.md#api-contract--client-generation) for the full spec-to-TypeScript-client pipeline (orval) and why build-time generation was chosen over serving the spec live.

### Interactive UI for manual testing

**Scalar** (`Scalar.AspNetCore`, `app.MapScalarApiReference()`), Development-only, served alongside the raw spec at `MapOpenApi()`. Chosen over Swashbuckle's Swagger UI: the project already uses the built-in `Microsoft.AspNetCore.OpenApi` generator (not Swashbuckle's), and Microsoft's own docs point to Scalar as the successor now that Swagger UI was dropped from the default .NET template — wiring in Swashbuckle just for its UI half would mean carrying a second, redundant spec generator. Gated to Development in `ConfigureApps` alongside `MapOpenApi()` — not exposed in the home-lab demo deployment.

## Related Docs

- [`../overview-architecture.md`](../overview-architecture.md) — system-level overview, including the OpenAPI/client-generation pipeline
- [`cqrs.md`](./cqrs.md) — CQRS pattern without a mediator library
- [`../authentication.md`](../authentication.md) — auth is out of scope for phase 1; this doc assumes single-user
- [`../../infra/container-image.md`](../../infra/container-image.md) — container image design, including the SQLite `/data` mount
- [`../../infra/deployment.md`](../../infra/deployment.md) — TLS strategy, home-lab deployment target
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source requirements
- [`../../requirements/requirements-qa.md`](../../requirements/requirements-qa.md) — open questions sent to Foci
