using InventoryStore.Application.Services;
using InventoryStore.Infrastructure.Data;

namespace InventoryStore.App.Api;

// The anonymous probe the server's update timer polls after every release. A release that cannot reach its
// database never answers 200, so the previous version is restored. The force-HTTPS redirect and the first-run
// redirect both leave this path alone, because the probe is plain HTTP from the same machine.
internal static class HealthEndpoints
{
    internal const string Path = "/health";

    internal static bool IsHealthRequest(PathString path) => path.Equals(Path, StringComparison.OrdinalIgnoreCase);

    internal static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, async (AppDbContext database, HttpResponse response, CancellationToken cancel) =>
        {
            response.Headers.CacheControl = "no-store";
            return await DatabaseProbe.CanReachAsync(database.Database.CanConnectAsync, cancel)
                ? Results.Text("Healthy")
                : Results.Text("Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        });
        return endpoints;
    }
}
