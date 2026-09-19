using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TodoApi.Todos.Dtos;
using TodoApi.Todos.Queries;

namespace TodoApi.Todos.Endpoints;

public sealed class ListTodosEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/", Handle)
            .WithName("ListTodos")
            .WithSummary("List all to-do items");

    private static async Task<Ok<IReadOnlyList<TodoResponse>>> Handle(
        [FromServices] IListTodosQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListTodosQuery(), cancellationToken);
        var response = result.Todos.Select(todo => todo.ToResponse()).ToList();
        return TypedResults.Ok<IReadOnlyList<TodoResponse>>(response);
    }
}
