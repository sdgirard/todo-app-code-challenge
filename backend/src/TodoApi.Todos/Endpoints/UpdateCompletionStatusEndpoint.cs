using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TodoApi.Todos.Commands;
using TodoApi.Todos.Dtos;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Endpoints;

public sealed class UpdateCompletionStatusEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch("/{id:guid}", Handle)
            .WithName("UpdateCompletionStatus")
            .WithSummary("Mark a to-do item as completed or not completed")
            .AddEndpointFilter<ValidationFilter<Request>>();

    public sealed record Request(bool IsCompleted);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.IsCompleted).NotNull();
        }
    }

    private static async Task<Results<Ok<TodoResponse>, NotFound>> Handle(
        Guid id,
        [FromBody] Request request,
        [FromServices] ITodoRepository repository,
        [FromServices] IUpdateTodoCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);

        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        var updateRequest = new UpdateTodoEndpoint.Request(
            existing.Title,
            existing.Description,
            existing.DueDate,
            request.IsCompleted);

        var result = await handler.HandleAsync(new UpdateTodoCommand(id, updateRequest), cancellationToken);

        return TypedResults.Ok(result.Todo!.ToResponse());
    }
}
