# Feature Spec: Update Completion Status (`PATCH /todos/{id}`)

Sixth feature endpoint, and the one [`../update-todo/spec.md`](../update-todo/spec.md#open-questions) and [`../delete-todo/spec.md`](../delete-todo/spec.md#scope) both flagged as still outstanding. This is the requirements' **Complete** and **Incomplete** operations — "mark a specific to-do item as completed by its ID" / "mark a specific to-do item as not completed by its ID" — collapsed into a single endpoint rather than two, per the decision in [Scope](#scope) below. `requirements.md`'s own API sketch names this possibility directly: "Potentially `PATCH` or dedicated actions for complete/incomplete." Follows the patterns in [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) and [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md); coding standards from [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) apply throughout. Reuses the route-parameter binding, `{id:guid}` constraint, and `404` contract settled by [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md), and — per [`../update-todo/spec.md#open-questions`](../update-todo/spec.md#open-questions)'s path (b) — dispatches through `UpdateTodoCommand`/`UpdateTodoCommandHandler` directly rather than introducing a parallel write path.

## Scope

In scope: the `UpdateCompletionStatus` vertical slice end-to-end — endpoint, DTO, mapping into an existing `UpdateTodoEndpoint.Request`, DI wiring (none new — no new command/handler/repository method), tests.

**One endpoint, not two.** `requirements.md` lists Complete and Incomplete as two separate capabilities, and an earlier reading of that (recorded in [`../update-todo/spec.md#full-replacement-not-partial-service-tier-fields`](../update-todo/spec.md#full-replacement-not-partial-service-tier-fields)) assumed they'd land as two endpoints. **Decided instead: a single `PATCH /todos/{id}` whose request body carries the target `isCompleted` value**, not two routes (`POST /todos/{id}/complete` + `POST /todos/{id}/incomplete`) or two verbs. The requirement is "set completion status to X," and `IsCompleted` is a `bool` — a single endpoint taking the desired boolean covers both cases with one code path instead of two near-identical ones differing only in a hardcoded `true`/`false`. This also matches the family resemblance to `UpdateTodo`: both are "change one or more fields of an existing todo," just with a smaller field set here. If a real need for the two-dedicated-routes shape appears later (e.g. an audit trail that needs "completed" and "reopened" as distinct verbs, not just a boolean flip), that's a different spec — nothing here forecloses adding `POST /todos/{id}/complete` alongside this endpoint.

Out of scope, deliberately:

- **`PUT /todos/{id}`** — unchanged, its own spec. This endpoint does not replace it; a client that wants to change `Title`/`Description`/`DueDate` still uses `PUT`.
- **A new CQRS command/handler.** Per [CQRS](#cqrs), this endpoint reuses `UpdateTodoCommand`/`UpdateTodoCommandHandler` outright — no `UpdateCompletionStatusCommand` is introduced.
- **A new repository method.** `GetTrackedByIdAsync`/`UpdateAsync` (already added by [`../update-todo/spec.md#repository`](../update-todo/spec.md#repository)) are reused unchanged; see [Open Questions](#open-questions) for why `GetTrackedByIdAsync` still isn't generalized despite this being its third consumer.
- Any change to `TodoModel`'s shape — no new fields, no migration.
- **A `GET` before the `PATCH`.** Per [Mapping](#mapping-todomapping), this spec fetches the existing todo once, inside the handler that already runs via `UpdateTodoCommandHandler` — not once in this endpoint to build a full `UpdateTodoEndpoint.Request` and again inside the handler.

## `TodoModel` (domain model)

Unchanged by this feature. See [`../add-todo/spec.md`](../add-todo/spec.md#todomodel-domain-model) for the full field table.

## Field naming: `IsCompleted`, not `IsComplete`

The request body's field is spelled `isCompleted`, matching the name already locked in everywhere else in this feature — `TodoModel.IsCompleted`, `UpdateTodoEndpoint.Request.IsCompleted`, `TodoResponse.IsCompleted` (see [`../add-todo/spec.md#todomodel-domain-model`](../add-todo/spec.md#todomodel-domain-model) and [`../update-todo/spec.md#dto-contract`](../update-todo/spec.md#dto-contract)). A new endpoint spelling the same underlying field differently (`isComplete`) would put two names on one boolean across the API surface for no reason — same "don't introduce an inconsistency without a real need" principle applied elsewhere in this feature (e.g. [`../update-todo/spec.md#repository`](../update-todo/spec.md#repository) rejecting a same-method-two-behaviors shortcut).

## Route: `PATCH /todos/{id}`, not `PATCH /todo/{id}`

The route is plural, `/todos/{id}` — matching the existing `MapGroup("/todos")` every other endpoint in this feature is registered under (`GET /todos`, `GET /todos/{id}`, `PUT /todos/{id}`, `DELETE /todos/{id}`), not a new singular `/todo/{id}` prefix. Introducing a second, singular route group for one endpoint would split the API's URL surface for no functional reason and contradicts `requirements.md`'s "use standard HTTP methods and status codes" guidance, which is generally read (throughout this feature's specs) as "be a conventional, consistent REST API." This endpoint registers under the same `todos` group as the rest.

## DTO Contract

### `PATCH /todos/{id}`

**Request:** `id` is a route parameter, bound as `Guid`, same `{id:guid}` constraint as [`../get-todo-by-id/spec.md#endpoint`](../get-todo-by-id/spec.md#endpoint).

```json
{
  "isCompleted": true
}
```

- `isCompleted` — required `bool`. No optional/default — the whole point of this endpoint is to set this one field explicitly; there's no sensible "omit it" case the way there is for `Description`/`DueDate` on `PUT`.

**Success response — `200 OK`:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Buy milk and eggs",
  "description": "2% or whole, whichever is on sale",
  "dueDate": "2026-09-26T00:00:00Z",
  "isCompleted": true,
  "createdAt": "2026-09-19T14:32:07.1234567Z",
  "updatedAt": "2026-09-19T16:41:53.4455667Z"
}
```

The shared `TodoResponse` DTO from [`../get-todo-by-id/spec.md#shared-todoresponse-dto`](../get-todo-by-id/spec.md#shared-todoresponse-dto), reused directly — same as `PUT`'s `200`. **`200`, not `204`** — unlike `DeleteTodo` (see [`../delete-todo/spec.md#dto-contract`](../delete-todo/spec.md#dto-contract)), there's a server-computed value worth confirming here (`updatedAt`), and the pattern this endpoint is layered on (`PUT /todos/{id}`) already returns `200` with the full body for exactly that reason — see [`../update-todo/spec.md#dto-contract`](../update-todo/spec.md#dto-contract).

**Not found — `404 Not Found`:**

Identical contract to [`../get-todo-by-id/spec.md#dto-contract`](../get-todo-by-id/spec.md#dto-contract) — same `TypedResults.NotFound()`, same RFC 9457 shape, same rationale.

**Validation failure — `400 Bad Request`:**

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "IsCompleted": ["'Is Completed' must not be empty."]
  }
}
```

In practice this case is close to unreachable for a non-nullable `bool` bound from JSON — a request body omitting `isCompleted` entirely deserializes it to `false` (the CLR default), not a validation failure, and a non-boolean JSON value (`"isCompleted": "yes"`) fails model binding before the validator runs at all, surfacing as ASP.NET Core's own binding-failure `400`, not `RequestValidator`'s. The `RequestValidator` class and its `ValidationFilter<Request>` wiring are still included (see [Validation Rules](#validation-rules-service-tier--requestvalidator)) purely for consistency with every other mutating endpoint in this feature, not because a `bool` genuinely needs a `NotEmpty()`-style rule to reject much.

**Idempotency.** Calling `PATCH /todos/{id}` twice with the same `isCompleted` value returns `200` both times, with the second call's `updatedAt` still refreshed to the new current time — same "full replacement of the fields this endpoint owns" semantics `PUT` already established, just scoped to one field. This is a deliberate, narrow reading of idempotency identical to [`../delete-todo/spec.md#dto-contract`](../delete-todo/spec.md#dto-contract)'s: the *intended effect* (the todo's completion status is X) is the same on the first and second call, even though `updatedAt` visibly changes — HTTP idempotency (RFC 9110 §9.2.2) is about intended effect on server state, not response-body stability.

## Validation Rules (service tier — `RequestValidator`)

```csharp
public sealed class RequestValidator : AbstractValidator<Request>
{
    public RequestValidator()
    {
        RuleFor(x => x.IsCompleted).NotNull();
    }
}
```

**`NotNull()`, not a business rule.** There's no domain constraint being enforced here — `isCompleted` is a required field of the request, full stop. FluentValidation's `NotNull()` guards the case where the request is bound from a body that's technically valid JSON but sets the field to a JSON `null` against a nullable-annotated type; see [Endpoint](#endpoint) for why `Request.IsCompleted` is declared as the non-nullable `bool` rather than `bool?`, which makes even this rule mostly a formality kept for consistency with the rest of the feature's per-endpoint `RequestValidator` convention (see [`../update-todo/spec.md#validation-rules-service-tier--requestvalidator`](../update-todo/spec.md#validation-rules-service-tier--requestvalidator)).

No separate validation for "is this todo already in this state" — per [Idempotency](#idempotency), re-applying the same value is a legitimate, successful call, not an error.

## Mapping (`TodoMapping`)

**No new mapping method.** This endpoint's `Handle` builds an `UpdateTodoEndpoint.Request` from the existing todo's current `Title`/`Description`/`DueDate` plus this request's `IsCompleted`, then dispatches through the existing `UpdateTodoCommand` — so the existing `ApplyTo(UpdateTodoEndpoint.Request, TodoModel)` (see [`../update-todo/spec.md#mapping-todomapping`](../update-todo/spec.md#mapping-todomapping)) runs unmodified and unaware it was reached from a `PATCH` rather than a `PUT`. This is path (b) from [`../update-todo/spec.md#open-questions`](../update-todo/spec.md#open-questions) ("Complete/Incomplete's endpoints construct an `UpdateTodoEndpoint.Request` internally ... and dispatch through `UpdateTodoCommand` directly, reusing the handler outright"), chosen over path (a) (a second command/handler duplicating the fetch-check-save shape) for the reason that spec's Open Questions section already gave: it avoids duplicating `UpdateTodoCommandHandler`'s existence-check-then-save logic, at the cost of one extra read this endpoint's own contract wouldn't otherwise need — see [CQRS](#cqrs) for why that extra read is unavoidable regardless of which path was chosen.

## CQRS

No new command. `UpdateCompletionStatusEndpoint.Handle` reuses `UpdateTodoCommand`/`IUpdateTodoCommandHandler`/`UpdateTodoCommandHandler` exactly as [`../update-todo/spec.md#cqrs`](../update-todo/spec.md#cqrs) defined them — no changes to that file.

```csharp
private static async Task<Results<Ok<TodoResponse>, NotFound>> Handle(
    Guid id,
    [FromBody] Request request,
    [FromServices] ITodoRepository repository,
    [FromServices] IUpdateTodoCommandHandler handler,
    CancellationToken cancellationToken)
{
    var existing = await repository.GetByIdAsync(id, cancellationToken);

    if (existing is null)
    {
        return TypedResults.NotFound();
    }

    var updateRequest = new UpdateTodoEndpoint.Request(
        existing.Title,
        existing.Description,
        existing.DueDate,
        request.IsCompleted);

    var result = await handler.HandleAsync(new UpdateTodoCommand(id, updateRequest), cancellationToken);

    return TypedResults.Ok(result.Todo!.ToResponse());
}
```

**Reads the todo twice under the hood — once here (`AsNoTracking()` `GetByIdAsync`) to build the `UpdateTodoEndpoint.Request`, once inside `UpdateTodoCommandHandler` (`GetTrackedByIdAsync`) to actually apply and save it.** This is the extra read [Mapping](#mapping-todomapping) flagged as path (b)'s cost. It's accepted rather than engineered away because the alternative — teaching `UpdateTodoCommandHandler` to accept a partial "only these fields changed" request, or adding a second command that duplicates its existence-check-then-save shape — either reopens the full/partial update question [`../update-todo/spec.md#full-replacement-not-partial-service-tier-fields`](../update-todo/spec.md#full-replacement-not-partial-service-tier-fields) already closed, or duplicates logic for one call site. Two reads of a `SQLite` row by primary key is not a cost worth restructuring the command layer over.

**This endpoint's own existence check (`existing is null` → `404`) is technically redundant with `UpdateTodoCommandHandler`'s** (`GetTrackedByIdAsync` returning `null` → `UpdateTodoCommandResult(null)`) — both would independently produce `404` for the same nonexistent `id`. It's kept anyway because without it, `existing.Title`/`existing.Description`/`existing.DueDate` on a `null` `existing` would throw a `NullReferenceException` before the command is even constructed — the read has to happen here regardless (to source the three fields this endpoint doesn't take from the client), so the null check on its result is required, not optional defensive coding. Once that check exists, `result.Todo!` inside the success path is safe: `existing` being non-null at this point means the same row still existed a moment ago, and nothing in this endpoint's flow deletes rows concurrently within a single request.

## Repository

No changes. `GetByIdAsync` and `GetTrackedByIdAsync` (both already on `ITodoRepository` per [`../update-todo/spec.md#repository`](../update-todo/spec.md#repository) and [`../delete-todo/spec.md#repository`](../delete-todo/spec.md#repository)) are used as-is — the first by this endpoint directly, the second transitively via `UpdateTodoCommandHandler`.

## Endpoint

`TodoApi.Todos/Endpoints/UpdateCompletionStatusEndpoint.cs`, shape per [`overview.md#endpoint-pattern`](../../architecture/backend/overview.md#endpoint-pattern):

```csharp
public sealed class UpdateCompletionStatusEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch("/{id:guid}", Handle)
            .WithName("UpdateCompletionStatus")
            .WithSummary("Mark a to-do item as completed or not completed")
            .AddEndpointFilter<ValidationFilter<Request>>();

    public sealed record Request(bool IsCompleted);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.IsCompleted).NotNull();
        }
    }

    private static async Task<Results<Ok<TodoResponse>, NotFound>> Handle(
        Guid id,
        [FromBody] Request request,
        [FromServices] ITodoRepository repository,
        [FromServices] IUpdateTodoCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);

        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        var updateRequest = new UpdateTodoEndpoint.Request(
            existing.Title,
            existing.Description,
            existing.DueDate,
            request.IsCompleted);

        var result = await handler.HandleAsync(new UpdateTodoCommand(id, updateRequest), cancellationToken);

        return TypedResults.Ok(result.Todo!.ToResponse());
    }
}
```

Notes:

- **`{id:guid}` route constraint**, same as every other id-keyed endpoint in this feature.
- **`Results<Ok<TodoResponse>, NotFound>` return type**, same pattern as `GetTodoByIdEndpoint`/`UpdateTodoEndpoint` for accurate `200`/`404` schemas in `openapi.json`. No `400` arm in the type union — same reasoning [`../get-todo-by-id/spec.md#endpoint`](../get-todo-by-id/spec.md#endpoint) and [`../update-todo/spec.md#endpoint`](../update-todo/spec.md#endpoint) already used: `ValidationFilter<Request>` short-circuits with its own `400` before `Handle` runs, so `Handle`'s own return type only needs to describe the outcomes it can actually produce.
- **Depends on both `ITodoRepository` and `IUpdateTodoCommandHandler`** — the first endpoint in the feature to take two collaborators directly rather than routing everything through a single command handler. This is a direct consequence of [CQRS](#cqrs)'s path (b) choice: the endpoint needs the existing todo's current field values before it can even construct the command it dispatches.
- **`app.MapPatch`**, ASP.NET Core's Minimal API verb-mapping method for `PATCH`, same family as the `MapPut`/`MapDelete`/`MapGet` used elsewhere in this feature.
- **Registers under the existing `/todos` group**, not a new one — see [Route](#route-patch-todosid-not-patch-todoid).

Registered in `TodoApi.Gateway/Endpoints.cs` under the existing `/todos` group (`.MapEndpoint<UpdateCompletionStatusEndpoint>()`).

## Wiring Checklist

- [ ] `TodoApi.Todos/Endpoints/UpdateCompletionStatusEndpoint.cs`: new file — endpoint, `Request`, `RequestValidator`, `Handle` (see [Endpoint](#endpoint)).
- [ ] `TodoApi.Gateway/Endpoints.cs`: map `UpdateCompletionStatusEndpoint` under the existing `/todos` group.
- [ ] Run a full `dotnet build` and confirm `TodoApi.Gateway/openapi.json` regenerates to include `PATCH /todos/{id}` with its `200`/`400`/`404` schemas, then commit the regenerated spec.

**Explicitly not needed** — stated so nobody goes looking: no new CQRS command/handler (see [CQRS](#cqrs)), no new repository method (see [Repository](#repository)), no new `TodoMapping` method (see [Mapping](#mapping-todomapping)), no EF Core migration (no schema change), no new `Dtos/` file (reuses the existing `TodoResponse`), no new DI registration in `ServiceCollectionExtensions.AddTodosServices()` (`IUpdateTodoCommandHandler` is already registered by [`../update-todo/spec.md#wiring-checklist`](../update-todo/spec.md#wiring-checklist); `RequestValidator` is picked up automatically by the existing `AddValidatorsFromAssemblyContaining<AddTodoEndpoint.RequestValidator>()` scan), no new NuGet package, no `appsettings` change, no CORS change (the existing `"Frontend"` policy already covers `PATCH`).

## Tests

Per-layer, following the same split as [`../update-todo/spec.md`](../update-todo/spec.md#tests) and [`../delete-todo/spec.md`](../delete-todo/spec.md#tests).

**`TodoApi.Todos.Tests`**

- `RequestValidator`: a request with `IsCompleted` set (either `true` or `false`) passes — there isn't a meaningful failing case for a non-nullable `bool`; the test exists to document that the validator is a no-op guard, not to pin a real rejection rule.
- No new command handler tests — `UpdateTodoCommandHandlerTests` (from [`../update-todo/spec.md#tests`](../update-todo/spec.md#tests)) already covers `UpdateTodoCommandHandler` fully; this endpoint doesn't change its behavior.

**`TodoApi.Gateway.Tests`**

Integration tests through the full pipeline via `TodoApiWebApplicationFactory`, with `ITodoRepository` replaced by a **Moq** mock (already wired per [`../list-todos/spec.md#mocking`](../list-todos/spec.md#mocking)):

- `PATCH /todos/{id}` with the mock's `GetByIdAsync` returning an existing, incomplete model and `{"isCompleted": true}` returns `200` with `isCompleted: true` and a non-null `updatedAt` in the response.
- `PATCH /todos/{id}` with the mock's `GetByIdAsync` returning an existing, completed model and `{"isCompleted": false}` returns `200` with `isCompleted: false` — the "Incomplete" half of the requirement, exercised through the same endpoint as "Complete."
- `PATCH /todos/{id}` with the mock's `GetByIdAsync` returning an existing model asserts the response's `title`/`description`/`dueDate` are unchanged from the existing model — pinning that this endpoint touches only `isCompleted` (plus `updatedAt`), never the other three fields, distinguishing it from `PUT`.
- `PATCH /todos/{id}` with the mock's `GetByIdAsync` returning `null` returns `404`, and neither `GetTrackedByIdAsync` nor `UpdateAsync` is called on the mock.
- `PATCH /todos/not-a-guid` (malformed id) returns `404` via the route constraint, same as every other id-keyed endpoint's equivalent case.
- A valid request against an existing id calls `ITodoRepository.UpdateAsync` exactly once, with a model whose `IsCompleted` matches the request body.

## Open Questions

- **`GetTrackedByIdAsync` still not generalized, despite a third consumer.** [`../update-todo/spec.md#open-questions`](../update-todo/spec.md#open-questions) and [`../delete-todo/spec.md#open-questions`](../delete-todo/spec.md#open-questions) both flagged this as worth reconsidering once a third tracked-fetch consumer showed up; this endpoint is that third consumer (transitively, via `UpdateTodoCommandHandler`, not directly — this endpoint's own direct call is to the untracked `GetByIdAsync`). Still left as-is: this spec doesn't add a new direct call to `GetTrackedByIdAsync`, so the count of *direct* call sites hasn't actually changed, only the number of code paths that indirectly exercise it. Genuinely revisit only if a future spec adds a fourth direct caller.
- **The double-read in `Handle`.** Flagged in [CQRS](#cqrs) as an accepted cost of path (b). If a future profiling pass finds this endpoint under real load-bearing traffic (unlikely for a take-home-scoped app), the fix would be a `TryUpdateFieldsAsync`-style repository/handler addition that takes a mutation delegate — not something to speculatively build now.

## Related Docs

- [`../update-todo/spec.md`](../update-todo/spec.md) — the `UpdateTodoCommand`/`UpdateTodoCommandHandler` this endpoint dispatches through, the full-replacement-vs-partial decision this endpoint deliberately doesn't reopen, and the Open Questions section that named this exact design choice as path (b)
- [`../delete-todo/spec.md`](../delete-todo/spec.md) — `GetTrackedByIdAsync`, and the idempotency framing (`RFC 9110 §9.2.2`, intended effect vs. response stability) this spec's [Idempotency](#idempotency) reuses
- [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md) — the route-parameter/`{id:guid}` pattern, `404` contract, and shared `TodoResponse` DTO this spec reuses directly
- [`../add-todo/spec.md`](../add-todo/spec.md) — the `TodoModel` schema and `IsCompleted` field this spec's request body targets
- [`../list-todos/spec.md`](../list-todos/spec.md) — the Moq-based mocking strategy this spec's tests follow
- [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) — endpoint pattern, mapping, repository shape, error response contract
- [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md) — the CQRS pattern this endpoint reuses rather than extends
- [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) — coding standards applied throughout
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source **Complete**/**Incomplete** requirements and the `PATCH`-for-complete/incomplete API-sketch note this spec implements
