using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TodoApi.Todos.Persistence;

namespace TodoApi.Gateway;

public static class ConfigureApps
{
    public static WebApplication Configure(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseCors(ConfigureServices.FrontendCorsPolicy);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }
        else
        {
            app.UseHsts();
        }

        app.MapEndpoints();
        app.ApplyMigrations();

        return app;
    }

    private static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
        dbContext.Database.Migrate();
    }
}
