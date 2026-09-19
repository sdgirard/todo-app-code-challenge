using TodoApi.Todos.Endpoints;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Commands;

public sealed record UpdateTodoCommand(Guid Id, UpdateTodoEndpoint.Request Request);

public sealed record UpdateTodoCommandResult(TodoModel? Todo);

public interface IUpdateTodoCommandHandler : ICommandHandler<UpdateTodoCommand, UpdateTodoCommandResult>;

public sealed class UpdateTodoCommandHandler(ITodoRepository repository) : IUpdateTodoCommandHandler
{
    public async Task<UpdateTodoCommandResult> HandleAsync(
        UpdateTodoCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetTrackedByIdAsync(command.Id, cancellationToken);

        if (existing is null)
        {
            return new UpdateTodoCommandResult(null);
        }

        command.Request.ApplyTo(existing);
        var saved = await repository.UpdateAsync(existing, cancellationToken);
        return new UpdateTodoCommandResult(saved);
    }
}
