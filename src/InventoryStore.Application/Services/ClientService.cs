using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class ClientService : IClientService
{
    private readonly IClientRepository _repo;

    public ClientService(IClientRepository repo) => _repo = repo;

    public async Task<IEnumerable<ClientDto>> GetAllAsync()
        => (await _repo.GetAllAsync()).Select(Map);

    public async Task<ClientDto?> GetByIdAsync(int id)
    {
        var c = await _repo.GetByIdAsync(id);
        return c is null ? null : Map(c);
    }

    public async Task<IEnumerable<ClientDto>> SearchAsync(string query)
        => (await _repo.SearchAsync(query)).Select(Map);

    public async Task<ClientDto> CreateAsync(CreateClientDto dto)
    {
        var client = new Client
        {
            FirstName   = dto.FirstName.Trim(),
            LastName    = dto.LastName?.Trim(),
            Phone       = dto.Phone?.Trim(),
            Email       = dto.Email?.Trim(),
            DateOfBirth = dto.DateOfBirth,
            Address     = dto.Address?.Trim(),
            Notes       = dto.Notes?.Trim(),
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };
        return Map(await _repo.CreateAsync(client));
    }

    public async Task<ClientDto> QuickCreateAsync(string name)
    {
        var parts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var client = new Client
        {
            FirstName = parts[0],
            LastName  = parts.Length > 1 ? parts[1] : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return Map(await _repo.CreateAsync(client));
    }

    public async Task UpdateAsync(int id, UpdateClientDto dto)
    {
        var client = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Client {id} not found.");
        client.FirstName   = dto.FirstName.Trim();
        client.LastName    = dto.LastName?.Trim();
        client.Phone       = dto.Phone?.Trim();
        client.Email       = dto.Email?.Trim();
        client.DateOfBirth = dto.DateOfBirth;
        client.Address     = dto.Address?.Trim();
        client.Notes       = dto.Notes?.Trim();
        client.UpdatedAt   = DateTime.UtcNow;
        await _repo.UpdateAsync(client);
    }

    public async Task DeleteAsync(int id) => await _repo.DeleteAsync(id);

    private static ClientDto Map(Client c) => new(
        c.Id, c.FirstName, c.LastName, c.Phone, c.Email, c.DateOfBirth,
        c.Address, c.Notes, c.CreatedAt, c.UpdatedAt, c.DisplayName);
}
