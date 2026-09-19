using TodoApi.Todos.Models;

namespace TodoApi.Todos.Persistence;

public sealed class TodoRepository(TodoDbContext dbContext) : ITodoRepository
{
    public async Task<TodoModel> AddAsync(TodoModel todo, CancellationToken cancellationToken)
    {
        dbContext.Todos.Add(todo);
        await dbContext.SaveChangesAsync(cancellationToken);
        return todo;
    }
}
