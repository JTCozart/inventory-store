using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken> CreateAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetValidByHashAsync(string tokenHash);
    Task MarkUsedAsync(int id);
    Task InvalidateAllForUserAsync(int userId);
}
