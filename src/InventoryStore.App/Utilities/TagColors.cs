namespace InventoryStore.App.Utilities;

// Auto-colours a tag from its name. Tags have no user-chosen colour; the colour is derived
// deterministically so a tag looks identical wherever it appears. This MUST stay in sync with
// the tagColor() implementation in wwwroot/js/tag-input.js (same palette and hash).
public static class TagColors
{
    private static readonly string[] Palette =
    {
        "#0d6efd", "#198754", "#dc3545", "#ffc107", "#fd7e14", "#6f42c1",
        "#d63384", "#0dcaf0", "#20c997", "#6c757d", "#212529"
    };

    private static readonly HashSet<string> DarkText = new(StringComparer.OrdinalIgnoreCase)
        { "#ffc107", "#0dcaf0", "#20c997" };

    public static string For(string? name)
    {
        var s = (name ?? string.Empty).ToLowerInvariant();
        int h = 0;
        unchecked
        {
            foreach (var c in s) h = h * 31 + c;
        }
        var idx = (int)(Math.Abs((long)h) % Palette.Length);
        return Palette[idx];
    }

    public static bool NeedsDarkText(string? name) => DarkText.Contains(For(name));
}
