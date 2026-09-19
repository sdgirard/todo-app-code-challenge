namespace TodoApi.Todos.Dtos;

public sealed record TodoResponse(
    Guid Id,
    string Title,
    string? Description,
    DateTime? DueDate,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
