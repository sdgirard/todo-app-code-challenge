# Feature Spec: Add Todo (`POST /todos`)

First feature endpoint built for the app, and the first pass at `TodoModel` — the shape decided here is the shared domain model every other Todos operation (List, View, Update, Complete, Incomplete, Delete) will build on. Follows the patterns in [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) and [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md); coding standards from [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) apply throughout.

## Scope

In scope: the `AddTodo` vertical slice end-to-end — endpoint, service-tier validation, DTO⇄Model mapping, CQRS command/handler, repository, `TodoDbContext`, initial EF Core migration, DI wiring, tests.

Out of scope (future specs): List, View, Update, Complete, Incomplete, Delete. This spec locks in `TodoModel`'s full shape (including fields no other endpoint needs yet, like `UpdatedAt`) so those endpoints don't force a second migration for fields that were foreseeable now.

## `TodoModel` (domain model)

Lives at `TodoApi.Todos/Models/TodoModel.cs`. Below the service tier — CQRS handlers, repository, and persistence only ever see this type, never the endpoint's `Request`/`Response` DTOs.

| Field | Type | Nullable | Set by | Notes |
|---|---|---|---|---|
| `Id` | `Guid` | No | Mapping config, at create time | App-generated (`Guid.NewGuid()`), not DB-generated. Avoids a round-trip to learn the new id and avoids exposing a sequential/guessable identifier. Primary key. |
| `Title` | `string` | No | Client (request body) | Required per requirements. Max length 200 — see [Validation Rules](#validation-rules-service-tier--requestvalidator) for rationale. |
| `Description` | `string?` | Yes | Client (request body) | Optional per requirements. Max length 2000 when present — see [Validation Rules](#validation-rules-service-tier--requestvalidator) for rationale. |
| `DueDate` | `DateTime?` | Yes | Client (request body) | **Deliberately wider than the requirement.** Requirements say `YYYY-MM-DD` date-only is sufficient; using `DateTime?` instead of `DateOnly?` allows an optional time component too. No custom converter or parsing step — `DateTime` has native `System.Text.Json` support, so the request/response DTO field is typed `DateTime?` directly and passes straight through to `TodoModel` with no conversion logic anywhere in the mapping config. A client can send `"2026-09-25"` (parses to midnight) or a full timestamp (`"2026-09-25T14:00:00Z"`) — both are valid input. |
| `IsCompleted` | `bool` | No | Mapping config, at create time | Always `false` on create per requirements. Not settable via `AddTodo`. |
| `CreatedAt` | `DateTime` | No | Mapping config, at create time | `DateTime.UtcNow`, server-set. Not client-settable. Plain `DateTime` (not `DateTimeOffset`) for type consistency with `DueDate`/`UpdatedAt` across the model — always UTC by convention, not enforced by the type itself. |
| `UpdatedAt` | `DateTime?` | Yes | Mapping config, at create time | **Added now, ahead of the Update spec**, to settle `TodoModel`'s full schema in one migration rather than churning it again when Update/Complete/Incomplete land. Null on create (nothing has updated it yet); those future specs define when/how it's set — out of scope here. Not exposed in `AddTodo`'s response DTO (see [DTO Contract](#dto-contract)) since nothing has happened to populate it yet. |

```csharp
namespace TodoApi.Todos.Models;

public sealed class TodoModel
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public required bool IsCompleted { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Timestamp convention:** every `DateTime`/`DateTime?` on `TodoModel` is UTC by convention (server-set fields use `DateTime.UtcNow`; client-supplied `DueDate` is trusted as-is, with no timezone conversion applied). Plain `DateTime` was chosen over `DateTimeOffset` so `DueDate`, `CreatedAt`, and `UpdatedAt` all share one type — simpler than mixing a client-supplied field with two server-set ones under different types. The trade-off: `DateTime` carries no explicit offset marker, so "UTC" here is a convention enforced by code (always call `.UtcNow`, never `.Now`), not something the type system guarantees.

**Serialization detail:** `System.Text.Json` only emits the trailing `Z` for a `DateTime` when its `.Kind` is `DateTimeKind.Utc` — `DateTime.UtcNow` carries that `Kind` correctly, so `CreatedAt` serializes as `"...Z"` as shown in the examples below. A client-supplied `DueDate`, however, round-trips whatever `Kind` `System.Text.Json` assigns during deserialization (`Utc` if the input had a `Z`/offset, `Unspecified` if it was a bare `"2026-09-25"` or `"2026-09-25T00:00:00"`) — so a `DueDate` sent without an explicit `Z`/offset will echo back in the response the same way, without one. This is a real, user-visible inconsistency worth a test assertion (see [Tests](#tests)) rather than an assumption; no normalization is applied on the way in for v1.

## DTO Contract

### `POST /todos`

**Request body:**

```json
{
  "title": "Buy milk",
  "description": "2% or whole, whichever is on sale",
  "dueDate": "2026-09-25T00:00:00Z"
}
```

- `title` — required, non-empty string.
- `description` — optional string, omit or `null` if absent.
- `dueDate` — optional. Bound directly as `DateTime?` — .NET's built-in `System.Text.Json` support parses any ISO 8601 value, so a date-only string (`"2026-09-25"`) or a full timestamp (`"2026-09-25T14:00:00Z"`) both work with no custom converter.

**Success response — `201 Created`:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Buy milk",
  "description": "2% or whole, whichever is on sale",
  "dueDate": "2026-09-25T00:00:00Z",
  "isCompleted": false,
  "createdAt": "2026-09-19T14:32:07.1234567Z"
}
```

- `Location` header: `/todos/{id}`.
- `UpdatedAt` is **not** included in the response — it's always null immediately after create, so surfacing it here adds noise without information. (List/View DTOs, defined in their own specs, will include it once it can be non-null.)

**Validation failure — `400 Bad Request`** (`ValidationProblemDetails`, per [`aspnet-web-api-guidelines.md#error-responses`](../../standards/aspnet-web-api-guidelines.md#error-responses)):

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Title": ["'Title' must not be empty."]
  }
}
```

## Validation Rules (service tier — `RequestValidator`)

FluentValidation, per [`aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md#architecture-standards):

- `Title`: `NotEmpty()`, `MaximumLength(200)`. 200 chosen over a tighter bound like 100 because "short description of the task" can still legitimately run 80-100+ characters for a real task title, and 200 leaves headroom without blurring into `Description`'s job; it's also in line with similar "title"-class fields elsewhere (e.g. GitHub issue titles at 256, Jira summary at 255). Not a storage-driven number — SQLite doesn't enforce `VARCHAR(n)` at the engine level, so this is purely an app-level validation choice.
- `Description`: `MaximumLength(2000)` when present (`RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null)`); still optional, no `NotEmpty()`. 2000 chosen as generous room for a genuine multi-sentence "longer explanation" (~300-400 words) without leaving the field fully unbounded — same non-storage-driven reasoning as `Title`'s 200-char bound above.
- `DueDate`: unparseable input is already rejected at the JSON deserialization stage (a `400` before the validator even runs). On top of that, the validator enforces `DueDate >= DateTime.UtcNow.Date` when present — a due date must not be in the past at creation time. This is still a service-tier, DTO-shape check (it only needs the request's own value plus the current clock, no other state), not a business rule in the CQRS handler.

  ```csharp
  RuleFor(x => x.DueDate)
      .GreaterThanOrEqualTo(DateTime.UtcNow.Date)
      .When(x => x.DueDate is not null)
      .WithMessage("DueDate must not be in the past.");
  ```

No business-rule validation applies to Create — there's no existing state to check against (that distinction — DTO-shape checks here vs. state-dependent rules in the CQRS handler — is the split documented in [`overview.md#approach`](../../architecture/backend/overview.md#approach)).

## Mapping (`TodoMapping`)

> **Superseded:** `AddTodoEndpoint.Response` and `ToResponse()` as described below were consolidated into a shared `TodoResponse` DTO (also used by List and GetById) in [`../get-todo-by-id/spec.md#shared-todoresponse-dto`](../get-todo-by-id/spec.md#shared-todoresponse-dto), once `AddTodo`'s response picked up `UpdatedAt` and all three endpoints' shapes became identical. Left as-written below for the historical record of the original decision.

**No mapping library** — hand-written static extension methods, per [`overview.md#mapping-dto--model`](../../architecture/backend/overview.md#mapping-dto--model) (AutoMapper ruled out on licensing; Mapster considered and dropped — the planned `Mapster.SourceGenerator` package doesn't exist on NuGet, and `Mapster.Tool`'s CLI-codegen alternative wasn't worth its build-lag mechanic for a model this small).

`TodoApi.Todos/TodoMapping.cs`:

```csharp
namespace TodoApi.Todos;

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

    public static AddTodoEndpoint.Response ToResponse(this TodoModel model) => new(
        model.Id,
        model.Title,
        model.Description,
        model.DueDate,
        model.IsCompleted,
        model.CreatedAt);
}
```

## CQRS

`TodoApi.Todos/Commands/AddTodoCommand.cs`, per [`cqrs.md`](../../architecture/backend/cqrs.md#the-pattern):

```csharp
public sealed record AddTodoCommand(TodoModel Todo);
public sealed record AddTodoCommandResult(TodoModel Todo);
public interface IAddTodoCommandHandler : ICommandHandler<AddTodoCommand, AddTodoCommandResult>;

public sealed class AddTodoCommandHandler(ITodoRepository repository) : IAddTodoCommandHandler
{
    public async Task<AddTodoCommandResult> HandleAsync(
        AddTodoCommand command,
        CancellationToken cancellationToken)
    {
        var saved = await repository.AddAsync(command.Todo, cancellationToken);
        return new AddTodoCommandResult(saved);
    }
}
```

No business rules to apply beyond persisting — flagged explicitly rather than left implicit, since every other command in this feature will have at least one (e.g. Complete needs "does this id exist" as a rule the handler enforces via a `404`-worthy repository result).

## Repository

`ITodoRepository.AddAsync(TodoModel, CancellationToken)` — first method needed on the interface; the full interface shape (all seven operations) is already sketched in [`overview.md#repository-shape`](../../architecture/backend/overview.md#repository-shape), but only `AddAsync` gets a real implementation in this spec. The others are added incrementally as their specs land, rather than stubbing unimplemented methods now.

```csharp
public interface ITodoRepository
{
    Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken);
}

public sealed class TodoRepository(TodoDbContext dbContext) : ITodoRepository
{
    public async Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken)
    {
        dbContext.Todos.Add(todo);
        await dbContext.SaveChangesAsync(cancellationToken);
        return todo;
    }
}
```

## Persistence

### `TodoDbContext`

`TodoApi.Todos/Persistence/TodoDbContext.cs`:

```csharp
public sealed class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoModel> Todos => Set<TodoModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoModel>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).IsRequired();
        });
    }
}
```

### Migration

One initial migration (`InitialCreate`) creating the `Todos` table with all six `TodoModel` columns (including `UpdatedAt`, per the schema decision above) — not an incremental migration per field. Applied automatically at startup via `dbContext.Database.Migrate()`, per [`overview.md#migrations`](../../architecture/backend/overview.md#migrations).

Connection string: `ConnectionStrings:TodoDb`, local dev value in `appsettings.Development.json` (relative-path SQLite file per [`overview.md#where-the-sqlite-file-lives`](../../architecture/backend/overview.md#where-the-sqlite-file-lives)).

`TodoDbContext` is registered via `builder.Services.AddDbContext<TodoDbContext>(...)`, which defaults to **scoped** lifetime — one instance per HTTP request, matching `ITodoRepository`'s own scoped registration. No lifetime override needed; called out explicitly here since nothing else in this spec states it.

### Concurrent writes

Not a concern for this spec's scope: a single demo deployment, single SQLite file, and no requirement to handle concurrent multi-client writes at scale (`requirements-qa-internal.md` Q20, never promoted to a question sent to Foci — treated as a documented assumption instead). SQLite's own file-level locking serializes concurrent writers by default (a second `INSERT` blocks briefly rather than corrupting data or silently failing), so `AddTodo` needs no additional handling — EF Core's default `SaveChangesAsync` behavior is sufficient. Revisit only if a later requirement calls for real concurrent-client load.

## Shared Infrastructure: `ValidationFilter<TRequest>`

Referenced by the endpoint below (`.AddEndpointFilter<ValidationFilter<Request>>()`) but not yet defined anywhere in the architecture docs — this spec is the first to need it, so it's specified here. **Not feature-specific**; lives in `TodoApi.Todos/Endpoints/ValidationFilter.cs` alongside `IEndpoint` for now (only consumer today), same "don't split into a shared project until a second feature needs it" reasoning used for persistence and the CQRS generic interfaces elsewhere in this repo.

An `IEndpointFilter` that resolves the registered `AbstractValidator<TRequest>` from DI, runs it against the bound request, and short-circuits to a `400 ValidationProblem` on failure — so individual `Handle` methods never write validation-calling boilerplate themselves.

```csharp
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().Single();
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
            return TypedResults.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
```

Requires `builder.Services.AddValidatorsFromAssemblyContaining<AddTodoEndpoint.RequestValidator>()` (already in the [Wiring Checklist](#wiring-checklist)) so `IValidator<AddTodoEndpoint.Request>` resolves from DI.

## Endpoint

`TodoApi.Todos/Endpoints/AddTodoEndpoint.cs`, shape per [`overview.md#endpoint-pattern`](../../architecture/backend/overview.md#endpoint-pattern):

```csharp
public class AddTodoEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/", Handle)
            .WithName("AddTodo")
            .WithSummary("Add a new to-do item")
            .AddEndpointFilter<ValidationFilter<Request>>();

    public sealed record Request(string Title, string? Description, DateTime? DueDate);

    public sealed record Response(
        Guid Id,
        string Title,
        string? Description,
        DateTime? DueDate,
        bool IsCompleted,
        DateTime CreatedAt);

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

    private static async Task<Created<Response>> Handle(
        [FromBody] Request request,
        [FromServices] IAddTodoCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var model = request.ToModel();
        var result = await handler.HandleAsync(new AddTodoCommand(model), cancellationToken);
        var response = result.Todo.ToResponse();
        return TypedResults.Created($"/todos/{response.Id}", response);
    }
}
```

Registered in `TodoApi.Gateway/Endpoints.cs` under the existing `/todos` group (`.MapEndpoint<AddTodoEndpoint>()`).

## CORS

Not previously addressed anywhere in the architecture docs, and this is the first endpoint a browser client can actually call — needs resolving now rather than discovered as a broken fetch later. The React SPA (Vite dev server) runs on a different origin than the API in local dev (different port at minimum), so without an explicit CORS policy the browser blocks the response to `POST /todos` even though the server processes it fine.

`ConfigureServices` registers a named CORS policy (e.g. `"Frontend"`) allowing the Vite dev server's origin (`https://localhost:5173` or whatever the frontend spec settles on), all methods this API uses, and any headers/credentials the frontend actually needs (none beyond `Content-Type: application/json` expected for `AddTodo`). `ConfigureApps` calls `app.UseCors("Frontend")` in the pipeline, before endpoint mapping. Production/demo origin is a separate, environment-specific value — not decided in this spec (no frontend deployment story finalized yet); local dev is the immediate need this spec unblocks.

## Wiring Checklist

- [ ] NuGet packages on `TodoApi.Todos.csproj`: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design` (for migrations tooling), `FluentValidation.AspNetCore`. No mapping library — `TodoMapping` is hand-written.
- [ ] `TodoApi.Todos/Extensions/ServiceCollectionExtensions.AddTodosServices()`: register `ITodoRepository → TodoRepository`, `IAddTodoCommandHandler → AddTodoCommandHandler`, `TodoDbContext` (via `AddDbContext`, connection string from config), validators (`AddValidatorsFromAssemblyContaining<AddTodoEndpoint.RequestValidator>()`).
- [ ] `TodoApi.Gateway/ConfigureServices.cs`: calls `builder.Services.AddTodosServices()`.
- [ ] `TodoApi.Gateway/ConfigureApps.cs`: applies pending migrations at startup (`dbContext.Database.Migrate()`).
- [ ] `TodoApi.Gateway/Endpoints.cs`: maps `AddTodoEndpoint` under `/todos`.
- [ ] `appsettings.Development.json`: `ConnectionStrings:TodoDb` set to a local SQLite file path.
- [ ] `TodoApi.Gateway/ConfigureServices.cs` + `ConfigureApps.cs`: register and apply the `"Frontend"` CORS policy (see [CORS](#cors)).
- [ ] After implementing, run a full `dotnet build` and confirm `TodoApi.Gateway/openapi.json` regenerates to include `POST /todos` and gets committed — this is the first endpoint to exercise that build-time generation step end-to-end (see [`../../architecture/overview-architecture.md#api-contract--client-generation`](../../architecture/overview-architecture.md#api-contract--client-generation)), so it's worth confirming it actually works rather than assuming.

## Tests

Per-layer, following the split named as an open item in [`overview.md#resolved-formerly-open-questions--todo`](../../architecture/backend/overview.md#resolved-formerly-open-questions--todo) — this spec is the first to actually write that strategy down in practice:

- **`TodoApi.Todos.Tests`**
  - `RequestValidator`: empty/whitespace `Title` fails; `Title`/`Description` over their max length fail; a past `DueDate` fails, today/future/`null` `DueDate` passes; valid `Title` with/without optional fields passes.
  - `AddTodoCommandHandler`: calls `ITodoRepository.AddAsync` with the given model, returns it wrapped in `AddTodoCommandResult` — repository mocked via **Moq** (see [Mocking](#mocking) below).
  - `TodoMapping`: `ToModel()` produces a non-empty `Id`, `IsCompleted == false`, `CreatedAt` populated, `UpdatedAt == null`; `ToResponse()` maps all fields straight through and omits `UpdatedAt`.
  - `ValidationFilter<TRequest>`: invalid request short-circuits to `TypedResults.ValidationProblem` without calling `next`; valid request calls `next` and returns its result unchanged.
- **`TodoApi.Gateway.Tests`**
  - Integration test through the full pipeline (`WebApplicationFactory`, SQLite in-memory mode with `ITodoRepository` replaced by a **Moq** mock — see [Mocking](#mocking)): `POST /todos` with a valid body returns `201` with a `Location` header and a body matching the request; missing `Title` returns `400` with a `ValidationProblemDetails` body naming `Title`; a valid request calls `ITodoRepository.AddAsync` exactly once with a matching model, an invalid one calls it never.
  - `DueDate` serialization round-trip: a request with `dueDate` sent as a bare date (no `Z`) echoes back without a `Z`; a request with `dueDate` sent with an explicit `Z`/offset echoes back with one — confirms the `Kind`-dependent behavior noted in [`TodoModel`](#todomodel-domain-model) rather than assuming it. Still valid under a mocked repository since this is a JSON-serialization concern, not a persistence one.

### Mocking

**Moq**, added once List's spec needed a second handler test and the hand-written `FakeTodoRepository` pattern this spec originally used started costing a stub per repository method a given test never touched. Decision and full rationale (including the trade-off of no longer exercising real EF Core/SQLite round-trips in `TodoApi.Gateway.Tests`) recorded in [`../list-todos/spec.md#mocking`](../list-todos/spec.md#mocking) rather than duplicated here, since that is where the switch actually happened.

`TodoApiWebApplicationFactory` exposes a `Repository` (`Mock<ITodoRepository>`) that tests configure per-instance; the factory itself is now created per test class instance (`IDisposable`, not `IClassFixture`) so each test gets an isolated mock with no shared state to reset between tests.

## Open Questions

- ~~**Integration test DB strategy**~~ — Resolved: `TodoApi.Gateway.Tests` uses SQLite's in-memory mode (`Data Source=:memory:` with a kept-open connection per test class), real `TodoDbContext`/`TodoRepository`/migrations, nothing mocked below the HTTP boundary. See the rationale and the noted trade-off (revisit if the endpoint surface grows enough that full-stack setup per test class gets expensive) in the doc comment on `TodoApiWebApplicationFactory`.

## Related Docs

- [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) — endpoint pattern, mapping, persistence, bootstrapping
- [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md) — CQRS pattern this command follows
- [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) — coding standards applied throughout
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source data model requirement (`title`/`description`/`dueDate`/`isCompleted`/`createdAt`)
