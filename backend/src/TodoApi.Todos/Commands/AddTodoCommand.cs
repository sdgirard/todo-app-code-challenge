using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Commands;

public sealed record AddTodoCommand(TodoModel Todo);

public sealed record AddTodoCommandResult(TodoModel Todo);

public interface IAddTodoCommandHandler : ICommandHandler<AddTodoCommand, AddTodoCommandResult>;

public sealed class AddTodoCommandHandler(ITodoRepository repository) : IAddTodoCommandHandler
{
    public async Task<AddTodoCommandResult> HandleAsync(
        AddTodoCommand command,
        CancellationToken cancellationToken)
    {
        var saved = await repository.AddAsync(command.Todo, cancellationToken);
        return new AddTodoCommandResult(saved);
    }
}
