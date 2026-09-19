# Feature Spec: Delete Todo (`DELETE /todos/{id}`)

Fifth feature endpoint, and the last of the CRUD verbs — **Complete**/**Incomplete** remain, but both are convenience wrappers over [`../update-todo/spec.md`](../update-todo/spec.md)'s write path per that spec's [Open Questions](../update-todo/spec.md#open-questions), not new CRUD surface. This is the requirements' **Delete** operation — "remove a to-do item by its ID." Follows the patterns in [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) and [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md); coding standards from [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) apply throughout. Reuses the route-parameter binding, `{id:guid}` constraint, and `404` contract settled by [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md), and the "existence check as a business rule" shape [`../update-todo/spec.md`](../update-todo/spec.md) introduced for CQRS commands. It's the first endpoint with **no response body on success** — the one case [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md#why-now-and-why-not-further) flagged as the reason `TodoResponse` shouldn't be assumed universal.

## Scope

In scope: the `DeleteTodo` vertical slice end-to-end — endpoint, CQRS command/handler, repository method, DI wiring, tests.

Out of scope, deliberately:

- **Complete / Incomplete** — their own specs, layered on `UpdateTodo`'s write path per [`../update-todo/spec.md#full-replacement-not-partial-service-tier-fields`](../update-todo/spec.md#full-replacement-not-partial-service-tier-fields).
- Any change to `TodoModel`'s shape — no new fields, no migration.
- **Soft delete / undo.** `requirements.md` says "remove a to-do item by its ID," with no mention of recovery. This spec implements a hard delete (`DELETE FROM Todos WHERE Id = @id`, via EF Core's `Remove`). If a future requirement needs recoverable deletes, that's a `TodoModel` schema change (e.g. a `DeletedAt` column) and a different query/command shape throughout — not something this spec speculatively builds room for.

## `TodoModel` (domain model)

Unchanged by this feature. See [`../add-todo/spec.md`](../add-todo/spec.md#todomodel-domain-model) for the full field table.

## DTO Contract

### `DELETE /todos/{id}`

**Request:** no body. `id` is a route parameter, bound as `Guid`, same `{id:guid}` constraint as [`../get-todo-by-id/spec.md#endpoint`](../get-todo-by-id/spec.md#endpoint).

**Success response — `204 No Content`:**

No body. **Not `200` with the deleted todo echoed back** — unlike `UpdateTodo`'s `200` (justified there by the client wanting to confirm the server-computed `UpdatedAt`), a delete has no server-computed value worth confirming, and there is nothing left to fetch afterward that a body would help with. `204` is the standard REST convention for a successful request with intentionally empty content (RFC 9110 §15.3.5), and matches `requirements.md`'s own "use standard HTTP methods and status codes" guidance. This is the first endpoint in the feature whose success case is not `TodoResponse` — see [`../get-todo-by-id/spec.md#why-now-and-why-not-further`](../get-todo-by-id/spec.md#why-now-and-why-not-further), which anticipated exactly this case as the reason `TodoResponse` wasn't forced onto every future endpoint.

**Not found — `404 Not Found`:**

Identical contract to [`../get-todo-by-id/spec.md#dto-contract`](../get-todo-by-id/spec.md#dto-contract) — same `TypedResults.NotFound()`, same RFC 9457 shape, same rationale. Deleting an id that doesn't exist is a `404`, not a silent no-op `204` — the client asked to delete a specific resource, and if that resource isn't there, the server did not perform the delete it was asked to perform.

**No `400` case.** No request body, and `id` is validated by the `{id:guid}` route constraint before `Handle` ever runs — same reasoning as [`../get-todo-by-id/spec.md#dto-contract`](../get-todo-by-id/spec.md#dto-contract). No `Request` DTO, no `RequestValidator`, no `ValidationFilter<TRequest>` on this endpoint.

**Idempotency.** Calling `DELETE /todos/{id}` twice in a row returns `204` then `404` — not `204` both times. This is a deliberate, narrow reading of HTTP idempotency: RFC 9110 §9.2.2 defines idempotent as "the intended effect on the server of multiple identical requests is the same as for a single request" (the todo is gone either way), not "the response code must be identical on every call." The second call's *intended effect* (this todo should not exist) is still achieved — it already doesn't. Returning `404` on the second call is simply this spec's existing "id not found" contract applying uniformly, not a special case carved out for repeated deletes. An alternative (swallow "not found" into a `204` specifically for `DeleteTodo`, on the theory that a repeated delete should look like success) was considered and rejected: it would make `DeleteTodo` the one endpoint in the feature where "not found" doesn't mean `404`, an inconsistency with every other id-keyed endpoint (`GetTodoById`, `UpdateTodo`) for a distinction (first delete vs. redundant delete) the client already knows without the server needing to hide it.

## CQRS

`TodoApi.Todos/Commands/DeleteTodoCommand.cs`, per [`cqrs.md`](../../architecture/backend/cqrs.md#the-pattern) and matching the existence-check shape [`../update-todo/spec.md#cqrs`](../update-todo/spec.md#cqrs) established:

```csharp
namespace TodoApi.Todos.Commands;

public sealed record DeleteTodoCommand(Guid Id);

public sealed record DeleteTodoCommandResult(bool Deleted);

public interface IDeleteTodoCommandHandler : ICommandHandler<DeleteTodoCommand, DeleteTodoCommandResult>;

public sealed class DeleteTodoCommandHandler(ITodoRepository repository) : IDeleteTodoCommandHandler
{
    public async Task<DeleteTodoCommandResult> HandleAsync(
        DeleteTodoCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetTrackedByIdAsync(command.Id, cancellationToken);

        if (existing is null)
        {
            return new DeleteTodoCommandResult(Deleted: false);
        }

        await repository.DeleteAsync(existing, cancellationToken);
        return new DeleteTodoCommandResult(Deleted: true);
    }
}
```

**`DeleteTodoCommandResult` carries a `bool`, not a nullable `TodoModel`.** `UpdateTodoCommandResult.Todo` (see [`../update-todo/spec.md#cqrs`](../update-todo/spec.md#cqrs)) is a nullable `TodoModel` because `UpdateTodo`'s `200` response needs the updated todo's fields. `DeleteTodo`'s `204` response has no body (see [DTO Contract](#dto-contract)) — the endpoint only ever needs to know *whether* the delete happened, not any data about the deleted row. Carrying the full (about-to-be-gone) `TodoModel` through the result just to discard it at the endpoint would be dead weight; a `bool` states exactly what the caller needs and nothing more.

**Uses `GetTrackedByIdAsync`, not `GetByIdAsync`.** Same reasoning [`../update-todo/spec.md#repository`](../update-todo/spec.md#repository) already worked out for `UpdateTodoCommandHandler`: the fetched entity is about to be handed to a repository method that acts on it (there: mutate-then-`SaveChangesAsync`; here: `DbSet.Remove()`-then-`SaveChangesAsync`), and EF Core's change tracker needs to already know about the instance for that to work. `GetByIdAsync`'s `AsNoTracking()` would make `DeleteAsync`'s `Remove()` call operate on an untracked instance — see [Repository](#repository) for why that specifically breaks. This is the second consumer of `GetTrackedByIdAsync` that [`../update-todo/spec.md#open-questions`](../update-todo/spec.md#open-questions) anticipated ("if a later spec ... needs the same tracked-fetch pattern, this is the moment to reconsider") — see [Open Questions](#open-questions) below for why this spec still doesn't generalize it.

## Repository

Adds `DeleteAsync` to `ITodoRepository` and `TodoRepository`. `AddAsync`, `ListAsync`, `GetByIdAsync`, `GetTrackedByIdAsync`, `UpdateAsync` are unchanged.

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
```

```csharp
public sealed class TodoRepository(TodoDbContext dbContext) : ITodoRepository
{
    // AddAsync, ListAsync, GetByIdAsync, GetTrackedByIdAsync, UpdateAsync unchanged.

    public async Task DeleteAsync(TodoModel todo, CancellationToken cancellationToken)
    {
        dbContext.Todos.Remove(todo);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

**`dbContext.Todos.Remove(todo)` is required here**, unlike `UpdateAsync`'s bare `SaveChangesAsync()` (see [`../update-todo/spec.md#repository`](../update-todo/spec.md#repository)). `UpdateAsync` relies on the change tracker already having recorded in-place field mutations on a tracked instance — there's no analogous "detect this entity should be deleted" signal the tracker can infer on its own; an explicit `Remove()` (which marks the entity `EntityState.Deleted`) is what tells EF Core to emit a `DELETE` statement on the next `SaveChangesAsync()`. `todo` must be the same tracked instance `GetTrackedByIdAsync` returned (see [CQRS](#cqrs)) — calling `Remove()` on an `AsNoTracking()`-loaded instance throws `InvalidOperationException` (EF Core cannot attach-and-delete an entity it has no tracked identity for in one call without an explicit `Attach()` first), which is the concrete failure mode `GetTrackedByIdAsync` avoids.

**Returns `Task`, not `Task<TodoModel>`.** `AddAsync`/`UpdateAsync` return the persisted `TodoModel` because their callers need it (to map to a response). `DeleteAsync`'s caller only needs to know the operation completed — see [`DeleteTodoCommandResult` carries a `bool`](#cqrs) above for the same reasoning one layer up.

## Endpoint

`TodoApi.Todos/Endpoints/DeleteTodoEndpoint.cs`, shape per [`overview.md#endpoint-pattern`](../../architecture/backend/overview.md#endpoint-pattern):

```csharp
public sealed class DeleteTodoEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteTodo")
            .WithSummary("Delete a to-do item by id");

    private static async Task<Results<NoContent, NotFound>> Handle(
        Guid id,
        [FromServices] IDeleteTodoCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteTodoCommand(id), cancellationToken);

        if (!result.Deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
```

Notes:

- **`{id:guid}` route constraint**, same as `GetTodoByIdEndpoint`/`UpdateTodoEndpoint` — a malformed `id` never reaches `Handle`; see [`../get-todo-by-id/spec.md#endpoint`](../get-todo-by-id/spec.md#endpoint) for the full reasoning, unchanged here.
- **`Results<NoContent, NotFound>` return type** — the same typed-union pattern `GetTodoByIdEndpoint`/`UpdateTodoEndpoint` use for `Microsoft.AspNetCore.OpenApi` to generate accurate `204`/`404` schemas in `openapi.json`, just with `NoContent` in place of `Ok<TodoResponse>` since this endpoint has no response body on success.
- **No `Request` DTO, no `RequestValidator`, no `[AddEndpointFilter<ValidationFilter<Request>>]`.** Same absence-of-validation reasoning as `GetTodoByIdEndpoint` — see [DTO Contract](#dto-contract) above.
- **No `[FromBody]` parameter at all.** The first mutating (non-`GET`) endpoint in the feature with no request body — `AddTodo`/`UpdateTodo` both bind one. `Handle`'s only parameters are the route-bound `id`, the injected handler, and `CancellationToken`.

Registered in `TodoApi.Gateway/Endpoints.cs` under the existing `/todos` group (`.MapEndpoint<DeleteTodoEndpoint>()`).

## Wiring Checklist

- [ ] `TodoApi.Todos/Commands/DeleteTodoCommand.cs`: new file — command, `bool`-result, named interface, handler (see [CQRS](#cqrs)).
- [ ] `TodoApi.Todos/Persistence/ITodoRepository.cs` + `TodoRepository.cs`: add `DeleteAsync` (see [Repository](#repository)). `AddAsync`, `ListAsync`, `GetByIdAsync`, `GetTrackedByIdAsync`, `UpdateAsync` are unchanged.
- [ ] `TodoApi.Todos/Endpoints/DeleteTodoEndpoint.cs`: new file.
- [ ] `TodoApi.Todos/Extensions/ServiceCollectionExtensions.AddTodosServices()`: register `IDeleteTodoCommandHandler → DeleteTodoCommandHandler` (scoped, matching the existing handler registrations).
- [ ] `TodoApi.Gateway/Endpoints.cs`: map `DeleteTodoEndpoint` under the existing `/todos` group.
- [ ] Run a full `dotnet build` and confirm `TodoApi.Gateway/openapi.json` regenerates to include `DELETE /todos/{id}` with its `204`/`404` schemas, then commit the regenerated spec.

**Explicitly not needed** — stated so nobody goes looking: no EF Core migration (no schema change), no new `Dtos/` file (this endpoint has no response body), no new NuGet package, no `appsettings` change, no CORS change (the existing `"Frontend"` policy already covers this origin and `DELETE`), no `ValidationFilter<TRequest>` usage (no request body to validate).

## Tests

Per-layer, following the same split as [`../update-todo/spec.md`](../update-todo/spec.md#tests) and [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md#tests).

**`TodoApi.Todos.Tests`**

- `DeleteTodoCommandHandler`:
  - Repository's `GetTrackedByIdAsync` returning a model: handler calls `DeleteAsync` with that same instance, and returns a result with `Deleted == true`.
  - Repository's `GetTrackedByIdAsync` returning `null`: handler returns a result with `Deleted == false`, and `DeleteAsync` is **never called** — mirrors `UpdateTodoCommandHandler`'s "not found short-circuits before any write" test from [`../update-todo/spec.md#tests`](../update-todo/spec.md#tests).
  - Repository mocked via **Moq**, per [`../list-todos/spec.md#mocking`](../list-todos/spec.md#mocking).

**`TodoApi.Gateway.Tests`**

Integration tests through the full pipeline via `TodoApiWebApplicationFactory`, with `ITodoRepository` replaced by a **Moq** mock:

- `DELETE /todos/{id}` with the mock's `GetTrackedByIdAsync` returning a matching model returns `204` with an empty body, and `ITodoRepository.DeleteAsync` is called exactly once with that model.
- `DELETE /todos/{id}` with the mock's `GetTrackedByIdAsync` returning `null` returns `404` with a Problem Details body, mirroring `GetTodoByIdEndpointTests`/`UpdateTodoEndpointTests` — and `DeleteAsync` is **never called**.
- `DELETE /todos/not-a-guid` (malformed id) returns `404` via the route constraint, same as `GetTodoByIdEndpointTests`'/`UpdateTodoEndpointTests`' equivalent case.

## Open Questions

- **`GetTrackedByIdAsync` still not generalized, despite a second consumer.** [`../update-todo/spec.md#open-questions`](../update-todo/spec.md#open-questions) flagged a `GetByIdAsync(id, track: bool)` overload (or a shared private helper) as worth reconsidering once a second tracked-fetch consumer showed up; `DeleteTodoCommandHandler` is that second consumer. Left as two separate named methods anyway — the boolean-parameter footgun that spec rejected still applies, and two three-line methods is not yet enough duplication to outweigh the clarity of two distinctly-named calls. Complete/Incomplete ([`../update-todo/spec.md#open-questions`](../update-todo/spec.md#open-questions)) would make it a third consumer if/when those specs land through path (a) rather than (b); that's the point this should be revisited, not before.
- **Whether Complete/Incomplete's specs should link back here.** Nothing in this spec depends on Complete/Incomplete, but both remaining specs will share this feature's `{id:guid}`/`404` conventions same as this one does. Noted here only so whoever writes those specs cross-links appropriately; no action needed from this spec.

## Related Docs

- [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md) — the route-parameter/`{id:guid}` pattern, `404` contract, and RFC 9457 shape this spec reuses directly
- [`../update-todo/spec.md`](../update-todo/spec.md) — the existence-check CQRS shape, `GetTrackedByIdAsync` repository method, and tracked-vs-`AsNoTracking()` reasoning this spec builds on
- [`../add-todo/spec.md`](../add-todo/spec.md) — the `TodoModel` schema and test/mocking strategy this spec's tests follow
- [`../list-todos/spec.md`](../list-todos/spec.md) — the Moq-based mocking strategy this spec's tests follow
- [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) — endpoint pattern, repository shape, error response contract
- [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md) — the CQRS pattern this command follows
- [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) — coding standards applied throughout
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source **Delete** requirement
