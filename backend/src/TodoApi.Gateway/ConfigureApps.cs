using Scalar.AspNetCore;

namespace TodoApi.Gateway;

public static class ConfigureApps
{
    public static WebApplication Configure(this WebApplication app)
    {
        app.UseHttpsRedirection();

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

        return app;
    }
}
