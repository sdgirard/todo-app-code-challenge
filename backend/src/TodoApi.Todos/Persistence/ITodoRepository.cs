using TodoApi.Todos.Models;

namespace TodoApi.Todos.Persistence;

public interface ITodoRepository
{
    Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken);
}
