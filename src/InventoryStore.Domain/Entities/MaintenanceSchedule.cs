using InventoryStore.Domain.Enums;

namespace InventoryStore.Domain.Entities;

// A per-item maintenance schedule (1:1 with an InventoryItem, like ItemCost). Records when the
// item was last serviced and how often it should be serviced, from which the next-due date follows.
public class MaintenanceSchedule
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public DateOnly? LastMaintainedDate { get; set; }
    public int IntervalValue { get; set; }
    public MaintenanceIntervalUnit IntervalUnit { get; set; } = MaintenanceIntervalUnit.Months;
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Next service date = last maintained + interval. Null when there is no last date or no interval.
    public DateOnly? NextDueDate
    {
        get
        {
            if (LastMaintainedDate is not { } last || IntervalValue <= 0) return null;
            return IntervalUnit switch
            {
                MaintenanceIntervalUnit.Days   => last.AddDays(IntervalValue),
                MaintenanceIntervalUnit.Months => last.AddMonths(IntervalValue),
                MaintenanceIntervalUnit.Years  => last.AddYears(IntervalValue),
                _                              => null
            };
        }
    }
}
