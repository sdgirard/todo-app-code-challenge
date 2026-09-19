using Microsoft.Extensions.DependencyInjection;

namespace TodoApi.Todos.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTodosServices(this IServiceCollection services)
    {
        return services;
    }
}
