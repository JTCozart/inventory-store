namespace InventoryStore.Domain.Entities;

// A configured outbound webhook target for the Webhooks & Integrations module. Global config
// (not item-owned). When Secret is set, deliveries are signed with HMAC-SHA256.
public class WebhookEndpoint
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Events { get; set; } = "all"; // csv of event names, or "all"
    public string? Secret { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? LastStatus { get; set; }
    public DateTime? LastSentAt { get; set; }

    public bool Subscribes(string eventName) =>
        string.Equals(Events, "all", StringComparison.OrdinalIgnoreCase)
        || Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Any(e => string.Equals(e, eventName, StringComparison.OrdinalIgnoreCase));
}
