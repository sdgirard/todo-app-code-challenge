using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TodoApi.Todos.Commands;
using TodoApi.Todos.Dtos;

namespace TodoApi.Todos.Endpoints;

public sealed class AddTodoEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/", Handle)
            .WithName("AddTodo")
            .WithSummary("Add a new to-do item")
            .AddEndpointFilter<ValidationFilter<Request>>();

    public sealed record Request(string Title, string? Description, DateTime? DueDate);

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

    private static async Task<Created<TodoResponse>> Handle(
        [FromBody] Request request,
        [FromServices] IAddTodoCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var model = request.ToModel();
        var result = await handler.HandleAsync(new AddTodoCommand(model), cancellationToken);
        var response = result.Todo.ToResponse();
        return TypedResults.Created($"/todos/{response.Id}", response);
    }
}
