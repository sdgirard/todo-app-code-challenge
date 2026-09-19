using TodoApi.Todos.Dtos;
using TodoApi.Todos.Endpoints;
using TodoApi.Todos.Models;

namespace TodoApi.Todos;

public static class TodoMapping
{
    public static TodoModel ToModel(this AddTodoEndpoint.Request request) => new()
    {
        Id = Guid.NewGuid(),
        Title = request.Title,
        Description = request.Description,
        DueDate = request.DueDate,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = null,
    };

    public static void ApplyTo(this UpdateTodoEndpoint.Request request, TodoModel model)
    {
        model.Title = request.Title;
        model.Description = request.Description;
        model.DueDate = request.DueDate;
        model.IsCompleted = request.IsCompleted;
        model.UpdatedAt = DateTime.UtcNow;
    }

    public static TodoResponse ToResponse(this TodoModel model) => new(
        model.Id,
        model.Title,
        model.Description,
        model.DueDate,
        model.IsCompleted,
        model.CreatedAt,
        model.UpdatedAt);
}
