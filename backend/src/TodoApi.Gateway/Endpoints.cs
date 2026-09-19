namespace TodoApi.Gateway;

public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        VersionEndpoint.Map(app);

        app.MapGroup("/todos")
            .WithTags("Todos");
    }
}
