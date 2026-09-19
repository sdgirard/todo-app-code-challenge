using Microsoft.AspNetCore.OpenApi;
using TodoApi.Todos.Extensions;

namespace TodoApi.Gateway;

public static class ConfigureServices
{
    internal const string FrontendCorsPolicy = "Frontend";

    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
            options.CreateSchemaReferenceId = CreateNestedTypeAwareSchemaReferenceId);
        builder.Services.AddTodosServices(builder.Configuration);
        builder.Services.AddFrontendCors(builder.Configuration);

        return builder;
    }

    // Every endpoint declares its own nested Request/Response records (see
    // docs/architecture/backend/overview.md#endpoint-pattern), so the default schema-id
    // generator — which uses only the short type name — collapses AddTodoEndpoint.Response,
    // ListTodosEndpoint.Response, and VersionEndpoint.Response into one "Response" schema,
    // silently dropping two of the three from the emitted spec (and from the orval-generated
    // TypeScript client). Prefixing with the declaring type disambiguates them.
    private static string? CreateNestedTypeAwareSchemaReferenceId(System.Text.Json.Serialization.Metadata.JsonTypeInfo typeInfo)
    {
        var type = typeInfo.Type;
        return type.DeclaringType is { } declaringType
            ? $"{declaringType.Name}{type.Name}"
            : OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);
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
