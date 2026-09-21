using InventoryStore.App.Modules;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace InventoryStore.App.Api;

internal sealed record CreateApiKeyInput(string? Name);
internal sealed record RotateApiKeyInput(int GraceHours = 0);
internal sealed record SetApiKeyEnabledInput(bool Enabled);

// Admin-only key management for Settings > Modules > Public API. These are ordinary cookie-auth
// endpoints (not part of the documented /api/v1 surface). The raw key is returned exactly once,
// by create and rotate; only its hash is stored.
internal static class ApiKeyEndpoints
{
    private const int MaxNameLength = 60;
    private const int MaxGraceHours = 24 * 30;

    internal static IEndpointRouteBuilder MapApiKeyManagement(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/apikeys", [Authorize(Roles = "Admin")] async (
            IModuleRegistry modules, IApiKeyRepository repo) =>
        {
            if (!await modules.IsEnabledAsync("api")) return Results.NotFound();
            var now = DateTime.UtcNow;
            var rows = await repo.GetAllAsync();
            return Results.Ok(rows.Select(k => new
            {
                k.Id, k.Name, k.Prefix, k.Enabled, k.CreatedAt, k.LastUsedAt, k.RotatedAt,
                graceActive = k.GraceActive(now),
                graceExpiresAt = k.GraceActive(now) ? k.PreviousKeyExpiresAt : null
            }));
        });

        endpoints.MapPost("/api/apikeys", [Authorize(Roles = "Admin")] async (
            CreateApiKeyInput body, IModuleRegistry modules, IApiKeyRepository repo) =>
        {
            if (!await modules.IsEnabledAsync("api")) return Results.NotFound();
            var name = body.Name?.Trim();
            if (string.IsNullOrEmpty(name))
                return Results.BadRequest(new { error = "Give the key a name so you can tell it apart later." });
            if (name.Length > MaxNameLength)
                return Results.BadRequest(new { error = $"Name must be {MaxNameLength} characters or fewer." });

            var (key, hash, prefix) = ApiKeyCrypto.Generate();
            var created = await repo.CreateAsync(new ApiKey
            {
                Name = name, KeyHash = hash, Prefix = prefix, Enabled = true, CreatedAt = DateTime.UtcNow
            });
            return Results.Ok(new { success = true, created.Id, key });
        });

        endpoints.MapPost("/api/apikeys/{id:int}/rotate", [Authorize(Roles = "Admin")] async (
            int id, RotateApiKeyInput body, IModuleRegistry modules, IApiKeyRepository repo) =>
        {
            if (!await modules.IsEnabledAsync("api")) return Results.NotFound();
            var grace = TimeSpan.FromHours(Math.Clamp(body.GraceHours, 0, MaxGraceHours));

            var (key, hash, prefix) = ApiKeyCrypto.Generate();
            if (!await repo.RotateAsync(id, hash, prefix, grace))
                return Results.NotFound(new { error = "Key not found." });
            return Results.Ok(new
            {
                success = true, id, key,
                oldKeyValidUntil = grace > TimeSpan.Zero ? DateTime.UtcNow + grace : (DateTime?)null
            });
        });

        endpoints.MapPost("/api/apikeys/{id:int}/enabled", [Authorize(Roles = "Admin")] async (
            int id, SetApiKeyEnabledInput body, IModuleRegistry modules, IApiKeyRepository repo) =>
        {
            if (!await modules.IsEnabledAsync("api")) return Results.NotFound();
            await repo.SetEnabledAsync(id, body.Enabled);
            return Results.Ok(new { success = true });
        });

        endpoints.MapPost("/api/apikeys/{id:int}/delete", [Authorize(Roles = "Admin")] async (
            int id, IModuleRegistry modules, IApiKeyRepository repo) =>
        {
            if (!await modules.IsEnabledAsync("api")) return Results.NotFound();
            await repo.DeleteAsync(id);
            return Results.Ok(new { success = true });
        });

        return endpoints;
    }
}
