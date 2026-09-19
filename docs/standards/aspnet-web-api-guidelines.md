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
- DTO ⇄ Model mapping is **hand-written** — plain static extension methods (`ToModel()`/`ToResponse()`), no mapping library. Not AutoMapper (licensing); Mapster was also considered and dropped (no real source-generator package exists, and the CLI-codegen alternative's build-lag mechanic wasn't worth it for a model this small). See [`backend/overview.md`](../architecture/backend/overview.md#mapping-dto--model) for the full rationale.
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

## Secrets Management

Any sensitive value — certificate passwords, connection strings that carry credentials, API keys, anything that shouldn't be readable by someone with repo/image access — is supplied via a **mounted secrets file**, never as a literal value in an environment variable, `appsettings*.json`, or anywhere else in source control or the image itself.

- **Never hardcode secrets** in `appsettings.json`/`appsettings.Production.json` or inline in code. These files hold structure and non-sensitive defaults (e.g. the `ConnectionStrings__TodoDb` *path*, which isn't sensitive) — not the sensitive values themselves.
- **Never pass secret values as plain environment variable values** (e.g. `-e SOME_PASSWORD=hunter2` or a K8s `env:` entry with a literal `value:`) — env vars are visible via `docker inspect`, `/proc/<pid>/environ`, process listings, and are easy to accidentally log or leak into crash dumps.
- **Secrets are supplied as mounted files**, sourced from a K8s `Secret` volume-mounted into the pod (e.g. the TLS `.pfx` and its password, see [`../infra/container-image.md#cert-mounting`](../infra/container-image.md#cert-mounting)), or an equivalent local-dev mechanism (`dotnet user-secrets`, or a gitignored local file) for running outside the cluster. ASP.NET Core's configuration system reads file-provider-backed configuration the same way it reads env vars, so this doesn't complicate call sites — `IConfiguration` still resolves the value, it's just backed by a mounted file instead of the process environment.
- Where an env var is unavoidable for a *non-secret* setting that happens to configure where a secret lives (e.g. `ASPNETCORE_Kestrel__Certificates__Default__Path` pointing at the mounted `.pfx` file path), that's fine — the env var holds a path, not the secret material itself. The distinction is: does the env var's value need to stay confidential? If yes, it's a mounted file, not an env var.
- This applies to local Docker runs too: mount a local secrets file/directory rather than passing `-e` with real values, even for local testing, so the habit doesn't diverge between environments.

## Testing

- **Every `[Fact]`/`[Theory]` gets a one-line comment directly above it, in plain English, explaining what it's testing.** Write it the way you'd explain the test out loud to a teammate — not a restatement of the method name, not dense technical shorthand. Test method names are already descriptive (`Method_Scenario_ExpectedResult`), so the comment should add the *why*/*what's actually being checked* that the name alone doesn't carry — e.g. "makes sure the handler never even gets called when the input is invalid" rather than "verifies short-circuit behavior." See the `TodoApi.Todos.Tests`/`TodoApi.Gateway.Tests` files under the `AddTodo` feature for the pattern in practice.
- **`ITodoRepository` is mocked with Moq, not a hand-written fake.** An earlier draft of this doc preferred a hand-written fake for a single-method-shaped interface, reasoning that a dependency wasn't worth it for something a five-line private class could cover. That preference didn't survive contact with `ITodoRepository` actually growing past one method (`AddAsync`, `ListAsync`, `GetByIdAsync`, `GetTrackedByIdAsync`, `UpdateAsync`, `DeleteAsync`) across the Add/List/GetById/Update/Delete/UpdateCompletionStatus specs — a hand-written fake would have needed its own call-tracking and per-test return-value wiring for six methods, which is exactly the "genuinely tedious to hand-maintain" case the earlier guidance already carved out as the exception. Every test in `TodoApi.Todos.Tests` and `TodoApi.Gateway.Tests` uses `Mock<ITodoRepository>` (Moq) — see [`../features/list-todos/spec.md#mocking`](../features/list-todos/spec.md#mocking) for where this was decided.
- Integration tests (`TodoApi.Gateway.Tests`) run the real API pipeline via `WebApplicationFactory<Program>` (`TodoApiWebApplicationFactory`) — routing, model binding, `ValidationFilter`, CQRS dispatch, and JSON serialization are all exercised for real. `ITodoRepository` itself is replaced with the `Mock<ITodoRepository>` above (`services.AddSingleton(Repository.Object)`), so these tests do **not** exercise the repository against a real database. `TodoDbContext` is still registered against a kept-open SQLite in-memory connection (`Data Source=:memory:`, swapped in via `ConfigureWebHost`) purely because ASP.NET Core startup applies EF Core migrations against it — nothing in the test suite reads through that context once the repository is mocked. Real repository/EF Core/SQLite round-trip behavior is **not** covered by this test suite; that gap is recorded, not accidental — see `TodoApiWebApplicationFactory`'s own doc comment.

## General .NET Conventions

Beyond the above, follow standard [.NET / C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and [ASP.NET Core best practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices) — naming, async/await usage, nullable reference types, etc. Not re-documenting those here; this doc only covers where we're being deliberate or opinionated beyond the defaults.

## Related Docs

- [`../architecture/backend/overview.md`](../architecture/backend/overview.md) — backend architecture these standards apply to
- [`../architecture/backend/cqrs.md`](../architecture/backend/cqrs.md) — CQRS pattern without a mediator library
- [`../architecture/overview-architecture.md`](../architecture/overview-architecture.md) — system-level overview
- [`../infra/container-image.md`](../infra/container-image.md) — container image design, including cert/secret mounting
