using InventoryStore.App.Modules;
using InventoryStore.Application.Services;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.App.Api;

// Wraps every /api/v1 endpoint: checks the module is on, authenticates the API key (never a
// cookie, so callers are never redirected to the login page), and turns exceptions into the
// standard error envelope.
internal sealed class PublicApiFilter : IEndpointFilter
{
    internal const string KeyNameItem = "api.keyName";
    private const string HeaderName = "X-Api-Key";
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var sp   = http.RequestServices;

        if (!await sp.GetRequiredService<IModuleRegistry>().IsEnabledAsync("api"))
            return PublicApi.Error(StatusCodes.Status404NotFound, "API_DISABLED",
                "The Public API is turned off. An administrator can enable it under Settings > Modules.");

        var rawKey = ReadKey(http.Request);
        if (rawKey is null)
            return PublicApi.Error(StatusCodes.Status401Unauthorized, "MISSING_API_KEY",
                "No API key supplied. Send it in the X-Api-Key header or as a Bearer token.");

        var repo = sp.GetRequiredService<IApiKeyRepository>();
        var key  = await repo.GetByHashAsync(ApiKeyCrypto.Hash(rawKey));
        if (key is null)
            return PublicApi.Error(StatusCodes.Status401Unauthorized, "INVALID_API_KEY",
                "The API key is not valid. It may have been rotated or deleted.");
        if (!key.Enabled)
            return PublicApi.Error(StatusCodes.Status403Forbidden, "API_KEY_DISABLED",
                "This API key has been disabled by an administrator.");

        if (key.LastUsedAt is not { } last || DateTime.UtcNow - last > TouchInterval)
            await repo.TouchLastUsedAsync(key.Id);

        http.Items[KeyNameItem] = key.Name;

        try
        {
            return await next(context);
        }
        catch (InsufficientQuantityException ex)
        {
            return PublicApi.Error(StatusCodes.Status409Conflict, "INSUFFICIENT_QUANTITY",
                $"Only {ex.Available} in stock. Cannot consume {ex.Requested}.", ex.Requested, ex.Available);
        }
        catch (KeyNotFoundException)
        {
            return PublicApi.Error(StatusCodes.Status404NotFound, "ITEM_NOT_FOUND", "No item with that id or SKU exists.");
        }
        catch (InvalidOperationException ex)
        {
            return PublicApi.Error(StatusCodes.Status409Conflict, "CONFLICT", ex.Message);
        }
        catch (Exception ex)
        {
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("PublicApi")
                .LogError(ex, "Unhandled error in Public API request {Path}", http.Request.Path);
            return PublicApi.Error(StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                "Something went wrong on the server. Check the server log.");
        }
    }

    private static string? ReadKey(HttpRequest request)
    {
        var header = request.Headers[HeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(header)) return header.Trim();

        var auth = request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (auth.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            var token = auth[bearer.Length..].Trim();
            if (token.Length > 0) return token;
        }
        return null;
    }
}
