using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Gateway.Tests;

public class UpdateTodoEndpointTests : IDisposable
{
    private readonly TodoApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Mock<ITodoRepository> _repository;

    public UpdateTodoEndpointTests()
    {
        _client = _factory.CreateClient();
        _repository = _factory.Repository;
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<TodoModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoModel todo, CancellationToken _) => todo);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // Updating an existing todo through the real API should return its new details,
    // including a freshly stamped updatedAt.
    [Fact]
    public async Task PutTodos_WithMatchingTodoAndValidBody_ReturnsOkWithUpdatedDetails()
    {
        var todo = CreateTodo("Buy milk", description: null);
        StoredTodoIs(todo.Id, todo);
        var request = new
        {
            title = "Buy milk and eggs",
            description = "2% or whole",
            dueDate = "2026-09-26T00:00:00Z",
            isCompleted = true,
        };

        var response = await _client.PutAsJsonAsync($"/todos/{todo.Id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(todo.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("Buy milk and eggs", body.GetProperty("title").GetString());
        Assert.Equal("2% or whole", body.GetProperty("description").GetString());
        Assert.True(body.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("updatedAt").ValueKind);
    }

    // A valid update should actually reach the persistence layer with the
    // requested changes applied.
    [Fact]
    public async Task PutTodos_WithMatchingTodoAndValidBody_PersistsTheChanges()
    {
        var todo = CreateTodo("Buy milk", description: null);
        StoredTodoIs(todo.Id, todo);
        var request = new { title = "Buy milk and eggs", description = (string?)null, dueDate = (string?)null, isCompleted = true };

        await _client.PutAsJsonAsync($"/todos/{todo.Id}", request);

        _repository.Verify(
            r => r.UpdateAsync(
                It.Is<TodoModel>(t => t.Title == "Buy milk and eggs" && t.IsCompleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Omitting the optional fields should clear them, not leave the existing
    // values in place — PUT is a full replacement, not a partial update.
    [Fact]
    public async Task PutTodos_OmittingOptionalFields_ClearsThem()
    {
        var todo = CreateTodo("Buy milk", description: "2% or whole");
        todo.DueDate = DateTime.UtcNow.AddDays(1);
        StoredTodoIs(todo.Id, todo);
        var request = new { title = "Buy milk", isCompleted = false };

        var response = await _client.PutAsJsonAsync($"/todos/{todo.Id}", request);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("dueDate").ValueKind);
    }

    // Updating a todo that doesn't exist should come back as a clean 404.
    [Fact]
    public async Task PutTodos_WithNoMatchingTodo_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        StoredTodoIs(id, todo: null);
        var request = new { title = "Buy milk", isCompleted = false };

        var response = await _client.PutAsJsonAsync($"/todos/{id}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // An invalid body should be rejected before the existence check ever runs —
    // 400, not 404 — and nothing should be looked up or saved.
    [Fact]
    public async Task PutTodos_WithMissingTitle_ReturnsBadRequestWithValidationProblemNamingTitleAndDoesNotTouchRepository()
    {
        var id = Guid.NewGuid();
        var request = new { title = "", isCompleted = false };

        var response = await _client.PutAsJsonAsync($"/todos/{id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("Title", out _));

        _repository.Verify(
            r => r.GetTrackedByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<TodoModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Trying to update a todo with a due date that's already overdue should be rejected.
    [Fact]
    public async Task PutTodos_WithPastDueDate_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var request = new { title = "Buy milk", dueDate = "2020-01-01T00:00:00Z", isCompleted = false };

        var response = await _client.PutAsJsonAsync($"/todos/{id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // A malformed id shouldn't reach the handler at all — routing itself should
    // reject it before any lookup happens.
    [Fact]
    public async Task PutTodos_WithMalformedId_ReturnsNotFound()
    {
        var request = new { title = "Buy milk", isCompleted = false };

        var response = await _client.PutAsync(
            "/todos/not-a-guid",
            JsonContent.Create(request));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repository.Verify(
            r => r.GetTrackedByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A matching request should actually look the todo up by the requested id.
    [Fact]
    public async Task PutTodos_WithMatchingTodo_LooksItUpByTheRequestedId()
    {
        var todo = CreateTodo("Buy milk", description: null);
        StoredTodoIs(todo.Id, todo);
        var request = new { title = "Buy milk", isCompleted = false };

        await _client.PutAsJsonAsync($"/todos/{todo.Id}", request);

        _repository.Verify(r => r.GetTrackedByIdAsync(todo.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void StoredTodoIs(Guid id, TodoModel? todo) =>
        _repository
            .Setup(r => r.GetTrackedByIdAsync(id, It.IsAny<CancellationToken>()))
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
