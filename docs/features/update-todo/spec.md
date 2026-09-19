# Feature Spec: Update Todo (`PUT /todos/{id}`)

Fourth feature endpoint. This is the requirements' **Update** operation — "modify the title, description, or due date of an existing item by its ID." Follows the patterns in [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) and [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md); coding standards from [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) apply throughout. Reuses the route-parameter binding, `{id:guid}` constraint, and `404` contract settled by [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md), the `TodoModel` schema locked in by [`../add-todo/spec.md`](../add-todo/spec.md), and the shared `TodoResponse` DTO also settled there. It's the first endpoint to combine a route parameter *and* a request body, and the first CQRS **command** whose handler has a real business rule to enforce ("does this id exist") rather than the placeholder `AddTodoCommandHandler` flagged.

## Scope

In scope: the `UpdateTodo` vertical slice end-to-end — endpoint, service-tier validation, DTO⇄Model mapping, CQRS command/handler, repository method, DI wiring, tests.

Out of scope, deliberately:

- **Complete / Incomplete** — their own specs. They give a client a smaller, single-purpose way to flip `IsCompleted` without resending `Title`/`Description`/`DueDate`; `PUT /todos/{id}` itself does accept and apply `IsCompleted`, per [Full replacement, not partial](#full-replacement-not-partial-service-tier-fields).
- **Delete** — its own spec.
- Any change to `TodoModel`'s shape — no new fields, no migration.
- **Partial (`PATCH`-style) updates** — see [Full replacement, not partial](#full-replacement-not-partial-service-tier-fields) for why `PUT`'s full-replacement semantics were chosen over a `PATCH` alternative.

## `TodoModel` (domain model)

Unchanged by this feature. See [`../add-todo/spec.md`](../add-todo/spec.md#todomodel-domain-model) for the full field table and the UTC timestamp convention. This spec is the first to actually set `UpdatedAt` to a non-null value — every prior spec only read or passed through the field `AddTodo` left `null`.

## Full replacement, not partial (service-tier fields)

`requirements-qa-internal.md` Q8 asked whether Update should support partial (`PATCH`-style) updates or full replacement of `title`/`description`/`dueDate`; never promoted to a question sent to Foci — treated as a documented assumption instead, same disposition as the concurrent-writes question in [`../add-todo/spec.md#concurrent-writes`](../add-todo/spec.md#concurrent-writes).

**Decided: full replacement of the whole resource, per standard `PUT` semantics** — matches the verb `requirements.md`'s own API sketch names for this operation (`PUT /todos/{id}`, not `PATCH`), and matches strict `PUT` semantics (RFC 9110 §9.3.4: the request body is the complete new representation of the resource). The client sends `Title`, `Description`, `DueDate`, **and `IsCompleted`** every time; the server replaces all four unconditionally. A field omitted from the request body is not "left unchanged" — `Description: null`/absent means "clear the description," not "don't touch it." This is the conventional `PUT` contract and avoids the ambiguity a `PATCH`-style partial update would introduce (e.g., "is an absent `description` a no-op or a clear?") without a `JsonPatch` document or a wrapper type to disambiguate "not sent" from "sent as null." If a real partial-update use case appears later, that is a `PATCH /todos/{id}` addition, not a change to this endpoint's contract.

**`IsCompleted` is included in the request/response, not excluded.** An earlier draft of this spec kept `IsCompleted` out of `PUT`'s body, reasoning that the requirements name Complete/Incomplete as their own distinct operations and letting `UpdateTodo` also flip completion status would give two endpoints overlapping write access to the same field. That reasoning is **rejected**: excluding a real field of the resource from a `PUT` body is not full replacement, it's a partial update wearing a `PUT` verb — and "the request body is the complete representation of the resource" is the entire point of choosing `PUT` over `PATCH` here in the first place. **Decided instead: `PUT /todos/{id}` is a true full-resource replacement covering all four client-writable fields, and Complete/Incomplete (their own specs) become convenience endpoints layered on top of the same underlying write path** — each is equivalent to a client doing a `GetTodoById` read, flipping `isCompleted`, and re-`PUT`ting the rest unchanged, just without the round-trip. Their specs should define their `CQRS` commands as thin wrappers that end up reusing `UpdateTodoCommandHandler`'s existence-check-then-save shape (see [CQRS](#cqrs)) rather than a fully separate write path — the precise mechanism (share the handler outright vs. share only the repository method) is left to those specs, flagged in [Open Questions](#open-questions).

**`CreatedAt` and `UpdatedAt` are server-controlled, not client-controlled — they are excluded from the request body entirely, on the same basis `AddTodo`'s spec already established for `CreatedAt` at create time.** [`../add-todo/spec.md#todomodel-domain-model`](../add-todo/spec.md#todomodel-domain-model) sets `CreatedAt` at the mapping layer, not from client input, precisely because a creation timestamp is a fact the server asserts, not data the client supplies — the same reasoning applies to `UpdatedAt` here: *when* an update happened is a fact the server observes at the moment it processes the request, not something a client should be trusted to declare (a client could otherwise backdate/misreport it, and there's no legitimate case for a `PUT` to claim "this edit happened at some other time"). So `UpdateTodoEndpoint.Request` never has `CreatedAt`/`UpdatedAt` fields at all, matching `AddTodoEndpoint.Request`'s equivalent omission of `CreatedAt`. `ApplyTo()` (see [Mapping](#mapping-todomapping)) is where `UpdatedAt` actually gets its value — `DateTime.UtcNow`, unconditionally, on every successful update — and `CreatedAt` is left untouched on the loaded entity. Both fields **do** still appear in the `200` response (see [DTO Contract](#dto-contract)) via the shared `TodoResponse` DTO, same as every other endpoint that returns a todo — this is a request-side exclusion only, not a response-side one.

**`Id` is likewise excluded from the request body and never modified** — immutable once set, same as every other spec in this feature has assumed without stating it; this is the first spec where that assumption is load-bearing enough to write down.

## DTO Contract

### `PUT /todos/{id}`

**Request:** `id` is a route parameter, bound as `Guid`, same `{id:guid}` constraint as [`../get-todo-by-id/spec.md#endpoint`](../get-todo-by-id/spec.md#endpoint).

```json
{
  "title": "Buy milk and eggs",
  "description": "2% or whole, whichever is on sale",
  "dueDate": "2026-09-26T00:00:00Z",
  "isCompleted": false
}
```

- `title` — required, non-empty string. Same rules as `AddTodo`'s `Title` — see [Validation Rules](#validation-rules-service-tier--requestvalidator).
- `description` — optional string; omit or `null` clears it.
- `dueDate` — optional; same `DateTime?` binding as `AddTodo` — see [`../add-todo/spec.md#dto-contract`](../add-todo/spec.md#dto-contract) for the ISO 8601 parsing/`Kind` details, unchanged here.
- `isCompleted` — required `bool`. Not optional/defaultable — per [Full replacement, not partial](#full-replacement-not-partial-service-tier-fields), this is a true full-resource `PUT`, so the client states the completion status explicitly on every call rather than it being silently preserved. A client that only means to edit title/description/due-date must echo back the value it last read.

**Success response — `200 OK`:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Buy milk and eggs",
  "description": "2% or whole, whichever is on sale",
  "dueDate": "2026-09-26T00:00:00Z",
  "isCompleted": false,
  "createdAt": "2026-09-19T14:32:07.1234567Z",
  "updatedAt": "2026-09-19T16:05:11.9876543Z"
}
```

`isCompleted` in the response reflects whatever the request body sent — this endpoint applies it unconditionally, same as `Title`/`Description`/`DueDate`.

The shared `TodoResponse` DTO from [`../get-todo-by-id/spec.md#shared-todoresponse-dto`](../get-todo-by-id/spec.md#shared-todoresponse-dto), reused directly — this response is byte-for-byte the same shape as Add/List/GetById, just with a freshly non-null `UpdatedAt`. **`200`, not `201`** — the resource already existed; nothing was created. Body is the full updated todo (not `204 No Content`) so the client can confirm the server-computed `UpdatedAt` without a follow-up `GET`.

**Not found — `404 Not Found`:**

Identical contract to [`../get-todo-by-id/spec.md#dto-contract`](../get-todo-by-id/spec.md#dto-contract) — same `TypedResults.NotFound()`, same RFC 9457 shape, same rationale. An id that doesn't exist is a `404`, not a `400`; there is nothing wrong with the request body itself in that case.

**Validation failure — `400 Bad Request`:**

Same `ValidationProblemDetails` shape as [`../add-todo/spec.md#dto-contract`](../add-todo/spec.md#dto-contract):

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Title": ["'Title' must not be empty."]
  }
}
```

**Validation runs before the existence check.** A request with both an empty `Title` and a nonexistent `id` returns `400`, not `404` — `ValidationFilter<TRequest>` (see [`../add-todo/spec.md#shared-infrastructure-validationfiltertrequest`](../add-todo/spec.md#shared-infrastructure-validationfiltertrequest)) short-circuits before `Handle` ever runs, so the handler's existence check never gets a chance to execute. This ordering is a direct consequence of reusing the existing filter as-is rather than a new decision made for this endpoint, but it's worth stating explicitly since it's the first time this feature has two independent failure modes (`400` vs `404`) on the same request.

## Validation Rules (service tier — `RequestValidator`)

Identical rules to `AddTodoEndpoint.RequestValidator` (see [`../add-todo/spec.md#validation-rules-service-tier--requestvalidator`](../add-todo/spec.md#validation-rules-service-tier--requestvalidator)) — same field set, same constraints, same rationale for each bound:

- `Title`: `NotEmpty()`, `MaximumLength(200)`.
- `Description`: `MaximumLength(2000)` when present.
- `DueDate`: `GreaterThanOrEqualTo(DateTime.UtcNow.Date)` when present, same "not in the past" rule as create.

**`DueDate` "not in the past" applies at update time too**, evaluated against the current clock when the `PUT` is processed — not against the todo's original `CreatedAt`. Consistent with treating this as a full replacement: the server has no notion of "this due date was already in the past and that's fine because it was set before today," since nothing on the request or the existing row distinguishes an intentionally-past due date from a stale one. If a real need to move a due date into the past emerges (e.g. backfilling), that is a deliberate contract change, not something this spec's validator special-cases.

No separate `RequestValidator` type is introduced — `UpdateTodoEndpoint.RequestValidator` is its own class (same one-validator-per-endpoint convention as `AddTodoEndpoint`), just with rules identical in content to Add's. Not shared/extracted into a common base validator: two validators with matching bodies is not yet enough duplication to justify the indirection, per the same "don't abstract until a second real need shows up" reasoning used elsewhere in this repo (e.g. [`../add-todo/spec.md#shared-infrastructure-validationfiltertrequest`](../add-todo/spec.md#shared-infrastructure-validationfiltertrequest) waiting for a second consumer before generalizing).

## Mapping (`TodoMapping`)

Adds one method to the existing `TodoApi.Todos/TodoMapping.cs`. `ToModel()` (Add's request-to-model mapping) and `ToResponse()` (the shared response mapping) are untouched.

```csharp
public static void ApplyTo(this UpdateTodoEndpoint.Request request, TodoModel model)
{
    model.Title = request.Title;
    model.Description = request.Description;
    model.DueDate = request.DueDate;
    model.IsCompleted = request.IsCompleted;
    model.UpdatedAt = DateTime.UtcNow;
}
```

**Mutates the existing `TodoModel` in place rather than constructing a new one**, unlike `ToModel()` which builds a fresh `TodoModel` from an `AddTodoEndpoint.Request`. Deliberately different shape (`ApplyTo(model)` instead of `ToModel(): TodoModel`) because Update's mapping needs the *existing* row's `Id` and `CreatedAt` preserved — values the request body doesn't carry and Add's `ToModel()` has no equivalent need for (it always starts from nothing). Building a new `TodoModel` here would mean copying those two untouched fields across by hand, which is more error-prone than mutating the loaded entity directly, and EF Core's change tracker (the loaded `TodoModel` is tracked — see [Repository](#repository)) already expects in-place mutation as the normal way to record an update.

**`IsCompleted` is overwritten unconditionally, same as the other three client-writable fields** — see [Full replacement, not partial](#full-replacement-not-partial-service-tier-fields) for why this endpoint owns it rather than reserving it for Complete/Incomplete alone.

`UpdatedAt = DateTime.UtcNow` is set here, in the mapping method, not in the repository or the CQRS handler — consistent with `ToModel()` setting `CreatedAt`/`Id` at the same layer for `AddTodo`. Keeps "what does this request produce/change on a `TodoModel`" answerable from `TodoMapping` alone.

## CQRS

`TodoApi.Todos/Commands/UpdateTodoCommand.cs`, per [`cqrs.md`](../../architecture/backend/cqrs.md#the-pattern):

```csharp
namespace TodoApi.Todos.Commands;

public sealed record UpdateTodoCommand(Guid Id, UpdateTodoEndpoint.Request Request);

public sealed record UpdateTodoCommandResult(TodoModel? Todo);

public interface IUpdateTodoCommandHandler : ICommandHandler<UpdateTodoCommand, UpdateTodoCommandResult>;

public sealed class UpdateTodoCommandHandler(ITodoRepository repository) : IUpdateTodoCommandHandler
{
    public async Task<UpdateTodoCommandResult> HandleAsync(
        UpdateTodoCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (existing is null)
        {
            return new UpdateTodoCommandResult(null);
        }

        command.Request.ApplyTo(existing);
        var saved = await repository.UpdateAsync(existing, cancellationToken);
        return new UpdateTodoCommandResult(saved);
    }
}
```

**`UpdateTodoCommandResult.Todo` is nullable — the first command result in the feature to carry the same "null means not found" contract `GetTodoByIdQueryResult` established for queries** (see [`../get-todo-by-id/spec.md#cqrs`](../get-todo-by-id/spec.md#cqrs)). This is the business rule `../add-todo/spec.md`'s `AddTodoCommandHandler` flagged as a placeholder ("Complete needs 'does this id exist' as a rule the handler enforces via a 404-worthy repository result") — Update is the first command to actually need it, not Complete as that note guessed, since Update's spec landed first. Same reasoning as the query case: the handler stays HTTP-agnostic and returns an honest "did this work" signal; translating `null` into `404` is the endpoint's job.

**The command carries the raw `UpdateTodoEndpoint.Request`, not a pre-mapped `TodoModel`**, unlike `AddTodoCommand(TodoModel Todo)`. This is a deliberate difference, not an inconsistency: `AddTodo`'s mapping (`ToModel()`) needs no existing state, so the endpoint can map before constructing the command. `UpdateTodo`'s mapping (`ApplyTo()`) needs the *existing* `TodoModel`, which only the handler has (after `GetByIdAsync`) — so the request DTO travels through the command, and mapping happens inside the handler, after the existence check, not in the endpoint. Passing the DTO into the CQRS layer bends the "handlers never see DTOs" framing in `overview.md`'s layered diagram slightly — recorded here as a deliberate, scoped exception (the alternative, a two-step "fetch in the endpoint, map, then pass a whole `TodoModel` into the command" splits the existence check across two layers and leaves the endpoint racing the handler for who owns the fetch), not a precedent for every future command to take DTOs freely.

## Repository

Adds `UpdateAsync` to `ITodoRepository` and `TodoRepository`, matching the shape already sketched in [`overview.md#repository-shape`](../../architecture/backend/overview.md#repository-shape). `AddAsync`, `ListAsync`, `GetByIdAsync` are unchanged.

```csharp
public interface ITodoRepository
{
    Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoModel>> ListAsync(CancellationToken cancellationToken);
    Task<TodoModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TodoModel> UpdateAsync(TodoModel todo, CancellationToken cancellationToken);
}
```

```csharp
public sealed class TodoRepository(TodoDbContext dbContext) : ITodoRepository
{
    // AddAsync, ListAsync, GetByIdAsync unchanged.

    public async Task<TodoModel> UpdateAsync(TodoModel todo, CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        return todo;
    }
}
```

**No `dbContext.Todos.Update(todo)` call.** Unlike `AddAsync`'s `dbContext.Todos.Add(todo)`, this repository method does not need to tell EF Core the entity changed — `todo` here is the same tracked instance `GetByIdAsync` returned to the command handler (see [CQRS](#cqrs)), mutated in place by `ApplyTo()`. EF Core's change tracker already knows about it and detects the mutated properties on the next `SaveChangesAsync()` automatically. **This makes `GetByIdAsync`'s `AsNoTracking()` a real constraint worth restating**: [`../get-todo-by-id/spec.md#repository`](../get-todo-by-id/spec.md#repository) added `AsNoTracking()` to `GetByIdAsync` for its own (correct, still-valid) reason — that endpoint's query result is read-only and discarded. `UpdateTodoCommandHandler` reuses the same `GetByIdAsync` method, but for a case where the returned entity **is** mutated and needs to be saved. `AsNoTracking()` strips the tracked-instance behavior this repository method depends on — with it in place, `SaveChangesAsync()` would silently persist nothing, since the change tracker never saw the entity to begin with.

**Resolved by not calling `AsNoTracking()`'s `GetByIdAsync` from the handler at all** — the handler issues its own tracked query rather than reusing the read-only-optimized repository method built for a different endpoint's needs:

```csharp
public sealed class TodoRepository(TodoDbContext dbContext) : ITodoRepository
{
    // AddAsync, ListAsync unchanged.

    public Task<TodoModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<TodoModel?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Todos.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<TodoModel> UpdateAsync(TodoModel todo, CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        return todo;
    }
}
```

`ITodoRepository` gains `GetTrackedByIdAsync` alongside `UpdateAsync`; `UpdateTodoCommandHandler` calls `GetTrackedByIdAsync`, not `GetByIdAsync`. `GetTodoByIdQueryHandler` is unchanged and keeps calling the original `AsNoTracking()` `GetByIdAsync`. Two small, differently-named methods with near-identical bodies is judged the safer trade here over one method whose tracking behavior depends on which caller invokes it (a shared `GetByIdAsync(id, track: bool)` overload-by-flag was considered and rejected — a boolean parameter that silently changes whether a save later succeeds is exactly the kind of footgun a second, explicitly-named method avoids). This supersedes the interface shown earlier in this section; the two-method version above is what actually ships.

## Endpoint

`TodoApi.Todos/Endpoints/UpdateTodoEndpoint.cs`, shape per [`overview.md#endpoint-pattern`](../../architecture/backend/overview.md#endpoint-pattern):

```csharp
public sealed class UpdateTodoEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/{id:guid}", Handle)
            .WithName("UpdateTodo")
            .WithSummary("Update an existing to-do item's title, description, or due date")
            .AddEndpointFilter<ValidationFilter<Request>>();

    public sealed record Request(string Title, string? Description, DateTime? DueDate, bool IsCompleted);

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

    private static async Task<Results<Ok<TodoResponse>, NotFound>> Handle(
        Guid id,
        [FromBody] Request request,
        [FromServices] IUpdateTodoCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new UpdateTodoCommand(id, request), cancellationToken);

        if (result.Todo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(result.Todo.ToResponse());
    }
}
```

Notes:

- **`{id:guid}` route constraint**, same as `GetTodoByIdEndpoint` — a malformed `id` never reaches `Handle`; see [`../get-todo-by-id/spec.md#endpoint`](../get-todo-by-id/spec.md#endpoint) for the full reasoning, unchanged here.
- **`Results<Ok<TodoResponse>, NotFound>` return type**, same pattern as `GetTodoByIdEndpoint` and for the same reason — accurate `200`/`404` schemas in the generated `openapi.json`.
- **Both a route parameter and a bound request body** — the first endpoint in the feature to need both. No conflict: Minimal API model binding infers `id` from the route template and `request` from `[FromBody]` independently: order of parameters in `Handle`'s signature doesn't matter to the binder, but `id` is listed first here to match the URL's own left-to-right reading order (`/todos/{id}` → body).
- **`Request` is its own record, not reused from `AddTodoEndpoint`.** Unlike the earlier draft of this spec, `AddTodoEndpoint.Request` (`Title`, `Description`, `DueDate`) and `UpdateTodoEndpoint.Request` (`Title`, `Description`, `DueDate`, **`IsCompleted`**) are no longer even the same shape — Add has no notion of completion status at create time (`IsCompleted` is always `false` on create, per [`../add-todo/spec.md#todomodel-domain-model`](../add-todo/spec.md#todomodel-domain-model)), while Update's full-resource `PUT` contract requires it. So this is not a "kept separate despite matching" judgment call anymore, just two DTOs that are actually different.

Registered in `TodoApi.Gateway/Endpoints.cs` under the existing `/todos` group (`.MapEndpoint<UpdateTodoEndpoint>()`).

## Wiring Checklist

- [ ] `TodoApi.Todos/Commands/UpdateTodoCommand.cs`: new file — command, nullable-`Todo` result, named interface, handler (see [CQRS](#cqrs)).
- [ ] `TodoApi.Todos/Persistence/ITodoRepository.cs` + `TodoRepository.cs`: add `GetTrackedByIdAsync` and `UpdateAsync` (see [Repository](#repository)). `GetByIdAsync` is unchanged.
- [ ] `TodoApi.Todos/TodoMapping.cs`: add `ApplyTo(UpdateTodoEndpoint.Request, TodoModel)`.
- [ ] `TodoApi.Todos/Endpoints/UpdateTodoEndpoint.cs`: new file.
- [ ] `TodoApi.Todos/Extensions/ServiceCollectionExtensions.AddTodosServices()`: register `IUpdateTodoCommandHandler → UpdateTodoCommandHandler` (scoped, matching the existing handler registrations). `RequestValidator` is picked up automatically by the existing `AddValidatorsFromAssemblyContaining<AddTodoEndpoint.RequestValidator>()` scan.
- [ ] `TodoApi.Gateway/Endpoints.cs`: map `UpdateTodoEndpoint` under the existing `/todos` group.
- [ ] Run a full `dotnet build` and confirm `TodoApi.Gateway/openapi.json` regenerates to include `PUT /todos/{id}` with its `200`/`400`/`404` schemas, then commit the regenerated spec.

**Explicitly not needed** — stated so nobody goes looking: no EF Core migration (no schema change), no new `Dtos/` file (reuses the existing `TodoResponse`), no new NuGet package, no `appsettings` change, no CORS change (the existing `"Frontend"` policy already covers this origin and `PUT`), no new `ValidationFilter<TRequest>` variant (the existing generic filter works unchanged for `Request`).

## Tests

Per-layer, following the same split as [`../add-todo/spec.md`](../add-todo/spec.md#tests) and [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md#tests).

**`TodoApi.Todos.Tests`**

- `RequestValidator`: same cases as `AddTodoEndpoint.RequestValidatorTests` — empty/whitespace `Title` fails; `Title`/`Description` over max length fail; a past `DueDate` fails, today/future/`null` passes; a fully valid request passes.
- `UpdateTodoCommandHandler`:
  - Repository's `GetTrackedByIdAsync` returning a model: handler calls `ApplyTo()` on it (assert the mutated fields — `Title`/`Description`/`DueDate`/`UpdatedAt` — match the request), calls `UpdateAsync` with that same instance, and returns it wrapped in a non-null `UpdateTodoCommandResult.Todo`.
  - Repository's `GetTrackedByIdAsync` returning `null`: handler returns a result whose `Todo` is `null`, and `UpdateAsync` is **never called** — the assertion that pins "not found short-circuits before any write," mirroring `GetTodoByIdQueryHandler`'s "null means not found, not an error" test from [`../get-todo-by-id/spec.md#tests`](../get-todo-by-id/spec.md#tests).
  - Repository mocked via **Moq**, per [`../list-todos/spec.md#mocking`](../list-todos/spec.md#mocking).
- `TodoMapping.ApplyTo()`: given an existing `TodoModel` and a `Request`, asserts `Title`/`Description`/`DueDate`/`IsCompleted` are all overwritten with the request's values (including the case where the request flips `IsCompleted` from the model's current value), `UpdatedAt` becomes non-null and recent (`DateTime.UtcNow`-close), and `Id`/`CreatedAt` are **unchanged** from the model passed in — the assertion that pins [Full replacement, not partial](#full-replacement-not-partial-service-tier-fields)'s scope (four fields move, two never do) against accidentally widening later.

**`TodoApi.Gateway.Tests`**

Integration tests through the full pipeline via `TodoApiWebApplicationFactory`, with `ITodoRepository` replaced by a **Moq** mock (already wired per [`../list-todos/spec.md#mocking`](../list-todos/spec.md#mocking)):

- `PUT /todos/{id}` with the mock's `GetTrackedByIdAsync` returning a matching model and a valid body returns `200` with the updated fields in the response, including a non-null `UpdatedAt`.
- `PUT /todos/{id}` with the mock's `GetTrackedByIdAsync` returning `null` returns `404` with a Problem Details body, mirroring `GetTodoByIdEndpointTests`.
- `PUT /todos/{id}` with an invalid body (empty `Title`) returns `400` with a `ValidationProblemDetails` body naming `Title` — and the mock's `GetTrackedByIdAsync`/`UpdateAsync` are **never called**, confirming the `400`-before-`404` ordering from [DTO Contract](#dto-contract).
- `PUT /todos/not-a-guid` (malformed id) returns `404` via the route constraint, same as `GetTodoByIdEndpointTests`' equivalent case.
- A valid request against an existing id calls `ITodoRepository.UpdateAsync` exactly once with a model reflecting the request's values.

## Open Questions

- **`GetTrackedByIdAsync` naming.** Chosen to read clearly against the existing `AsNoTracking()` `GetByIdAsync` (see [Repository](#repository)), but if a later spec (e.g. Complete/Incomplete, which will have the identical "fetch-then-mutate-then-save" shape) needs the same tracked-fetch pattern, this is the moment to reconsider whether `GetByIdAsync` should take a `track: bool` after all now that there would be three call sites instead of two, or whether a small private helper inside `TodoRepository` shared by `GetTrackedByIdAsync`/`UpdateAsync`-style methods is worth it. Left as-is here since Update is still the only consumer.
- **How Complete/Incomplete reuse this endpoint's write path.** [Full replacement, not partial](#full-replacement-not-partial-service-tier-fields) decides that Complete/Incomplete are convenience endpoints over the same underlying resource write `PUT /todos/{id}` performs, but leaves the precise mechanism to their own specs. Two candidates: (a) each has its own tiny endpoint/command that fetches the existing `TodoModel` (via `GetTrackedByIdAsync`), flips `IsCompleted`, and calls the same `ITodoRepository.UpdateAsync`, duplicating the "fetch, check existence, save" shape `UpdateTodoCommandHandler` already has; or (b) Complete/Incomplete's endpoints construct an `UpdateTodoEndpoint.Request` internally (reading the todo first to get its current `Title`/`Description`/`DueDate`, then substituting `IsCompleted`) and dispatch through `UpdateTodoCommand` directly, reusing the handler outright. (b) avoids duplicating the existence-check/save logic but requires an extra read Complete/Incomplete's own contract may not otherwise need; (a) is more duplication but keeps each command's dependencies minimal. Left to those specs.

## Related Docs

- [`../add-todo/spec.md`](../add-todo/spec.md) — the `TodoModel` schema, validation rules this spec's `RequestValidator` mirrors, and test/mocking strategy this spec builds on
- [`../get-todo-by-id/spec.md`](../get-todo-by-id/spec.md) — the route-parameter/`{id:guid}` pattern, `404` contract, and shared `TodoResponse` DTO this spec reuses directly
- [`../list-todos/spec.md`](../list-todos/spec.md) — the Moq-based mocking strategy this spec's tests follow
- [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md) — endpoint pattern, mapping, repository shape, error response contract
- [`../../architecture/backend/cqrs.md`](../../architecture/backend/cqrs.md) — the CQRS pattern this command follows
- [`../../standards/aspnet-web-api-guidelines.md`](../../standards/aspnet-web-api-guidelines.md) — coding standards applied throughout
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source **Update** requirement
- [`../../requirements/requirements-qa-internal.md`](../../requirements/requirements-qa-internal.md) — Q8, the partial-vs-full-update ambiguity this spec resolves as a documented assumption
