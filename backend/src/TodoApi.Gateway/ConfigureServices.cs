using TodoApi.Todos.Extensions;

namespace TodoApi.Gateway;

public static class ConfigureServices
{
    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddTodosServices();

        return builder;
    }
}
