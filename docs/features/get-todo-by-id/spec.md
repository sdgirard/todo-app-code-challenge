# Feature Spec: Get Todo By Id (`GET /todos/{id}`)

Third feature endpoint. This is the requirements' **View** operation — "show details of a specific to-do item by its ID." Follows the patterns in [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) and [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md); coding standards from [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) apply throughout. It's the first endpoint with a route parameter and the first with a genuine `404` outcome, so it's also where route-parameter binding and the RFC 9457 not-found shape get settled for the rest of the feature (Update, Complete, Incomplete, Delete all share both). Builds on `TodoModel` as locked in by [`../add-todo/spec.md`](../add-todo/spec.md) and the query pattern established by [`../list-todos/spec.md`](../list-todos/spec.md).

This spec also resolves an open question both of those specs left behind: `AddTodoEndpoint.Response`, `ListTodosEndpoint.Response`, and this feature's own response are all byte-for-byte identical, so rather than adding a fourth copy-pasted `Response` record and a fourth near-identical mapping method, this spec **consolidates all three into one shared `TodoResponse` DTO**. See [Shared `TodoResponse` DTO](#shared-todoresponse-dto).

## Scope

In scope: the `GetTodoById` vertical slice end-to-end — endpoint, CQRS query/handler, repository method, Model→DTO mapping, DI wiring, tests. Also in scope, as a deliberate refactor bundled into this feature rather than deferred again: consolidating `AddTodoEndpoint.Response`, `ListTodosEndpoint.Response`, and this endpoint's response into a single shared `TodoResponse` DTO, touching `AddTodoEndpoint.cs` and `ListTodosEndpoint.cs` as well as the new endpoint.

Out of scope, deliberately:

- Add, List, Update, Complete, Incomplete, Delete — their own specs.
- Any change to `TodoModel`'s shape — this is a pure read of an existing row, no new fields, no migration.

## `TodoModel` (domain model)

Unchanged by this feature. See [`../add-todo/spec.md`](../add-todo/spec.md#todomodel-domain-model) for the full field table and the UTC timestamp convention.

## DTO Contract

### `GET /todos/{id}`

**Request:** no body, no query parameters. `id` is a route parameter, bound as `Guid`.

**Success response — `200 OK`:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Buy milk",
  "description": "2% or whole, whichever is on sale",
  "dueDate": "2026-09-25T00:00:00Z",
  "isCompleted": false,
  "createdAt": "2026-09-19T14:32:07.1234567Z",
  "updatedAt": null
}
```

All seven `TodoModel` fields, including `UpdatedAt` — same reasoning as [`../list-todos/spec.md`](../list-todos/spec.md#dto-contract): this is a read endpoint, and `UpdatedAt` can be genuinely non-null by the time a client views a single todo, so it belongs in the contract. This is also, byte-for-byte, the same shape `POST /todos` and `GET /todos`'s items already return — see [Shared `TodoResponse` DTO](#shared-todoresponse-dto) below for why that's now a single type instead of three.

**Not found — `404 Not Found`:**

`aspnet-web-api-guidelines.md`'s own [Error Responses](../../standards/aspnet-web-api-guidelines.md#error-responses) example *is* this exact case — quoted here as the literal contract, not just a reference:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Todo with id '3fa85f64-5717-4562-b3fc-2c963f66afa6' was not found.",
  "instance": "/todos/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

Produced by `TypedResults.NotFound()` — see [Endpoint](#endpoint) for why the `detail`/`instance` fields shown above come from ASP.NET Core's default Problem Details generation rather than being hand-populated by the endpoint.

**No `400` case for a malformed `id`.** ASP.NET Core's route-parameter binding rejects a non-`Guid` `{id}` segment before the endpoint delegate ever runs, returning its own `400` automatically — there's nothing for this endpoint's own logic to validate. No `Request` DTO, no `RequestValidator`, same absence-of-validation reasoning [`../list-todos/spec.md`](../list-todos/spec.md#endpoint) used for `ListTodos`, for a different reason (there: no input at all; here: the one input is validated by model binding itself, before this endpoint's code runs).

## CQRS

### `GetTodoByIdQuery`

`TodoApi.Todos/Queries/GetTodoByIdQuery.cs`, following the shape [`../list-todos/spec.md`](../list-todos/spec.md#listtodosquery) established for `IQueryHandler<TQuery, TResult>` consumers — no new shared infrastructure needed here, `IQueryHandler<,>` already exists.

```csharp
namespace TodoApi.Todos.Queries;

public sealed record GetTodoByIdQuery(Guid Id);

public sealed record GetTodoByIdQueryResult(TodoModel? Todo);

public interface IGetTodoByIdQueryHandler : IQueryHandler<GetTodoByIdQuery, GetTodoByIdQueryResult>;

public sealed class GetTodoByIdQueryHandler(ITodoRepository repository) : IGetTodoByIdQueryHandler
{
    public async Task<GetTodoByIdQueryResult> HandleAsync(
        GetTodoByIdQuery query,
        CancellationToken cancellationToken)
    {
        var todo = await repository.GetByIdAsync(query.Id, cancellationToken);
        return new GetTodoByIdQueryResult(todo);
    }
}
```

**`GetTodoByIdQueryResult.Todo` is nullable, `Todo` in `ListTodosQueryResult` is not.** This is the one new shape decision in this spec: "not found" is communicated by a `null` `Todo` on the result, not by throwing or by the handler returning an `HttpResult`. The handler stays HTTP-agnostic (per [`overview.md`](../../architecture/backend/overview.md#layered-view-n-tier) — CQRS handlers have no knowledge of DTOs or HTTP), and it's the endpoint's job to translate `null` into `404`. This is the "does this id exist" business rule flagged as a placeholder in [`../add-todo/spec.md`](../add-todo/spec.md#cqrs) — the first CQRS handler in the feature to actually need one, even though the rule itself is trivial (check for `null`, nothing more).

An alternative considered: throw a `NotFoundException` from the handler, caught by middleware. Rejected — `overview.md`'s error-response contract already reserves `app.UseExceptionHandler()` for genuinely unexpected `500`-level failures, and a missing todo on a `GetTodoById` call is an expected, well-defined outcome, not an exceptional one. Using control flow (a nullable result) rather than control flow via exceptions for an expected case is also cheaper and keeps the handler's signature honest about what can happen.

## Repository

Adds `GetByIdAsync` to `ITodoRepository` and `TodoRepository`, matching the signature already sketched in [`overview.md#repository-shape`](../../architecture/backend/overview.md#repository-shape). `AddAsync` and `ListAsync` are unchanged; the remaining operations still land with their own specs.

```csharp
public interface ITodoRepository
{
    Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoModel>> ListAsync(CancellationToken cancellationToken);
    Task<TodoModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
```

```csharp
public sealed class TodoRepository(TodoDbContext dbContext) : ITodoRepository
{
    // AddAsync, ListAsync unchanged.

    public Task<TodoModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
}
```

**`AsNoTracking()`**, same reasoning as `ListAsync` in [`../list-todos/spec.md#repository`](../list-todos/spec.md#repository) — a read-only query whose result is mapped to a DTO and discarded, so change tracking is pure overhead. `overview.md`'s own sketch of `GetByIdAsync` omits `AsNoTracking()`; this spec adds it deliberately rather than following the sketch verbatim, for consistency with `ListAsync`'s already-committed reasoning.

**No `using Microsoft.EntityFrameworkCore;` addition needed** — unlike `ListTodos`, which had to add that import for `ToListAsync`, `TodoRepository.cs` already carries it from `ListAsync`'s `ToListAsync` call.

## Shared `TodoResponse` DTO

**Context.** `../add-todo/spec.md` and `../list-todos/spec.md` each defined their own nested `Response` record (`AddTodoEndpoint.Response`, `ListTodosEndpoint.Response`) because, at the time, `AddTodo`'s response omitted `UpdatedAt` while `List`'s included it — genuinely different shapes, so genuinely different types. That distinction no longer holds: a since-spec bug fix (`bug fix: Add UpdatedAt field to Todo model and responses`) added `UpdatedAt` to `AddTodoEndpoint.Response` too. As of this feature, all three endpoints' responses — Add, List, and this one — carry the identical seven fields. Continuing the "each endpoint gets its own nested `Response` + its own `To*Response()` mapping" pattern for a fourth, identically-shaped DTO would mean four copies of the same seven-field record and four copies of the same seven-line mapping body, which is exactly the redundancy `../list-todos/spec.md`'s naming-convention note and this spec's original Open Questions flagged as a "candidate cleanup for later." Later is now — decided here rather than deferred a second time, since the duplication is already real, not hypothetical.

**Decided: one shared `TodoResponse` DTO, one `ToResponse()` mapping, used by `AddTodoEndpoint`, `ListTodosEndpoint`, and `GetTodoByIdEndpoint`.**

**Location: `TodoApi.Todos/Dtos/TodoResponse.cs`.** New `Dtos/` folder, sibling to `Models/`, `Queries/`, `Commands/`, `Endpoints/` in [`overview.md`](../../architecture/backend/overview.md#directory-structure)'s directory tree. Not nested inside any one endpoint class (it no longer belongs to one) and not dropped at the `TodoApi.Todos/` root next to `TodoMapping.cs` (that root is reserved for cross-cutting files, not DTO types) — a dedicated `Dtos/` folder gives response/request DTOs the same first-class treatment `Models/` gives domain models, and leaves room for a shared `TodoRequest`-shape consolidation later if `Update`'s request ever converges with `Add`'s.

```csharp
namespace TodoApi.Todos.Dtos;

public sealed record TodoResponse(
    Guid Id,
    string Title,
    string? Description,
    DateTime? DueDate,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

**Endpoints reference `TodoResponse` directly, dropping their own nested `Response` records:**

- `AddTodoEndpoint`: `Handle` returns `Task<Created<TodoResponse>>`. `Request` is untouched (it's still Add-specific and has never been shared).
- `ListTodosEndpoint`: `Handle` returns `Task<Ok<IReadOnlyList<TodoResponse>>>`.
- `GetTodoByIdEndpoint`: `Handle` returns `Task<Results<Ok<TodoResponse>, NotFound>>` — see [Endpoint](#endpoint) below.

**Mapping (`TodoMapping`)**, per [`overview.md#mapping-dto--model`](../../architecture/backend/overview.md#mapping-dto--model): the three existing methods (`ToResponse()`, `ToListResponse()`, and this spec's planned `ToGetByIdResponse()`) collapse into **one** `ToResponse()`, since C#'s overload-on-return-type restriction — the entire reason the three were named differently in the first place — no longer applies once there's only one return type.

```csharp
public static TodoResponse ToResponse(this TodoModel model) => new(
    model.Id,
    model.Title,
    model.Description,
    model.DueDate,
    model.IsCompleted,
    model.CreatedAt,
    model.UpdatedAt);
```

`ToModel()` (the `AddTodoEndpoint.Request → TodoModel` direction) is untouched — this consolidation is about the three Model→DTO response mappings only, not the request side.

**Why now, and why not further.** This is scoped strictly to the three response types that are provably identical today, not a preemptive "one DTO to rule them all" for every future endpoint. `UpdateTodoEndpoint`, `CompleteTodoEndpoint`, `IncompleteTodoEndpoint`, and `DeleteTodoEndpoint` are all still unspecified — if their responses turn out to match `TodoResponse` too, they should reuse it for the same reason this spec does; if any of them has a genuinely different shape (e.g. `DeleteTodo` returning `204 No Content` with no body), it gets its own type rather than being forced to fit. Consolidating three real, currently-identical DTOs is refactoring toward what's already true; consolidating against four not-yet-written specs would be designing for a hypothetical.

## Endpoint

`TodoApi.Todos/Endpoints/GetTodoByIdEndpoint.cs`, shape per [`overview.md#endpoint-pattern`](../../architecture/backend/overview.md#endpoint-pattern):

```csharp
public sealed class GetTodoByIdEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/{id:guid}", Handle)
            .WithName("GetTodoById")
            .WithSummary("Get a single to-do item by id");

    private static async Task<Results<Ok<TodoResponse>, NotFound>> Handle(
        Guid id,
        [FromServices] IGetTodoByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTodoByIdQuery(id), cancellationToken);

        if (result.Todo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(result.Todo.ToResponse());
    }
}
```

No nested `Response` record — this endpoint returns the shared `TodoResponse` from [Shared `TodoResponse` DTO](#shared-todoresponse-dto), and maps to it via the single, consolidated `ToResponse()`.

Notes:

- **`{id:guid}` route constraint.** Constrains the segment to a valid `Guid` at routing time, so a non-`Guid` `{id}` (e.g. `/todos/not-a-guid`) doesn't even match this route/falls through to `404` via routing rather than reaching `Handle` with a bindable-but-wrong value. Combined with the plain `Guid id` parameter (no `[FromRoute]` needed — Minimal APIs infer route binding from a matching route-template parameter name), this is what makes the "no `400` case" claim in [DTO Contract](#dto-contract) true: an unparseable id never reaches this endpoint's own code path at all.
- **`Results<Ok<TodoResponse>, NotFound>` return type.** The first endpoint in the feature with more than one possible success/outcome shape — `AddTodoEndpoint` returns `Created<TodoResponse>`, `ListTodosEndpoint` returns `Ok<IReadOnlyList<TodoResponse>>`, neither has a second outcome. `Results<,>` is ASP.NET Core's typed-union return type for exactly this case, and it's required (not just stylistic) for `Microsoft.AspNetCore.OpenApi` to generate accurate `200`/`404` response schemas in `openapi.json` for this operation — returning a shared base type like `IResult` would erase that information from the generated spec.
- **No `ValidationFilter`, no `RequestValidator`.** Same reasoning as `ListTodosEndpoint` — see [DTO Contract](#dto-contract) above for why, specific to this endpoint, that holds.

Registered in `TodoApi.Gateway/Endpoints.cs` under the existing `/todos` group (`.MapEndpoint<GetTodoByIdEndpoint>()`).

## Wiring Checklist

- [ ] `TodoApi.Todos/Dtos/TodoResponse.cs`: new file, new `Dtos/` folder — the shared response DTO (see [Shared `TodoResponse` DTO](#shared-todoresponse-dto)).
- [ ] `TodoApi.Todos/TodoMapping.cs`: replace `ToResponse()` and `ToListResponse()` with a single `ToResponse()` returning `TodoResponse`. `ToModel()` unchanged.
- [ ] `TodoApi.Todos/Endpoints/AddTodoEndpoint.cs`: remove the nested `Response` record; `Handle` returns `Task<Created<TodoResponse>>`.
- [ ] `TodoApi.Todos/Endpoints/ListTodosEndpoint.cs`: remove the nested `Response` record; `Handle` returns `Task<Ok<IReadOnlyList<TodoResponse>>>`.
- [ ] `TodoApi.Todos/Queries/GetTodoByIdQuery.cs`: new file — query, result, named interface, handler.
- [ ] `TodoApi.Todos/Persistence/ITodoRepository.cs` + `TodoRepository.cs`: add `GetByIdAsync`.
- [ ] `TodoApi.Todos/Endpoints/GetTodoByIdEndpoint.cs`: new file, uses `TodoResponse` directly (no nested `Response` record).
- [ ] `TodoApi.Todos/Extensions/ServiceCollectionExtensions.AddTodosServices()`: register `IGetTodoByIdQueryHandler → GetTodoByIdQueryHandler` (scoped, matching the existing handler registrations).
- [ ] `TodoApi.Gateway/Endpoints.cs`: map `GetTodoByIdEndpoint` under the existing `/todos` group.
- [ ] `TodoApi.Todos.Tests/TodoMappingTests.cs`: update to test the single consolidated `ToResponse()` instead of `ToResponse()`/`ToListResponse()` separately.
- [ ] `TodoApi.Gateway.Tests/AddTodoEndpointTests.cs` + `ListTodosEndpointTests.cs`: update any reference to the now-removed `AddTodoEndpoint.Response`/`ListTodosEndpoint.Response` types to `TodoResponse`.
- [ ] Run a full `dotnet build` and confirm `TodoApi.Gateway/openapi.json` regenerates — `GET /todos/{id}`'s new `200`/`404` schemas, and `POST /todos`/`GET /todos`'s existing schemas now referencing the same `TodoResponse` component instead of two separately-named schemas — then commit the regenerated spec.

**Explicitly not needed** — stated so nobody goes looking: no EF Core migration (no schema change), no new NuGet package (`Results<,>` and route constraints are built into `Microsoft.AspNetCore.Http.HttpResults`/routing, already in use), no `appsettings` change, no CORS change, no new test-project package references (Moq is already wired per [`../add-todo/spec.md#mocking`](../add-todo/spec.md#mocking)).

## Tests

Per-layer, following the same split as [`../add-todo/spec.md`](../add-todo/spec.md#tests) and [`../list-todos/spec.md`](../list-todos/spec.md#tests).

**`TodoApi.Todos.Tests`**

- `GetTodoByIdQueryHandler`: repository returning a model yields it wrapped in `GetTodoByIdQueryResult`; repository returning `null` yields a result whose `Todo` is `null` (**not** a thrown exception) — this is the assertion that pins the "null means not found, not an error" contract. Repository mocked via **Moq**, same pattern as `ListTodosQueryHandlerTests`.
- `TodoMapping.ToResponse()` (the consolidated single mapping, see [Shared `TodoResponse` DTO](#shared-todoresponse-dto)): maps all seven fields straight through, including a **non-null** `UpdatedAt`. This replaces the separate `ToResponse()`/`ToListResponse()` test cases `../add-todo/spec.md` and `../list-todos/spec.md` originally called for — one method, one test, covering all three call sites.

**`TodoApi.Gateway.Tests`**

Integration tests through the full pipeline via `TodoApiWebApplicationFactory`, with `ITodoRepository` replaced by a **Moq** mock (already wired per [`../list-todos/spec.md#mocking`](../list-todos/spec.md#mocking), no changes needed to the factory itself):

- `GET /todos/{id}` with the mock returning a matching model returns `200` with that todo's fields intact, including both a non-null and (in a separate case) a null `UpdatedAt`.
- `GET /todos/{id}` with the mock returning `null` (id not found) returns `404` with a Problem Details body — assert `status == 404` at minimum; asserting the exact `detail` string is optional and skippable if it turns out to be ASP.NET Core boilerplate text rather than anything this endpoint controls.
- `GET /todos/not-a-guid` (malformed id) returns `404` — the route-constraint case, confirming `{id:guid}` rejects non-`Guid` segments before `Handle` runs rather than the endpoint needing to handle it. (`404`, not `400`, because an unmatched route falls through to ASP.NET Core's default not-found behavior — no `RequestValidator` exists here to produce a `400`; see the route-constraint note under [Endpoint](#endpoint).)
- A matching request calls `ITodoRepository.GetByIdAsync` exactly once with the requested id.

## Open Questions

- **Exact `detail`/`instance` string ownership.** `TypedResults.NotFound()` alone does not populate `detail` — the literal example quoted in [DTO Contract](#dto-contract) comes from `aspnet-web-api-guidelines.md`'s own illustration, not a guarantee about what `TypedResults.NotFound()` emits by default. Confirm during implementation whether the default ASP.NET Core Problem Details output includes a populated `detail`/`instance` for a plain `NotFound()`, or whether matching that exact example would require `TypedResults.Problem(statusCode: 404, detail: ..., instance: ...)` instead. If the plain form is visibly thinner, that's fine — the guideline's own text treats `TypedResults.NotFound()` as sufficient ("Endpoints return `TypedResults.NotFound()` ... these already produce RFC 9457-shaped bodies") — but worth verifying rather than assuming the quoted example is emitted verbatim.
- ~~**Mapping duplication across `ToResponse()` / `ToListResponse()` / `ToGetByIdResponse()`.**~~ — Resolved in this spec: consolidated into one shared `TodoResponse` DTO and one `ToResponse()` mapping, used by `AddTodoEndpoint`, `ListTodosEndpoint`, and `GetTodoByIdEndpoint`. See [Shared `TodoResponse` DTO](#shared-todoresponse-dto). Originally flagged in `../list-todos/spec.md`'s naming-convention note and repeated in this spec's own first draft as a "candidate cleanup for whenever Update lands" — addressed now instead, since the duplication was already real across three endpoints rather than hypothetical.
- **Whether `Update`/`Complete`/`Incomplete`/`Delete` reuse `TodoResponse`.** Left open by design — see the "Why now, and why not further" note in [Shared `TodoResponse` DTO](#shared-todoresponse-dto). Each of those specs should reuse `TodoResponse` if its response shape matches, or introduce its own type if it genuinely doesn't (e.g. a `204 No Content` delete).

## Related Docs

- [`../add-todo/spec.md`](../add-todo/spec.md) — the `TodoModel` schema and test/mocking strategy this spec builds on
- [`../list-todos/spec.md`](../list-todos/spec.md) — the query pattern, `IQueryHandler<,>` contract, and Moq-based mocking this spec reuses directly
- [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) — endpoint pattern, mapping, repository shape (`GetByIdAsync` sketch), error response contract
- [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md) — the CQRS pattern this query follows
- [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) — coding standards and the RFC 9457 error-response example this spec's `404` contract quotes directly
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source **View** requirement
