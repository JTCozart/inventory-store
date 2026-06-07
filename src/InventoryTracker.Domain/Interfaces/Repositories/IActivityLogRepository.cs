using InventoryTracker.Domain.Entities;

namespace InventoryTracker.Domain.Interfaces.Repositories;

public interface IActivityLogRepository
{
    Task<IEnumerable<ActivityLog>> GetRecentAsync(int count = 20);
    Task<IEnumerable<ActivityLog>> GetAllAsync(int page = 1, int pageSize = 50);
    Task<IEnumerable<ActivityLog>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc);
    Task<ActivityLog> CreateAsync(ActivityLog log);
    Task<int> CountAsync();
}
