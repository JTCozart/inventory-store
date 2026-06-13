using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class TagRepository : ITagRepository
{
    private readonly AppDbContext _context;

    public TagRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Tag>> GetAllAsync() =>
        await _context.Tags.OrderBy(t => t.Name).ToListAsync();

    public async Task<Tag?> GetByIdAsync(int id) =>
        await _context.Tags.FindAsync(id);

    public async Task<Tag?> GetByNameAsync(string name) =>
        await _context.Tags
            .FirstOrDefaultAsync(t => t.Name.ToLower() == name.ToLower());

    public async Task<Tag> CreateAsync(Tag tag)
    {
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();
        return tag;
    }

    public async Task UpdateAsync(Tag tag)
    {
        _context.Tags.Update(tag);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var tag = await _context.Tags.FindAsync(id);
        if (tag is not null)
        {
            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Tag>> GetOrCreateByNamesAsync(IEnumerable<string> names)
    {
        // Normalise: trim, drop blanks, dedupe case-insensitively.
        var wanted = names
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0)
            .GroupBy(n => n.ToLower())
            .Select(g => g.First())
            .ToList();

        if (wanted.Count == 0) return new List<Tag>();

        var lowered = wanted.Select(n => n.ToLower()).ToHashSet();
        var existing = await _context.Tags
            .Where(t => lowered.Contains(t.Name.ToLower()))
            .ToListAsync();

        var result = new List<Tag>(existing);
        var have = existing.Select(t => t.Name.ToLower()).ToHashSet();

        foreach (var name in wanted)
        {
            if (have.Contains(name.ToLower())) continue;
            var tag = new Tag { Name = name };
            _context.Tags.Add(tag);
            result.Add(tag);
        }

        await _context.SaveChangesAsync();
        return result;
    }
}
