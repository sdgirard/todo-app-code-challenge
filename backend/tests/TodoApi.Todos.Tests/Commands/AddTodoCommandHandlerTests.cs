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
        var repository = new FakeTodoRepository();
        var handler = new AddTodoCommandHandler(repository);
        var todo = new TodoModel
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await handler.HandleAsync(new AddTodoCommand(todo), CancellationToken.None);

        Assert.Same(todo, repository.LastAdded);
        Assert.Same(todo, result.Todo);
    }

    private sealed class FakeTodoRepository : ITodoRepository
    {
        public TodoModel? LastAdded { get; private set; }

        public Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken)
        {
            LastAdded = todo;
            return Task.FromResult(todo);
        }
    }
}
