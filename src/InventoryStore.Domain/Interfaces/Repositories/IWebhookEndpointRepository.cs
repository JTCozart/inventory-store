using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IWebhookEndpointRepository
{
    Task<IEnumerable<WebhookEndpoint>> GetAllAsync();
    Task<IEnumerable<WebhookEndpoint>> GetEnabledAsync();
    Task<WebhookEndpoint?> GetByIdAsync(int id);
    Task<WebhookEndpoint> CreateAsync(WebhookEndpoint endpoint);
    Task UpdateAsync(WebhookEndpoint endpoint);
    Task DeleteAsync(int id);
}
