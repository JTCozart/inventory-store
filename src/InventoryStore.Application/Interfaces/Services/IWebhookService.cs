namespace InventoryStore.Application.Interfaces.Services;

public interface IWebhookService
{
    // Fire-and-forget delivery of an inventory event to all subscribed endpoints. No-ops when the
    // Webhooks module is disabled. Implementations must not throw into the caller.
    Task DispatchAsync(string eventName, object payload);

    // Send a sample payload to one endpoint and report the result (used by the Test button).
    Task<(bool ok, int status, string? error)> SendTestAsync(int endpointId);
}
