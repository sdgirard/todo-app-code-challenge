using TodoApi.Todos.Endpoints;

namespace TodoApi.Gateway;

public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        VersionEndpoint.Map(app);

        var todos = app.MapGroup("/todos")
            .WithTags("Todos");

        todos
            .MapEndpoint<AddTodoEndpoint>()
            .MapEndpoint<ListTodosEndpoint>();
    }

    private static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app)
        where TEndpoint : IEndpoint
    {
        TEndpoint.Map(app);
        return app;
    }
}
