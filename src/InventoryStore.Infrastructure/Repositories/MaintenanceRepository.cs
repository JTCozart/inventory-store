using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class MaintenanceRepository : IMaintenanceRepository
{
    private readonly AppDbContext _context;

    public MaintenanceRepository(AppDbContext context) => _context = context;

    public async Task<MaintenanceSchedule?> GetScheduleAsync(int inventoryItemId)
        => await _context.MaintenanceSchedules
            .FirstOrDefaultAsync(s => s.InventoryItemId == inventoryItemId);

    public async Task<MaintenanceSchedule> UpsertScheduleAsync(MaintenanceSchedule schedule)
    {
        if (schedule.Id == 0)
            _context.MaintenanceSchedules.Add(schedule);
        else
            _context.MaintenanceSchedules.Update(schedule);
        await _context.SaveChangesAsync();
        return schedule;
    }

    public async Task<MaintenanceVisit?> GetOpenVisitAsync(int inventoryItemId)
        => await _context.MaintenanceVisits
            .Where(v => v.InventoryItemId == inventoryItemId && v.ReturnedAt == null)
            .OrderByDescending(v => v.OutForMaintenanceAt)
            .FirstOrDefaultAsync();

    public async Task<MaintenanceVisit?> GetVisitAsync(int visitId)
        => await _context.MaintenanceVisits.FindAsync(visitId);

    public async Task<IEnumerable<MaintenanceVisit>> GetVisitsByItemAsync(int inventoryItemId)
        => await _context.MaintenanceVisits
            .Where(v => v.InventoryItemId == inventoryItemId)
            .OrderByDescending(v => v.OutForMaintenanceAt)
            .ToListAsync();

    public async Task<MaintenanceVisit> CreateVisitAsync(MaintenanceVisit visit)
    {
        _context.MaintenanceVisits.Add(visit);
        await _context.SaveChangesAsync();
        return visit;
    }

    public async Task UpdateVisitAsync(MaintenanceVisit visit)
    {
        _context.MaintenanceVisits.Update(visit);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<MaintenanceSchedule>> GetAllSchedulesAsync()
        => await _context.MaintenanceSchedules.ToListAsync();

    public async Task<IReadOnlyList<MaintenanceVisit>> GetAllOpenVisitsAsync()
        => await _context.MaintenanceVisits.Where(v => v.ReturnedAt == null).ToListAsync();
}
