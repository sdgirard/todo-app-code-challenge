# CQRS Without a Mediator Library

Covers command/query dispatch for the backend described in [`overview.md`](./overview.md).

## Not using MediatR

Same reason as AutoMapper (see [`overview.md#mapping-dto--model`](./overview.md#mapping-dto--model)) — MediatR is the same author (Jimmy Bogard) and moved to a paid commercial license alongside AutoMapper. Not appropriate to pull into this project.

## Alternative: DI + interfaces, no mediator abstraction

A mediator library's whole job is indirection: instead of calling a handler directly, you send a message (`IRequest<T>`) through a generic `ISender`/`IMediator`, which looks up the right handler at runtime and dispatches to it. That indirection buys you things like pipeline behaviors (cross-cutting concerns wrapped around every handler) and loose coupling between caller and handler type.

For a project this size, the runtime dispatch/lookup part has no payoff — there's one caller per command/query (the endpoint that owns it). But interfaces are still worth having, not for swappability (each handler only ever has one implementation) but for **shape and consistency**: a uniform contract every handler implements, symmetry with `ITodoRepository` (already an interface, for the same reasons), and a hook for decorators later if cross-cutting concerns are ever needed without a mediator.

### The pattern

1. A generic base interface defines the shared shape.
2. A **named interface per handler** inherits the generic one — no new members, just a specific name. This is what the endpoint actually injects: `IAddTodoCommandHandler` reads far better at an injection site than `ICommandHandler<AddTodoCommand, AddTodoCommandResult>`, and DI registration maps the named interface straight to its implementation — no closed-generic registration needed.
3. The concrete handler class implements the named interface (and transitively the generic one).

```csharp
// ICommandHandler.cs / IQueryHandler.cs — shared generic contracts, one per feature library
// or a common project if reused across features later.
public interface ICommandHandler<TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
```

```csharp
// AddTodoCommand.cs — command, result, named interface, and handler together in one file,
// same colocation principle as the endpoint pattern.
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

```csharp
// Endpoint call site — injects the named interface, not the concrete class.
private static async Task<Results<Created<Response>, ValidationProblem>> Handle(
    [FromBody] Request request,
    [FromServices] IAddTodoCommandHandler handler,   // <- named interface, not ISender, not the concrete class
    CancellationToken cancellationToken)
{
    var model = request.ToModel();
    var result = await handler.HandleAsync(new AddTodoCommand(model), cancellationToken);
    var response = result.ToResponse();
    return TypedResults.Created($"/todos/{response.Id}", response);
}
```

Still no `ISender`, no `IRequest<T>` marker interfaces, no runtime handler lookup — the endpoint's dependency is resolved by ordinary DI, at compile time, same as any other injected service. The interface layer buys readability and consistency, not dispatch flexibility.

## Registration

Handler classes are registered against their named interface. Each feature's `ServiceCollectionExtensions.AddTodosServices()` (see [`overview.md#directory-structure`](./overview.md#directory-structure)) registers its own handlers explicitly:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTodosServices(this IServiceCollection services)
    {
        services.AddScoped<IAddTodoCommandHandler, AddTodoCommandHandler>();
        services.AddScoped<IUpdateTodoCommandHandler, UpdateTodoCommandHandler>();
        services.AddScoped<ICompleteTodoCommandHandler, CompleteTodoCommandHandler>();
        services.AddScoped<IIncompleteTodoCommandHandler, IncompleteTodoCommandHandler>();
        services.AddScoped<IDeleteTodoCommandHandler, DeleteTodoCommandHandler>();
        services.AddScoped<IListTodosQueryHandler, ListTodosQueryHandler>();
        services.AddScoped<IGetTodoByIdQueryHandler, GetTodoByIdQueryHandler>();

        return services;
    }
}
```

Explicit and a little repetitive, but for seven handlers that's a non-issue, and it's more transparent than assembly-scanning magic — anyone reading this file sees exactly what's registered and how.

## What we give up by not having a mediator

Worth naming honestly, since a mediator isn't purely ceremony — these are real capabilities MediatR-style libraries provide that this approach doesn't:

- **Automatic pipeline behaviors** — a mediator can wrap every handler in cross-cutting concerns (logging, validation, transactions) registered once, globally. Here, the same thing is achievable per-handler via a decorator implementing the same named interface (e.g. a `LoggingAddTodoCommandHandler : IAddTodoCommandHandler` wrapping the real one), but it has to be wired up explicitly per handler rather than applying automatically to all of them.
- **Fully decoupling the caller from which handler exists** — a mediator lets a caller send a message without knowing or importing any handler type at all. Here, the endpoint still has a compile-time reference to `IAddTodoCommandHandler` specifically. That's fine when there's one caller per handler (true here); it matters more when many unrelated callers need to invoke the same handler through a uniform, decoupled entry point.

Given the project's size (one feature, seven operations, one caller per handler), neither tradeoff is costly enough to justify a mediator even ignoring the licensing issue — DI + named interfaces gets the readability and consistency benefits without paying for indirection this project doesn't need.

## Where the generic interfaces live

**Decided:** `ICommandHandler<,>`/`IQueryHandler<,>` live inside `TodoApi.Todos`, not a separate shared project — same "don't split until there's a second consumer" reasoning used for persistence (see [`overview.md`](./overview.md#directory-structure)). Revisit only if a second feature needs the same contracts.

## Open Questions

- Exact command/query list is defined in [`overview.md`](./overview.md#open-questions--todo) — Add/Update/Complete/Incomplete/Delete as commands, List/View as queries. Naming convention (`{Operation}{Feature}Command` / `I{Operation}{Feature}CommandHandler` / `{Operation}{Feature}CommandHandler`) follows from the example above but isn't formally written down elsewhere yet.

## Related Docs

- [`overview.md`](./overview.md) — backend architecture this pattern fits into
- [`../overview-architecture.md`](../overview-architecture.md) — system-level overview
