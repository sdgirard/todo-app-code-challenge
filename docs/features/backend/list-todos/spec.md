# Feature Spec: List Todos (`GET /todos`)

Second feature endpoint, and a set of firsts for this codebase: the first **read** endpoint, the first **query** (as opposed to a command), and therefore the first consumer of the `IQueryHandler<,>` contract that [`cqrs.md`](../../architecture/backend/cqrs.md) sketches but no file defines yet. Follows the patterns in [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) and [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md); coding standards from [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) apply throughout. Builds directly on the `TodoModel` schema locked in by [`../add-todo/spec.md`](../add-todo/spec.md).

## Scope

In scope: the `ListTodos` vertical slice end-to-end — endpoint, CQRS query/handler, the shared `IQueryHandler<,>` contract, repository method, Model→DTO mapping, DI wiring, tests.

Out of scope, deliberately:

- **Filtering** (completed / incomplete / overdue) — listed as an optional enhancement in [`requirements.md`](../../requirements/requirements.md#optional-enhancements-not-required), deferred to a later task.
- **Sorting** (by due date, creation date, title) — same, deferred to a later task.
- **Default ordering** — deferred alongside sorting. See [Ordering](#ordering-known-limitation) below, which documents what this means in practice rather than leaving it implicit.
- **Pagination** — not required, and not worth pre-building for a single-user list of this size.
- View, Update, Complete, Incomplete, Delete — their own specs.

Keeping this slice to a plain, unparameterized list makes it the smallest possible increment that gets the SPA a working read path, and leaves the query-parameter surface to be designed once, in one place, rather than half-built here.

## `TodoModel` (domain model)

Unchanged by this feature. `ListTodos` only reads — no new fields, no migration, no schema change. See [`../add-todo/spec.md`](../add-todo/spec.md#todomodel-domain-model) for the full field table and the UTC timestamp convention.

Relevant detail carried over: `UpdatedAt` is nullable and null until something edits the todo. This endpoint is the first to actually surface it (see [DTO Contract](#dto-contract)).

## DTO Contract

### `GET /todos`

**Request:** no body, no query parameters, no route parameters. The entire request is the URL.

**Success response — `200 OK`:**

A bare JSON array, not an envelope object. Chosen because there is no pagination metadata or total count to carry alongside the items, and a bare array maps directly to `Todo[]` in the orval-generated TypeScript client (see [`overview-architecture.md#api-contract--client-generation`](../../architecture/overview-architecture.md#api-contract--client-generation)) with no unwrapping step in the SPA. If pagination is ever added, that is a deliberate, versioned contract change rather than something to pre-build for now.

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Buy milk",
    "description": "2% or whole, whichever is on sale",
    "dueDate": "2026-09-25T00:00:00Z",
    "isCompleted": false,
    "createdAt": "2026-09-19T14:32:07.1234567Z",
    "updatedAt": null
  },
  {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "title": "Renew passport",
    "description": null,
    "dueDate": null,
    "isCompleted": true,
    "createdAt": "2026-09-18T09:12:44.7654321Z",
    "updatedAt": "2026-09-19T11:05:02.3216549Z"
  }
]
```

Each item carries **all seven** `TodoModel` fields, including `UpdatedAt`. Two notes on that:

- This differs from `AddTodoEndpoint.Response`, which omits `UpdatedAt` — justified there because it is always null immediately after a create, so surfacing it adds noise without information. On a read endpoint it can be genuinely non-null, so it belongs in the contract. The first example item above (never edited) and the second (edited) show both states concretely.
- Returning the full todo rather than a trimmed summary (the requirements only mandate Title / Due Date / Completion Status for List) means the SPA can render the list *and* open a detail view without a second round-trip. For a list of this size the extra payload is immaterial.

**Empty list — `200 OK` with `[]`:**

An empty database returns `200` and an empty array, **not** `404`. The collection resource `/todos` exists and is simply empty; that is a successful request with a well-defined answer. `404` is reserved for a request naming a *specific* todo that does not exist — the View endpoint's concern, not this one. Worth stating explicitly because "no results" and "no such resource" are easy to conflate, and conflating them forces every client to special-case an error path for an ordinary empty state.

**No `400` case.** There is no client-supplied input to reject — no body, no parameters. This is the only endpoint in the feature with no validation failure mode.

## Ordering (known limitation)

The EF Core query in this spec has **no `OrderBy` clause**, so the order of items in the response is unspecified. SQLite will in practice usually return rows in rowid (insertion) order, but that is an implementation detail of the engine, not a guarantee — it is not part of this endpoint's contract and clients must not rely on it.

This is a conscious, documented gap rather than an oversight: ordering is deferred to the same follow-up task as filtering and sorting, so the whole query-parameter surface (`sortBy`, `sortDir`, and the filter predicates) gets designed together instead of having a default ordering baked in here and then reworked. Until then, the SPA can sort client-side if it needs a stable presentation order.

Two consequences worth carrying forward:

- **Tests must be order-independent.** Assert on set membership and count — "the response contains a todo with this id" — never on positional index (`response[0].Title`). A test that passes today on incidental insertion order would fail unpredictably the moment real ordering lands. See [Tests](#tests).
- **The follow-up task owns this.** When sorting is specced, this section should be replaced by the real ordering contract, not merely amended.

## CQRS

First query in the project, so this spec defines both the shared contract and the query itself.

### Shared contract: `IQueryHandler<TQuery, TResult>`

[`cqrs.md`](../../architecture/backend/cqrs.md#the-pattern) sketches this interface alongside `ICommandHandler<,>`, but no file defines it yet — commands were all that existed. This spec is its first consumer, so it specifies it, following the same precedent [`../add-todo/spec.md`](../add-todo/spec.md#shared-infrastructure-validationfiltertrequest) set when it defined `ValidationFilter<TRequest>` as the first endpoint that needed it.

**Location:** `TodoApi.Todos/Queries/IQueryHandler.cs`, mirroring the existing `TodoApi.Todos/Commands/ICommandHandler.cs`.

`cqrs.md` says only that the generic contracts share a home ("one per feature library"), without settling the folder. Two readings were available: keep both generics together under `Commands/` (where `ICommandHandler.cs` lives today), or give each its own folder next to the handlers that implement it. **Decided: each generic lives beside its own kind of handler** — `Commands/ICommandHandler.cs` and `Queries/IQueryHandler.cs`. Putting a file named `IQueryHandler` inside a folder named `Commands/` would be actively misleading, and the alternative (a third `Shared/` folder holding two small interfaces) is more structure than two files justify. No change to the existing `ICommandHandler.cs`.

```csharp
namespace TodoApi.Todos.Queries;

public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
```

Identical in shape to `ICommandHandler<,>` — deliberately. The two are separate types rather than one shared interface because the command/query distinction is the point of CQRS: the type a handler implements should say which side of the split it is on, even when the method signature happens to match.

### `ListTodosQuery`

`TodoApi.Todos/Queries/ListTodosQuery.cs` — new `Queries/` folder, per [`overview.md`](../../architecture/backend/overview.md#directory-structure)'s directory tree:

```csharp
namespace TodoApi.Todos.Queries;

public sealed record ListTodosQuery;

public sealed record ListTodosQueryResult(IReadOnlyList<TodoModel> Todos);

public interface IListTodosQueryHandler : IQueryHandler<ListTodosQuery, ListTodosQueryResult>;

public sealed class ListTodosQueryHandler(ITodoRepository repository) : IListTodosQueryHandler
{
    public async Task<ListTodosQueryResult> HandleAsync(
        ListTodosQuery query,
        CancellationToken cancellationToken)
    {
        var todos = await repository.ListAsync(cancellationToken);
        return new ListTodosQueryResult(todos);
    }
}
```

`ListTodosQuery` is a **parameterless record** — it carries no data today, since there is nothing to filter or sort by. It stays a record (rather than collapsing the parameter away entirely) precisely so the deferred filter/sort work can add properties to it without changing `HandleAsync`'s signature, the handler interface, or the endpoint's call shape.

No business rules apply — this is a straight read. Flagged explicitly for the same reason the AddTodo spec flagged its own absence of rules: so a reader can tell "no rules needed here" from "rules were forgotten."

## Repository

Adds `ListAsync` to `ITodoRepository` and `TodoRepository`, matching the signature already sketched in [`overview.md#repository-shape`](../../architecture/backend/overview.md#repository-shape). `AddAsync` is unchanged; the remaining operations still land with their own specs.

```csharp
public interface ITodoRepository
{
    Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoModel>> ListAsync(CancellationToken cancellationToken);
}
```

```csharp
public sealed class TodoRepository(TodoDbContext dbContext) : ITodoRepository
{
    // AddAsync unchanged.

    public async Task<IReadOnlyList<TodoModel>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Todos.AsNoTracking().ToListAsync(cancellationToken);
}
```

Two implementation notes:

- **`AsNoTracking()`** is correct here and should not be dropped. This is a read-only query whose results are mapped to DTOs and discarded — nothing will be mutated or saved, so EF Core's change tracker would be pure overhead (it would otherwise snapshot every returned entity to detect later modifications that never come).
- **`using Microsoft.EntityFrameworkCore;`** must be added to `TodoRepository.cs` — the file does not currently import it, because `AddAsync` only uses `DbSet.Add` and `SaveChangesAsync`, neither of which needs the extension-method namespace. `ToListAsync` does.

No ordering clause, per [Ordering](#ordering-known-limitation).

## Mapping (`TodoMapping`)

> **Superseded:** `ListTodosEndpoint.Response` and `ToListResponse()` as described below, along with `AddTodoEndpoint.Response`/`ToResponse()`, were consolidated into a single shared `TodoResponse` DTO and one `ToResponse()` mapping in [`../get-todo-by-id/spec.md#shared-todoresponse-dto`](../get-todo-by-id/spec.md#shared-todoresponse-dto) — the naming split below (driven by C#'s no-overload-on-return-type rule) stopped being necessary once all three endpoints' response shapes converged. Left as-written below for the historical record of the original decision.

Hand-written extension method, per [`overview.md#mapping-dto--model`](../../architecture/backend/overview.md#mapping-dto--model). Adds one method to the existing `TodoApi.Todos/TodoMapping.cs`; `ToModel()` and `ToResponse()` are untouched.

```csharp
public static ListTodosEndpoint.Response ToListResponse(this TodoModel model) => new(
    model.Id,
    model.Title,
    model.Description,
    model.DueDate,
    model.IsCompleted,
    model.CreatedAt,
    model.UpdatedAt);
```

**On the name.** The existing mapping is `ToResponse()`, returning `AddTodoEndpoint.Response`. The obvious instinct is to overload it for the List response type — but **C# does not permit overloading on return type alone**, and both methods would otherwise have the identical signature `(this TodoModel model)`. That is a compile error, not a style preference.

The convention adopted, therefore: **each Model→DTO mapping is named after the endpoint it serves** — `ToListResponse()` here, `ToGetByIdResponse()` when View lands, and so on. The existing `ToResponse()` keeps its name rather than being renamed to `ToAddResponse()` for symmetry, because renaming would churn `AddTodoEndpoint.cs`, `TodoMapping.cs`, and `TodoMappingTests.cs` — three files in an already-committed, working, tested slice — for a purely cosmetic gain. The slight asymmetry is documented here so it reads as a deliberate call rather than an oversight; folding `ToResponse()` into the convention is a reasonable cleanup to bundle into some later change that touches those files anyway.

`ToListResponse()` maps `UpdatedAt` straight through, where `ToResponse()` drops it — the contract difference described in [DTO Contract](#dto-contract).

## Endpoint

`TodoApi.Todos/Endpoints/ListTodosEndpoint.cs`, shape per [`overview.md#endpoint-pattern`](../../architecture/backend/overview.md#endpoint-pattern):

```csharp
public sealed class ListTodosEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/", Handle)
            .WithName("ListTodos")
            .WithSummary("List all to-do items");

    public sealed record Response(
        Guid Id,
        string Title,
        string? Description,
        DateTime? DueDate,
        bool IsCompleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    private static async Task<Ok<IReadOnlyList<Response>>> Handle(
        [FromServices] IListTodosQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListTodosQuery(), cancellationToken);
        var response = result.Todos.Select(todo => todo.ToListResponse()).ToList();
        return TypedResults.Ok<IReadOnlyList<Response>>(response);
    }
}
```

**No `Request` record, no `RequestValidator`, no `.AddEndpointFilter<ValidationFilter<Request>>()`.** There is no client-supplied input to validate. This is a deliberate absence rather than an omission — every other endpoint in the feature has a validator, so its absence here should be traceable to a reason. If filtering/sorting parameters land later, that is when this endpoint grows a `Request` and a validator (for `sortBy`/`sortDir` values), and that is the right time to add them.

The empty-list case needs no special handling: `Select(...).ToList()` over an empty collection yields an empty list, which serializes to `[]` — the behavior specified in [DTO Contract](#dto-contract) falls out of the ordinary path.

Registered in `TodoApi.Gateway/Endpoints.cs` under the existing `/todos` group (`.MapEndpoint<ListTodosEndpoint>()`).

## Wiring Checklist

- [ ] `TodoApi.Todos/Queries/IQueryHandler.cs`: new file, shared generic contract (see [CQRS](#shared-contract-iqueryhandlertquery-tresult)).
- [ ] `TodoApi.Todos/Queries/ListTodosQuery.cs`: new file — query, result, named interface, handler.
- [ ] `TodoApi.Todos/Persistence/ITodoRepository.cs` + `TodoRepository.cs`: add `ListAsync`, add the `Microsoft.EntityFrameworkCore` import.
- [ ] `TodoApi.Todos/TodoMapping.cs`: add `ToListResponse()`.
- [ ] `TodoApi.Todos/Endpoints/ListTodosEndpoint.cs`: new file.
- [ ] `TodoApi.Todos/Extensions/ServiceCollectionExtensions.AddTodosServices()`: register `IListTodosQueryHandler → ListTodosQueryHandler` (scoped, matching the existing handler registrations).
- [ ] `TodoApi.Gateway/Endpoints.cs`: map `ListTodosEndpoint` under the existing `/todos` group.
- [ ] `TodoApi.Todos.Tests.csproj` + `TodoApi.Gateway.Tests.csproj`: add the `Moq` package reference (see [Mocking](#mocking)).
- [ ] `TodoApi.Gateway.Tests/TodoApiWebApplicationFactory.cs`: replace `ITodoRepository`'s DI registration with a `Mock<ITodoRepository>`, exposed as `Repository`; switch its consuming test classes from `IClassFixture<T>` to per-instance construction (`IDisposable`) — see [Mocking](#mocking) for why.
- [ ] Run a full `dotnet build` and confirm `TodoApi.Gateway/openapi.json` regenerates to include `GET /todos` with its array response schema, then commit the regenerated spec.

**Explicitly not needed** — stated so nobody goes looking: no EF Core migration (no schema change), no `appsettings` change, no CORS change (the existing `"Frontend"` policy already covers this origin and `GET`).

## Tests

Per-layer, following the same split as [`../add-todo/spec.md`](../add-todo/spec.md#tests).

**`TodoApi.Todos.Tests`**

- `ListTodosQueryHandler`: returns every model the mocked repository yields, wrapped in `ListTodosQueryResult`; an empty repository yields an empty — **not null** — collection, since the endpoint's `[]` contract depends on it. Repository mocked via **Moq** — see [Mocking](#mocking).
- `TodoMapping.ToListResponse()`: maps all seven fields straight through, including a **non-null** `UpdatedAt` (the field `ToResponse()` drops — this is the assertion that pins the contract difference).

**`TodoApi.Gateway.Tests`**

Integration tests through the full pipeline via `TodoApiWebApplicationFactory`, with `ITodoRepository` replaced by a **Moq** mock (see [Mocking](#mocking)):

- `GET /todos` with the mock returning an empty collection returns `200` with `[]`.
- `GET /todos` with the mock returning a set of models returns `200` with those items present and their fields intact, including a non-null `UpdatedAt` for an edited one and a null `UpdatedAt` for one that's never been edited.

**Order-independent assertions, always.** Per [Ordering](#ordering-known-limitation), response order is not contractual. Assert that the response *contains* an item with a given id (`Assert.Contains(body, t => t.Id == expectedId)`); never index positionally. A positional assertion would pass today and then fail unpredictably once real ordering lands — the worst kind of test failure, because it looks like the feature broke when in fact the test was always wrong.

## Mocking

**Decided: Moq**, not hand-written fakes. `../add-todo/spec.md` originally used a nested `FakeTodoRepository : ITodoRepository` per test class. That broke the moment this spec added `ListAsync` to the interface — `AddTodoCommandHandlerTests`' fake didn't implement it, a compile error, requiring either a real implementation or a `throw new NotSupportedException()` stub for a method that test never exercises. With five more repository methods still to come (Update, Delete, GetById, …), every existing fake would grow a stub for every method it doesn't use, purely to keep compiling. Moq's `Mock<ITodoRepository>` only needs `Setup()` for the members a given test actually calls.

**Trade-off, stated plainly:** this is a real loss of safety on interface changes. A hand-written fake fails to *compile* when the interface gains a member, forcing a look at every fake; a `Mock<T>` compiles unchanged and simply returns `default`/throws `MockException` at runtime only if the untested path is hit. Accepted anyway — the maintenance cost of the fakes was real and growing, and it already surfaced once in this exact task.

**Scope: handler unit tests *and* the Gateway integration tests.** `ITodoRepository` is swapped for a mock inside `TodoApiWebApplicationFactory` itself (exposed as a public `Repository` property), so `TodoApi.Gateway.Tests` no longer touches SQLite at all — it exercises routing, model binding, validation, CQRS dispatch, and JSON serialization only.

This is a second real trade-off, not a free win: `AddTodo`'s original integration-test strategy (documented in `../add-todo/spec.md`'s now-resolved Open Question) deliberately kept the repository real specifically because it caught the `DueDate` `Kind`/offset serialization quirk end-to-end. That coverage goal is unaffected here (JSON serialization still runs for real), but the suite genuinely stops exercising real EF Core query translation and SQLite round-trip behavior — a change to `TodoRepository.ListAsync`'s LINQ, for instance, that produced wrong SQL would no longer be caught by this test suite. No repository-level integration coverage against a real provider exists yet to fill that gap; flagged as a real, accepted gap rather than an oversight, and worth a repository-focused integration test class if that risk ever needs closing.

**Mechanics:**

- `TodoApiWebApplicationFactory` still registers `TodoDbContext` against a kept-open SQLite in-memory connection, purely because startup applies pending migrations; nothing reads through it once `ITodoRepository` is replaced.
- `services.AddSingleton(Repository.Object)` replaces the DI registration after `RemoveAll<ITodoRepository>()`.
- The factory is now constructed **per test class instance** (`new TodoApiWebApplicationFactory()` in the constructor, `IDisposable`, not `IClassFixture<T>`) rather than shared across a class's tests. A shared mock instance reset between tests (`Repository.Reset()`) was tried first and is a race under xUnit's per-test construction — one test's `Reset()` can clear another's `Setup()` mid-run. Per-instance isolation removes the shared state entirely rather than trying to sequence around it.

## Open Questions

- **`IQueryHandler<,>` file location** — settled in this spec (`Queries/IQueryHandler.cs`, beside the handlers it serves; see [CQRS](#shared-contract-iqueryhandlertquery-tresult)). Worth a second look only if a later feature library needs the same contracts, at which point the "don't split into a shared project until there's a second consumer" reasoning used elsewhere in this repo would finally have its second consumer.
- **Ordering / filtering / sorting follow-up** — deferred by decision, not forgotten. Needs to be tracked as a real follow-up task so [Ordering](#ordering-known-limitation) does not quietly become permanent.

## Related Docs

- [`../add-todo/spec.md`](../add-todo/spec.md) — the `TodoModel` schema, test strategy, and `ValidationFilter` this spec builds on
- [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) — endpoint pattern, mapping, persistence, repository shape
- [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md) — the CQRS pattern this query follows
- [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) — coding standards applied throughout
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source List requirement, and the optional filter/sort enhancements deferred here
