using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TodoApi.Todos.Commands;
using TodoApi.Todos.Dtos;

namespace TodoApi.Todos.Endpoints;

public sealed class UpdateTodoEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/{id:guid}", Handle)
            .WithName("UpdateTodo")
            .WithSummary("Update an existing to-do item's title, description, due date, and completion status")
            .AddEndpointFilter<ValidationFilter<Request>>();

    public sealed record Request(string Title, string? Description, DateTime? DueDate, bool IsCompleted);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
            RuleFor(x => x.DueDate)
                .GreaterThanOrEqualTo(DateTime.UtcNow.Date)
                .When(x => x.DueDate is not null)
                .WithMessage("DueDate must not be in the past.");
        }
    }

    private static async Task<Results<Ok<TodoResponse>, NotFound>> Handle(
        Guid id,
        [FromBody] Request request,
        [FromServices] IUpdateTodoCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new UpdateTodoCommand(id, request), cancellationToken);

        if (result.Todo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(result.Todo.ToResponse());
    }
}
