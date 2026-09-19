using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;

namespace TodoApi.Gateway;

/// <summary>
/// Reports the version baked into the assembly at build time via
/// /p:InformationalVersion (see docker/Dockerfile.gateway and
/// docs/infra/versioning.md). Gateway-level infrastructure, not a Todos
/// feature, so it lives here rather than in TodoApi.Todos.
/// </summary>
public static class VersionEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/version", Handle)
            .WithName("GetVersion")
            .WithSummary("Reports the running application's version, commit, and build date");

    public sealed record Response(string Version, string Commit, string BuildDate);

    private static Ok<Response> Handle()
    {
        // Format baked in by docker/Dockerfile.gateway: "{version}+{commit}.{buildDate}"
        // Falls back to "dev" locally (dotnet build/run with no /p:InformationalVersion set).
        var informationalVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "dev";

        var (version, commit, buildDate) = ParseInformationalVersion(informationalVersion);

        return TypedResults.Ok(new Response(version, commit, buildDate));
    }

    internal static (string Version, string Commit, string BuildDate) ParseInformationalVersion(string informationalVersion)
    {
        var versionPart = informationalVersion;
        var commit = "unknown";
        var buildDate = "unknown";

        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex < 0)
        {
            return (versionPart, commit, buildDate);
        }

        versionPart = informationalVersion[..plusIndex];
        var metadata = informationalVersion[(plusIndex + 1)..].Split('.', 2);
        commit = metadata[0];
        if (metadata.Length > 1)
        {
            buildDate = metadata[1];
        }

        return (versionPart, commit, buildDate);
    }
}
