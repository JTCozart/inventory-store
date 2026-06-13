using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly AppDbContext _context;

    public ClientRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Client>> GetAllAsync()
        => await _context.Clients.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync();

    public async Task<Client?> GetByIdAsync(int id)
        => await _context.Clients.FindAsync(id);

    public async Task<IEnumerable<Client>> SearchAsync(string query)
    {
        var lower = query.ToLower();
        return await _context.Clients
            .Where(c => c.FirstName.ToLower().Contains(lower)
                     || (c.LastName  != null && c.LastName.ToLower().Contains(lower))
                     || (c.Phone     != null && c.Phone.Contains(lower)))
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    public async Task<Client?> GetByNameAsync(string firstName, string? lastName)
    {
        var first = firstName.ToLower();
        var last  = lastName?.ToLower();
        return await _context.Clients.FirstOrDefaultAsync(c =>
            c.FirstName.ToLower() == first
            && ((last == null && c.LastName == null)
                || (c.LastName != null && c.LastName.ToLower() == last)));
    }

    public async Task<Client> CreateAsync(Client client)
    {
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return client;
    }

    public async Task UpdateAsync(Client client)
    {
        _context.Clients.Update(client);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var c = await _context.Clients.FindAsync(id);
        if (c is not null) { _context.Clients.Remove(c); await _context.SaveChangesAsync(); }
    }
}
