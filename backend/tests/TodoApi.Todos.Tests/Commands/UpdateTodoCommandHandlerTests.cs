using Moq;
using TodoApi.Todos.Commands;
using TodoApi.Todos.Endpoints;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Tests.Commands;

public class UpdateTodoCommandHandlerTests
{
    // When the todo exists, the handler should apply the request onto it and save it.
    [Fact]
    public async Task HandleAsync_WithMatchingTodo_AppliesRequestAndPersists()
    {
        var todo = CreateTodo("Buy milk");
        var request = new UpdateTodoEndpoint.Request("Buy milk and eggs", "2% or whole", null, true);
        var repository = new Mock<ITodoRepository>();
        repository
            .Setup(r => r.GetTrackedByIdAsync(todo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);
        repository
            .Setup(r => r.UpdateAsync(todo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);
        var handler = new UpdateTodoCommandHandler(repository.Object);

        var result = await handler.HandleAsync(new UpdateTodoCommand(todo.Id, request), CancellationToken.None);

        Assert.Equal("Buy milk and eggs", todo.Title);
        Assert.Equal("2% or whole", todo.Description);
        Assert.True(todo.IsCompleted);
        repository.Verify(r => r.UpdateAsync(todo, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Same(todo, result.Todo);
    }

    // A missing todo should come back as a null result, not a thrown exception —
    // the endpoint depends on this to decide when to return 404 — and nothing
    // should be saved.
    [Fact]
    public async Task HandleAsync_WithNoMatchingTodo_ReturnsNullTodoAndDoesNotPersist()
    {
        var id = Guid.NewGuid();
        var request = new UpdateTodoEndpoint.Request("Buy milk", null, null, false);
        var repository = new Mock<ITodoRepository>();
        repository
            .Setup(r => r.GetTrackedByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoModel?)null);
        var handler = new UpdateTodoCommandHandler(repository.Object);

        var result = await handler.HandleAsync(new UpdateTodoCommand(id, request), CancellationToken.None);

        Assert.Null(result.Todo);
        repository.Verify(
            r => r.UpdateAsync(It.IsAny<TodoModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TodoModel CreateTodo(string title) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow,
    };
}
