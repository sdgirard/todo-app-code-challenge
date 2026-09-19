# ASP.NET Core Web API Guidelines

Living doc — minimal set to start, extended as real decisions come up during implementation rather than written exhaustively up front. Covers the backend described in [`../architecture/backend/overview.md`](../architecture/backend/overview.md) — **.NET 10**.

## Warnings as Errors

All projects build with `TreatWarningsAsErrors` enabled, set once in `backend/Directory.Build.props` rather than per-project. A compiler warning is either a real problem or noise worth silencing at the point it's produced (`#pragma warning disable` with a comment explaining why, or a narrower fix) — leaving it as a warning means it's easy to accumulate and eventually miss the one that matters. No project should turn this off individually.

## Coding Style & Patterns

- Use **C# 14** features natively where they simplify the code: primary constructors, collection expressions, switch expressions.
- File-scoped namespaces only (`namespace TodoApi.Services;`), not the block-scoped `namespace { }` form.
- All endpoint handlers must be asynchronous, returning `Task<IResult>` or a `Task<Results<...>>` typed-results union — no synchronous handlers.
- Always use explicit access modifiers (`public`, `private`, `internal`) — never rely on C#'s implicit `private` default.
- Follow the private field prefix convention (`_logger`, `_repository`).

## Architecture Standards

- Follow a strict decoupled structure: Minimal API Endpoint → Application Service/CQRS Command or Query → Infrastructure Repository. This is the same layering documented in [`backend/overview.md`](../architecture/backend/overview.md#layered-view-n-tier) — DTOs stop at the service tier, only domain Models flow below it.
- DTO ⇄ Model mapping uses **Mapster in code-generation mode**, not AutoMapper (licensing) and not Mapster's default runtime-reflection mode. See [`backend/overview.md`](../architecture/backend/overview.md#mapping-dto--model) for the rationale.
- Return `IResult` (or typed `Results<...>` unions) from Minimal API endpoints using `TypedResults.Ok()`, `TypedResults.NotFound()`, etc. rather than raw objects or `IActionResult`.
- Use `FluentValidation` validators kept separate from the request payload record itself — a `RequestValidator : AbstractValidator<Request>` class alongside the `Request` record, not validation logic embedded in the record or the handler. See the endpoint example in [`backend/overview.md`](../architecture/backend/overview.md#endpoint-pattern).
- Keep `Program.cs` clean by leveraging extension methods for service registration and pipeline configuration blocks (e.g., `builder.AddServices()`, `app.Configure()`) rather than inlining setup directly in `Program.cs`. This is the `ConfigureServices`/`ConfigureApps` split documented in [`backend/overview.md`](../architecture/backend/overview.md#bootstrapping).
- CQRS commands/queries are dispatched via direct handler injection, no mediator library (MediatR ruled out — same licensing issue as AutoMapper). See [`backend/cqrs.md`](../architecture/backend/cqrs.md).

## Dependency Injection

Use DI wherever a dependency is needed — services, repositories, validators, loggers, etc. get constructor-injected (or method-injected via `[FromServices]` in Minimal API handlers), not `new`'d up inline or reached for via static/singleton access. Keeps everything testable in isolation and keeps the DI container as the single source of truth for how objects get wired together.

## Single Responsibility

Every class and method should have one reason to change. This is the same principle behind the endpoint-per-class pattern in [`backend/overview.md`](../architecture/backend/overview.md#endpoint-pattern) — one endpoint class handles one route, one CQRS handler handles one command or query, one service does one job. If a class is accumulating unrelated responsibilities, it should split.

## Cyclomatic Complexity

Keep cyclomatic complexity below **15** per method/class wherever reasonably possible. This is a ceiling, not a target — most methods should land well under it. High complexity is usually a sign a method is doing too much and should be broken apart (often overlaps with a Single Responsibility violation).

## Minimize `else`

Prefer guard clauses / early returns over `else` branches. Rationale: [Why You Shouldn't Use the `else` Statement in Your Code](https://anthony-trad.medium.com/why-you-shouldnt-use-the-else-statement-in-your-code-b74e8a218b30) — in short, early returns keep the happy path unindented and readable, and each precondition failure exits immediately instead of nesting the rest of the method inside a conditional.

```csharp
// Prefer
if (todo is null)
{
    return TypedResults.NotFound();
}

return TypedResults.Ok(todo);

// Over
if (todo is null)
{
    return TypedResults.NotFound();
}
else
{
    return TypedResults.Ok(todo);
}
```

## Error Responses

Use ASP.NET Core's built-in **RFC 9457 Problem Details** format (`application/problem+json`) for all error responses — this is the framework-native convention, not a custom shape.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Todo with id '3fa85f64-...' was not found.",
  "instance": "/todos/3fa85f64-..."
}
```

Validation errors use the `ValidationProblemDetails` subtype, which adds a field → messages `errors` dictionary:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Title": ["Title is required."],
    "DueDate": ["DueDate must be a valid date."]
  }
}
```

Wiring:

- `builder.Services.AddProblemDetails()` registers the default Problem Details service.
- `app.UseExceptionHandler()` (or a custom `IExceptionHandler`) catches unhandled exceptions and formats them as Problem Details automatically — this is where the 500-level "unexpected server failure" case is handled, distinct from the 400/404-level cases returned deliberately by an endpoint.
- Endpoints return `TypedResults.NotFound()`, `TypedResults.Problem(...)`, or `TypedResults.ValidationProblem(errors)` directly — these already produce RFC 9457-shaped bodies, no custom error DTO needed.
- FluentValidation's ASP.NET Core integration feeds naturally into `TypedResults.ValidationProblem`.

## General .NET Conventions

Beyond the above, follow standard [.NET / C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and [ASP.NET Core best practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) — naming, async/await usage, nullable reference types, etc. Not re-documenting those here; this doc only covers where we're being deliberate or opinionated beyond the defaults.

## Related Docs

- [`../architecture/backend/overview.md`](../architecture/backend/overview.md) — backend architecture these standards apply to
- [`../architecture/backend/cqrs.md`](../architecture/backend/cqrs.md) — CQRS pattern without a mediator library
- [`../architecture/overview-architecture.md`](../architecture/overview-architecture.md) — system-level overview
