using Moq;
using TodoApi.Todos.Commands;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Tests.Commands;

public class AddTodoCommandHandlerTests
{
    // The handler should just hand the todo off to the repository to save,
    // then return whatever the repository gives back.
    [Fact]
    public async Task HandleAsync_WithTodo_PersistsAndReturnsSavedTodo()
    {
        var todo = new TodoModel
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
        };
        var repository = new Mock<ITodoRepository>();
        repository
            .Setup(r => r.AddAsync(todo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);
        var handler = new AddTodoCommandHandler(repository.Object);

        var result = await handler.HandleAsync(new AddTodoCommand(todo), CancellationToken.None);

        repository.Verify(r => r.AddAsync(todo, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Same(todo, result.Todo);
    }
}
