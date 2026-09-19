using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Queries;

public sealed record ListTodosQuery;

public sealed record ListTodosQueryResult(IReadOnlyList<TodoModel> Todos);

public interface IListTodosQueryHandler : IQueryHandler<ListTodosQuery, ListTodosQueryResult>;

public sealed class ListTodosQueryHandler(ITodoRepository repository) : IListTodosQueryHandler
{
    public async Task<ListTodosQueryResult> HandleAsync(
        ListTodosQuery query,
        CancellationToken cancellationToken)
    {
        var todos = await repository.ListAsync(cancellationToken);
        return new ListTodosQueryResult(todos);
    }
}
