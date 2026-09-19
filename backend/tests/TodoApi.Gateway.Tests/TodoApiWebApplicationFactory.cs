using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TodoApi.Todos.Persistence;

namespace TodoApi.Gateway.Tests;

/// <summary>
/// Hosts the real API pipeline with ITodoRepository replaced by a Moq mock, so
/// endpoint tests exercise routing, model binding, validation, CQRS dispatch and
/// JSON serialization without touching a database. Set up and assert against
/// <see cref="Repository"/>.
///
/// TodoDbContext is still registered (onto a kept-open SQLite in-memory connection)
/// because startup applies migrations; nothing reads through it once the repository
/// is mocked.
///
/// Trade-off, recorded deliberately: this drops real EF Core/SQLite round-trip
/// coverage. It is what makes persistence-layer bugs invisible to this suite —
/// mapping, query translation and provider behavior are no longer exercised
/// end-to-end. Repository-level coverage would need its own tests against a real
/// provider to close that gap.
/// </summary>
public sealed class TodoApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public Mock<ITodoRepository> Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TodoDbContext>>();
            services.AddDbContext<TodoDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<ITodoRepository>();
            services.AddSingleton(Repository.Object);
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
