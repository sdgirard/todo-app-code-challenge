using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TodoApi.Todos.Dtos;
using TodoApi.Todos.Queries;

namespace TodoApi.Todos.Endpoints;

public sealed class GetTodoByIdEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/{id:guid}", Handle)
            .WithName("GetTodoById")
            .WithSummary("Get a single to-do item by id");

    private static async Task<Results<Ok<TodoResponse>, NotFound>> Handle(
        Guid id,
        [FromServices] IGetTodoByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTodoByIdQuery(id), cancellationToken);

        if (result.Todo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(result.Todo.ToResponse());
    }
}
