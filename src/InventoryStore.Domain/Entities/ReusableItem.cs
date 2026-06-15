namespace InventoryStore.Domain.Entities;

public class ReusableItem : InventoryItem
{
    public int CheckedOutCount { get; set; }
    public int LostCount { get; set; }

    // Units currently away being serviced (Maintenance module). Like checked-out units, they
    // are unavailable until returned.
    public int OutForMaintenanceCount { get; set; }

    public override int AvailableQuantity =>
        Math.Max(0, Quantity - CheckedOutCount - LostCount - OutForMaintenanceCount);
}
