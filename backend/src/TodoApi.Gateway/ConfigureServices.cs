using TodoApi.Todos.Extensions;

namespace TodoApi.Gateway;

public static class ConfigureServices
{
    internal const string FrontendCorsPolicy = "Frontend";

    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddTodosServices(builder.Configuration);
        builder.Services.AddFrontendCors(builder.Configuration);

        return builder;
    }

    private static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var frontendOrigin = configuration["Frontend:Origin"] ?? "https://localhost:5173";

        services.AddCors(options =>
            options.AddPolicy(FrontendCorsPolicy, policy =>
                policy.WithOrigins(frontendOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()));

        return services;
    }
}
