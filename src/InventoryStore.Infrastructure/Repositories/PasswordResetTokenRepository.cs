using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AppDbContext _context;

    public PasswordResetTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PasswordResetToken> CreateAsync(PasswordResetToken token)
    {
        _context.PasswordResetTokens.Add(token);
        await _context.SaveChangesAsync();
        return token;
    }

    public async Task<PasswordResetToken?> GetValidByHashAsync(string tokenHash) =>
        await _context.PasswordResetTokens.FirstOrDefaultAsync(t =>
            t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow);

    public async Task MarkUsedAsync(int id)
    {
        var token = await _context.PasswordResetTokens.FindAsync(id);
        if (token is not null)
        {
            token.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task InvalidateAllForUserAsync(int userId)
    {
        var tokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ToListAsync();
        foreach (var t in tokens)
            t.UsedAt = DateTime.UtcNow;
        if (tokens.Count > 0)
            await _context.SaveChangesAsync();
    }
}
