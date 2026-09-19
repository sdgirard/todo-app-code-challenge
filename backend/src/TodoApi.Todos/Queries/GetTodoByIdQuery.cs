using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Queries;

public sealed record GetTodoByIdQuery(Guid Id);

public sealed record GetTodoByIdQueryResult(TodoModel? Todo);

public interface IGetTodoByIdQueryHandler : IQueryHandler<GetTodoByIdQuery, GetTodoByIdQueryResult>;

public sealed class GetTodoByIdQueryHandler(ITodoRepository repository) : IGetTodoByIdQueryHandler
{
    public async Task<GetTodoByIdQueryResult> HandleAsync(
        GetTodoByIdQuery query,
        CancellationToken cancellationToken)
    {
        var todo = await repository.GetByIdAsync(query.Id, cancellationToken);
        return new GetTodoByIdQueryResult(todo);
    }
}
