using System.Reflection;
using System.Text.RegularExpressions;

namespace InventoryStore.App.Ai;

public sealed record HelpSection(string Id, string Title, string Text);

// Turns the same docs/user-guide.html that ships as the public user guide into a searchable
// knowledge base for the chat assistant's "how do I..." questions -- one source of truth, so
// the guide and the assistant's answers can't drift apart. Parsed once from the embedded
// resource (see the EmbeddedResource item in InventoryStore.App.csproj) and cached for the
// life of the process; the guide is static content bundled at build time, not something that
// changes at runtime.
public static class HelpKnowledgeBase
{
    private const string ResourceName = "InventoryStore.App.Ai.user-guide.html";

    private static readonly Lazy<IReadOnlyList<HelpSection>> Sections = new(Parse);

    // Simple keyword-overlap scoring: words that appear in the section title count for more
    // than words in the body. Good enough for a few dozen short, topically-distinct sections --
    // no need for a real search index here.
    public static List<HelpSection> Search(string query, int maxResults = 2)
    {
        var words = Regex.Matches(query.ToLowerInvariant(), @"[a-z0-9]{3,}")
            .Select(m => m.Value)
            .Distinct()
            .ToList();
        if (words.Count == 0) return [];

        return Sections.Value
            .Select(s => (Section: s, Score: Score(s, words)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Section)
            .ToList();
    }

    private static int Score(HelpSection section, List<string> words)
    {
        var title = section.Title.ToLowerInvariant();
        var body = section.Text.ToLowerInvariant();
        var score = 0;
        foreach (var w in words)
        {
            if (title.Contains(w)) score += 5;
            score += CountOccurrences(body, w);
        }
        return score;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static List<HelpSection> Parse()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();

        var sections = new List<HelpSection>();
        foreach (Match m in Regex.Matches(html, @"<section\s+id=""([^""]+)""[^>]*>(.*?)</section>", RegexOptions.Singleline))
        {
            var id = m.Groups[1].Value;
            var inner = m.Groups[2].Value;

            var titleMatch = Regex.Match(inner, @"<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline);
            var title = titleMatch.Success ? StripHtml(titleMatch.Value) : id;

            var body = titleMatch.Success ? inner.Remove(titleMatch.Index, titleMatch.Length) : inner;
            var text = StripHtml(body);
            if (text.Length > 2500) text = text[..2500] + "…";

            sections.Add(new HelpSection(id, title, text));
        }

        return sections;
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
