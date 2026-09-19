using Moq;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;
using TodoApi.Todos.Queries;

namespace TodoApi.Todos.Tests.Queries;

public class ListTodosQueryHandlerTests
{
    // The handler should hand back every todo the repository has, untouched.
    [Fact]
    public async Task HandleAsync_WithStoredTodos_ReturnsAllOfThem()
    {
        var first = CreateTodo("Buy milk");
        var second = CreateTodo("Renew passport");
        var handler = new ListTodosQueryHandler(CreateRepository(first, second));

        var result = await handler.HandleAsync(new ListTodosQuery(), CancellationToken.None);

        Assert.Equal(2, result.Todos.Count);
        Assert.Contains(first, result.Todos);
        Assert.Contains(second, result.Todos);
    }

    // With nothing stored, the handler should return an empty list rather than null —
    // the endpoint's "[]" response depends on this.
    [Fact]
    public async Task HandleAsync_WithNoTodos_ReturnsEmptyListNotNull()
    {
        var handler = new ListTodosQueryHandler(CreateRepository());

        var result = await handler.HandleAsync(new ListTodosQuery(), CancellationToken.None);

        Assert.NotNull(result.Todos);
        Assert.Empty(result.Todos);
    }

    private static ITodoRepository CreateRepository(params TodoModel[] todos)
    {
        var repository = new Mock<ITodoRepository>();
        repository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(todos);
        return repository.Object;
    }

    private static TodoModel CreateTodo(string title) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow,
    };
}
