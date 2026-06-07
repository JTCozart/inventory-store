namespace InventoryTracker.Domain.Entities;

public class ReusableItem : InventoryItem
{
    public int CheckedOutCount { get; set; }
    public int LostCount { get; set; }

    public override int AvailableQuantity => Math.Max(0, Quantity - CheckedOutCount - LostCount);
}
