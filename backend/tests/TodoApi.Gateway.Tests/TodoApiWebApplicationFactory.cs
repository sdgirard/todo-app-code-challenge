using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TodoApi.Todos.Persistence;

namespace TodoApi.Gateway.Tests;

/// <summary>
/// Swaps TodoDbContext onto a kept-open SQLite in-memory connection, so each test
/// class gets an isolated database (no shared file/state), while still exercising
/// the real SQLite provider rather than EF Core's InMemory provider (ruled out for
/// non-test use — see docs/architecture/backend/overview.md#persistence — but real
/// enough here for integration tests too, since it's the same underlying engine).
///
/// Deliberately nothing is mocked below the HTTP boundary — real TodoDbContext,
/// real TodoRepository, real EF Core migrations. A more common pattern mocks
/// ITodoRepository and stops the integration test at the service tier; that's
/// cheaper/faster at scale but would have hidden the DueDate Kind/offset
/// round-trip quirk this suite pins (see AddTodoEndpointTests). Worth
/// re-evaluating if the endpoint surface grows large enough that full-stack
/// SQLite setup per test class becomes a real cost.
/// </summary>
public sealed class TodoApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TodoDbContext>>();
            services.AddDbContext<TodoDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
