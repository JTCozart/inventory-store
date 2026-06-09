using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class ActivityLogRepository : IActivityLogRepository
{
    private readonly AppDbContext _context;

    public ActivityLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ActivityLog>> GetRecentAsync(int count = 20) =>
        await _context.ActivityLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync();

    public async Task<IEnumerable<ActivityLog>> GetAllAsync(int page = 1, int pageSize = 50) =>
        await _context.ActivityLogs
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<IEnumerable<ActivityLog>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc) =>
        await _context.ActivityLogs
            .Where(l => l.Timestamp >= fromUtc && l.Timestamp <= toUtc)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

    public async Task<ActivityLog> CreateAsync(ActivityLog log)
    {
        log.Timestamp = DateTime.UtcNow;
        _context.ActivityLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public Task<int> CountAsync() => _context.ActivityLogs.CountAsync();
}
