using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class WebhookEndpointRepository : IWebhookEndpointRepository
{
    private readonly AppDbContext _context;

    public WebhookEndpointRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<WebhookEndpoint>> GetAllAsync() =>
        await _context.WebhookEndpoints.OrderBy(w => w.Id).ToListAsync();

    public async Task<IEnumerable<WebhookEndpoint>> GetEnabledAsync() =>
        await _context.WebhookEndpoints.Where(w => w.Enabled).ToListAsync();

    public async Task<WebhookEndpoint?> GetByIdAsync(int id) =>
        await _context.WebhookEndpoints.FindAsync(id);

    public async Task<WebhookEndpoint> CreateAsync(WebhookEndpoint endpoint)
    {
        _context.WebhookEndpoints.Add(endpoint);
        await _context.SaveChangesAsync();
        return endpoint;
    }

    public async Task UpdateAsync(WebhookEndpoint endpoint)
    {
        _context.WebhookEndpoints.Update(endpoint);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var row = await _context.WebhookEndpoints.FindAsync(id);
        if (row is not null)
        {
            _context.WebhookEndpoints.Remove(row);
            await _context.SaveChangesAsync();
        }
    }
}
