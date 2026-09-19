using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TodoApi.Todos.Queries;

namespace TodoApi.Todos.Endpoints;

public sealed class ListTodosEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/", Handle)
            .WithName("ListTodos")
            .WithSummary("List all to-do items");

    public sealed record Response(
        Guid Id,
        string Title,
        string? Description,
        DateTime? DueDate,
        bool IsCompleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    private static async Task<Ok<IReadOnlyList<Response>>> Handle(
        [FromServices] IListTodosQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListTodosQuery(), cancellationToken);
        var response = result.Todos.Select(todo => todo.ToListResponse()).ToList();
        return TypedResults.Ok<IReadOnlyList<Response>>(response);
    }
}
