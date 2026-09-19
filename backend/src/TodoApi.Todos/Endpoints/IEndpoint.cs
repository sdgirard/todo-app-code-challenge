using Microsoft.AspNetCore.Routing;

namespace TodoApi.Todos.Endpoints;

public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder app);
}
