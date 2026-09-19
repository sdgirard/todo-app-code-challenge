using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TodoApi.Todos.Commands;

namespace TodoApi.Todos.Endpoints;

public sealed class DeleteTodoEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteTodo")
            .WithSummary("Delete a to-do item by id");

    private static async Task<Results<NoContent, NotFound>> Handle(
        Guid id,
        [FromServices] IDeleteTodoCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteTodoCommand(id), cancellationToken);

        if (!result.Deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
