using System.Security.Claims;

namespace InventoryStore.App.Extensions;

internal static class ClaimsPrincipalExtensions
{
    internal static (int userId, string username) GetIdentity(this ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("NameIdentifier claim is missing from the authenticated principal.");
        if (!int.TryParse(idStr, out var id))
            throw new InvalidOperationException($"NameIdentifier claim '{idStr}' is not a valid integer.");
        var name = user.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
        return (id, name);
    }
}
