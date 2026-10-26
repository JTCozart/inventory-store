using System.Text.RegularExpressions;

namespace InventoryStore.Application.Services;

/// <summary>
/// Orders release tags. Two shapes exist: the original YYYYMMDD.HHMM and the shared YY.MMDD.HHMM, which may end in
/// "-rc" for a prerelease. Every release in the new shape is newer than every release in the old one.
/// </summary>
public static partial class ReleaseVersion
{
    [GeneratedRegex(@"^(?<date>\d{8})\.(?<time>\d{4})$")]
    private static partial Regex OldShape();

    [GeneratedRegex(@"^(?<year>\d{2})\.(?<monthDay>\d{4})\.(?<time>\d{4})(-rc)?$")]
    private static partial Regex NewShape();

    public static bool IsNewer(string latest, string current)
    {
        if (!TryRank(latest, out var latestRank) || !TryRank(current, out var currentRank))
            return false;
        return string.CompareOrdinal(latestRank, currentRank) > 0;
    }

    /// <summary>A text key that sorts in release order: the shape first, then the digits, which are fixed width.</summary>
    private static bool TryRank(string tag, out string rank)
    {
        var bare = tag.Split('+')[0].Trim().TrimStart('v');

        if (NewShape().Match(bare) is { Success: true } created)
        {
            rank = "1" + created.Groups["year"].Value + created.Groups["monthDay"].Value + created.Groups["time"].Value;
            return true;
        }

        if (OldShape().Match(bare) is { Success: true } original)
        {
            rank = "0" + original.Groups["date"].Value + original.Groups["time"].Value;
            return true;
        }

        rank = "";
        return false;
    }
}
