using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Gateway.Tests;

public class GetTodoByIdEndpointTests : IDisposable
{
    private readonly TodoApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Mock<ITodoRepository> _repository;

    public GetTodoByIdEndpointTests()
    {
        _client = _factory.CreateClient();
        _repository = _factory.Repository;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // Asking for a todo that exists should come back with its full details.
    [Fact]
    public async Task GetTodoById_WithMatchingTodo_ReturnsOkWithItsDetails()
    {
        var todo = CreateTodo("Buy milk", description: "2% or whole");
        StoredTodoIs(todo.Id, todo);

        var response = await _client.GetAsync($"/todos/{todo.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(todo.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("Buy milk", body.GetProperty("title").GetString());
        Assert.Equal("2% or whole", body.GetProperty("description").GetString());
        Assert.False(body.GetProperty("isCompleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("updatedAt").ValueKind);
    }

    // A todo that's been edited should report when.
    [Fact]
    public async Task GetTodoById_WithEditedTodo_ReportsUpdatedAt()
    {
        var todo = CreateTodo("Buy milk", description: null);
        todo.UpdatedAt = new DateTime(2026, 9, 19, 11, 5, 2, DateTimeKind.Utc);
        StoredTodoIs(todo.Id, todo);

        var response = await _client.GetAsync($"/todos/{todo.Id}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(todo.UpdatedAt, body.GetProperty("updatedAt").GetDateTime());
    }

    // Asking for a todo that doesn't exist should come back as a clean 404,
    // not a 200 with an empty/null body.
    [Fact]
    public async Task GetTodoById_WithNoMatchingTodo_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        StoredTodoIs(id, todo: null);

        var response = await _client.GetAsync($"/todos/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // A malformed id shouldn't reach the handler at all — routing itself should
    // reject it before any lookup happens.
    [Fact]
    public async Task GetTodoById_WithMalformedId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/todos/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repository.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A matching request should actually reach the persistence layer with the
    // requested id, not some other value.
    [Fact]
    public async Task GetTodoById_WithMatchingTodo_LooksItUpByTheRequestedId()
    {
        var todo = CreateTodo("Buy milk", description: null);
        StoredTodoIs(todo.Id, todo);

        await _client.GetAsync($"/todos/{todo.Id}");

        _repository.Verify(r => r.GetByIdAsync(todo.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void StoredTodoIs(Guid id, TodoModel? todo) =>
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);

    private static TodoModel CreateTodo(string title, string? description) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = description,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow,
    };
}
