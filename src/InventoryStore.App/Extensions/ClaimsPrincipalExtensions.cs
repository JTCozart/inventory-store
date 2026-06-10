using System.Security.Claims;

namespace InventoryStore.App.Extensions;

internal static class ClaimsPrincipalExtensions
{
    // Holds the user's login handle (User.Username). Kept separate from ClaimTypes.Name, which
    // carries the friendly DisplayName used in the UI greeting.
    internal const string UsernameClaimType = "username";

    // Returns (userId, username) where username is the canonical login handle. Audit logs and
    // credential checks use this, so they are consistent regardless of whether a display name is set.
    internal static (int userId, string username) GetIdentity(this ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("NameIdentifier claim is missing from the authenticated principal.");
        if (!int.TryParse(idStr, out var id))
            throw new InvalidOperationException($"NameIdentifier claim '{idStr}' is not a valid integer.");
        // Prefer the dedicated username claim; fall back to Name for cookies issued before it existed.
        var username = user.FindFirstValue(UsernameClaimType)
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? "Unknown";
        return (id, username);
    }
}
