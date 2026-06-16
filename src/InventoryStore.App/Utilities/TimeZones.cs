namespace InventoryStore.App.Utilities;

/// <summary>
/// Provides the list of selectable time zones for Settings → General and validates a stored id.
/// Timestamps are stored in UTC and rendered in the browser, so the value persisted and handed to
/// the client is always an IANA id (e.g. "America/New_York") — the identifier the browser's
/// Intl.DateTimeFormat understands.
/// </summary>
public static class TimeZones
{
    public sealed record TzOption(string Id, string Display);

    /// <summary>Selectable zones (IANA id + friendly label), ordered west-to-east by UTC offset.</summary>
    public static IReadOnlyList<TzOption> Options { get; } = BuildOptions();

    private static List<TzOption> BuildOptions()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<(TimeSpan Offset, TzOption Option)>();

        foreach (var z in TimeZoneInfo.GetSystemTimeZones())
        {
            // On Linux the system ids are already IANA; on Windows convert them so the value is
            // portable and usable by the browser.
            var iana = z.HasIanaId
                ? z.Id
                : (TimeZoneInfo.TryConvertWindowsIdToIanaId(z.Id, out var i) ? i : null);
            if (iana is null || !seen.Add(iana)) continue;
            rows.Add((z.BaseUtcOffset, new TzOption(iana, z.DisplayName)));
        }

        return rows
            .OrderBy(r => r.Offset)
            .ThenBy(r => r.Option.Display, StringComparer.Ordinal)
            .Select(r => r.Option)
            .ToList();
    }

    /// <summary>True when <paramref name="id"/> resolves to a real time zone on this host.</summary>
    public static bool IsValid(string? id) => TryGetInfo(id, out _);

    /// <summary>
    /// Resolves a stored id to a <see cref="TimeZoneInfo"/>. Ids are portable between Windows and
    /// Linux: .NET 8 resolves both IANA and Windows ids directly, and we fall back to converting
    /// IANA→Windows when a host only knows the Windows scheme.
    /// </summary>
    public static bool TryGetInfo(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id)) return false;

        try { zone = TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (TimeZoneNotFoundException) { }
        catch (InvalidTimeZoneException) { return false; }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var win) && win is not null)
        {
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(win); return true; } catch { }
        }
        return false;
    }
}
