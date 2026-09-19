using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Commands;

public sealed record DeleteTodoCommand(Guid Id);

public sealed record DeleteTodoCommandResult(bool Deleted);

public interface IDeleteTodoCommandHandler : ICommandHandler<DeleteTodoCommand, DeleteTodoCommandResult>;

public sealed class DeleteTodoCommandHandler(ITodoRepository repository) : IDeleteTodoCommandHandler
{
    public async Task<DeleteTodoCommandResult> HandleAsync(
        DeleteTodoCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetTrackedByIdAsync(command.Id, cancellationToken);

        if (existing is null)
        {
            return new DeleteTodoCommandResult(Deleted: false);
        }

        await repository.DeleteAsync(existing, cancellationToken);
        return new DeleteTodoCommandResult(Deleted: true);
    }
}
