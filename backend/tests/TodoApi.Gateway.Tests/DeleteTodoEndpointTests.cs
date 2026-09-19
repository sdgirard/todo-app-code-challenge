using System.Net;
using Moq;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Gateway.Tests;

public class DeleteTodoEndpointTests : IDisposable
{
    private readonly TodoApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Mock<ITodoRepository> _repository;

    public DeleteTodoEndpointTests()
    {
        _client = _factory.CreateClient();
        _repository = _factory.Repository;
        _repository
            .Setup(r => r.DeleteAsync(It.IsAny<TodoModel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // Deleting an existing todo through the real API should return an empty 204.
    [Fact]
    public async Task DeleteTodos_WithMatchingTodo_ReturnsNoContent()
    {
        var todo = CreateTodo("Buy milk");
        StoredTodoIs(todo.Id, todo);

        var response = await _client.DeleteAsync($"/todos/{todo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
    }

    // A successful delete should actually reach the persistence layer with the
    // looked-up todo.
    [Fact]
    public async Task DeleteTodos_WithMatchingTodo_PersistsTheDeletion()
    {
        var todo = CreateTodo("Buy milk");
        StoredTodoIs(todo.Id, todo);

        await _client.DeleteAsync($"/todos/{todo.Id}");

        _repository.Verify(r => r.DeleteAsync(todo, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Deleting a todo that doesn't exist should come back as a clean 404, and
    // nothing should be deleted.
    [Fact]
    public async Task DeleteTodos_WithNoMatchingTodo_ReturnsNotFoundAndDoesNotDelete()
    {
        var id = Guid.NewGuid();
        StoredTodoIs(id, todo: null);

        var response = await _client.DeleteAsync($"/todos/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repository.Verify(
            r => r.DeleteAsync(It.IsAny<TodoModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A malformed id shouldn't reach the handler at all — routing itself should
    // reject it before any lookup happens.
    [Fact]
    public async Task DeleteTodos_WithMalformedId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/todos/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repository.Verify(
            r => r.GetTrackedByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A matching request should actually look the todo up by the requested id.
    [Fact]
    public async Task DeleteTodos_WithMatchingTodo_LooksItUpByTheRequestedId()
    {
        var todo = CreateTodo("Buy milk");
        StoredTodoIs(todo.Id, todo);

        await _client.DeleteAsync($"/todos/{todo.Id}");

        _repository.Verify(r => r.GetTrackedByIdAsync(todo.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void StoredTodoIs(Guid id, TodoModel? todo) =>
        _repository
            .Setup(r => r.GetTrackedByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);

    private static TodoModel CreateTodo(string title) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow,
    };
}
