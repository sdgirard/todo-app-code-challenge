using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApi.Todos.Commands;
using TodoApi.Todos.Endpoints;
using TodoApi.Todos.Persistence;

namespace TodoApi.Todos.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTodosServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TodoDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("TodoDb")));

        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<IAddTodoCommandHandler, AddTodoCommandHandler>();

        services.AddValidatorsFromAssemblyContaining<AddTodoEndpoint.RequestValidator>();

        return services;
    }
}
