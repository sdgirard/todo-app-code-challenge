using Moq;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;
using TodoApi.Todos.Queries;

namespace TodoApi.Todos.Tests.Queries;

public class GetTodoByIdQueryHandlerTests
{
    // When the repository has a matching todo, the handler should hand it back.
    [Fact]
    public async Task HandleAsync_WithMatchingTodo_ReturnsIt()
    {
        var todo = CreateTodo("Buy milk");
        var handler = new GetTodoByIdQueryHandler(CreateRepository(todo.Id, todo));

        var result = await handler.HandleAsync(new GetTodoByIdQuery(todo.Id), CancellationToken.None);

        Assert.Equal(todo, result.Todo);
    }

    // A missing todo should come back as a null result, not a thrown exception —
    // the endpoint depends on this to decide when to return 404.
    [Fact]
    public async Task HandleAsync_WithNoMatchingTodo_ReturnsNullTodo()
    {
        var id = Guid.NewGuid();
        var handler = new GetTodoByIdQueryHandler(CreateRepository(id, todo: null));

        var result = await handler.HandleAsync(new GetTodoByIdQuery(id), CancellationToken.None);

        Assert.Null(result.Todo);
    }

    private static ITodoRepository CreateRepository(Guid id, TodoModel? todo)
    {
        var repository = new Mock<ITodoRepository>();
        repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);
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
