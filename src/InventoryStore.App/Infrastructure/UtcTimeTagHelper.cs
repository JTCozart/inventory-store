using System.Globalization;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace InventoryStore.App.Infrastructure;

/// <summary>
/// Renders a server-side UTC timestamp for the browser to localize. Emits
/// &lt;time class="js-dt" datetime="…Z" data-fmt="…"&gt; that site.js hydrates into the configured
/// display zone. The element's text is a UTC fallback (used only before hydration / without JS);
/// the browser is the single authority that applies the time zone.
///
/// Usage: &lt;utc-time value="@item.CreatedAt" fmt="datetime" /&gt;
/// Formats: "date" (default), "datetime", "datetime-short".
/// </summary>
[HtmlTargetElement("utc-time")]
public sealed class UtcTimeTagHelper : TagHelper
{
    public DateTime? Value { get; set; }
    public string Fmt { get; set; } = "date";

    /// <summary>Text shown when Value is null (e.g. "Never").</summary>
    public string Empty { get; set; } = "—";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "time";

        if (Value is null)
        {
            output.Content.SetContent(Empty);
            return;
        }

        var utc = DateTime.SpecifyKind(Value.Value, DateTimeKind.Utc);
        output.Attributes.SetAttribute("datetime", utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        output.Attributes.SetAttribute("data-fmt", Fmt);

        // Preserve any class the caller set; add the hydration hook.
        var existing = output.Attributes["class"]?.Value?.ToString();
        output.Attributes.SetAttribute("class", string.IsNullOrEmpty(existing) ? "js-dt" : $"{existing} js-dt");

        // UTC fallback text (replaced by site.js on load).
        output.Content.SetContent(utc.ToString(NetFormat(Fmt), CultureInfo.InvariantCulture));
    }

    private static string NetFormat(string fmt) => fmt switch
    {
        "datetime"       => "MMM d, yyyy h:mm tt",
        "datetime-short" => "MMM d, h:mm tt",
        _                => "MMM d, yyyy",
    };
}
