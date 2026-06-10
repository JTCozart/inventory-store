namespace InventoryStore.App.Modules;

// Maps GHS pictogram names (as stored by the SDS module, e.g. "Flammable", "Corrosive") to the
// bundled standard UN symbol files in wwwroot/img/ghs. Used to show hazard pictograms on the
// inventory list and scan card when the SDS module is enabled.
public static class GhsPictograms
{
    // Ordered so more specific names match before generic ones.
    private static readonly (string Match, string Code)[] _map =
    {
        ("explos",        "GHS01"),
        ("flam",          "GHS02"),
        ("oxidi",         "GHS03"),
        ("compressed gas","GHS04"),
        ("gas cylinder",  "GHS04"),
        ("corros",        "GHS05"),
        ("acute tox",     "GHS06"),
        ("toxic",         "GHS06"),
        ("environ",       "GHS09"),
        ("health",        "GHS08"),
        ("irritant",      "GHS07"),
        ("harmful",       "GHS07"),
    };

    // Returns the web path to the pictogram image, or null if the name isn't recognized.
    public static string? ImagePath(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var (match, code) in _map)
            if (name.Contains(match, StringComparison.OrdinalIgnoreCase))
                return $"/img/ghs/{code}.svg";
        return null;
    }

    // Splits a stored "A; B; C" pictogram string into individual names.
    public static IEnumerable<string> Split(string? pictograms) =>
        string.IsNullOrWhiteSpace(pictograms)
            ? []
            : pictograms.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
