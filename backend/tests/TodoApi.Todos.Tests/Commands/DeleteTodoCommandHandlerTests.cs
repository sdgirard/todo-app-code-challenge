using Moq;
using TodoApi.Todos.Commands;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Tests.Commands;

public class DeleteTodoCommandHandlerTests
{
    // When the todo exists, the handler should delete it and report success.
    [Fact]
    public async Task HandleAsync_WithMatchingTodo_DeletesAndReturnsDeletedTrue()
    {
        var todo = CreateTodo("Buy milk");
        var repository = new Mock<ITodoRepository>();
        repository
            .Setup(r => r.GetTrackedByIdAsync(todo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);
        var handler = new DeleteTodoCommandHandler(repository.Object);

        var result = await handler.HandleAsync(new DeleteTodoCommand(todo.Id), CancellationToken.None);

        Assert.True(result.Deleted);
        repository.Verify(r => r.DeleteAsync(todo, It.IsAny<CancellationToken>()), Times.Once);
    }

    // A missing todo should come back as Deleted == false, not a thrown exception —
    // the endpoint depends on this to decide when to return 404 — and nothing
    // should be deleted.
    [Fact]
    public async Task HandleAsync_WithNoMatchingTodo_ReturnsDeletedFalseAndDoesNotDelete()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<ITodoRepository>();
        repository
            .Setup(r => r.GetTrackedByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoModel?)null);
        var handler = new DeleteTodoCommandHandler(repository.Object);

        var result = await handler.HandleAsync(new DeleteTodoCommand(id), CancellationToken.None);

        Assert.False(result.Deleted);
        repository.Verify(
            r => r.DeleteAsync(It.IsAny<TodoModel>(), It.IsAny<CancellationToken>()),
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
