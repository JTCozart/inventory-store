namespace InventoryTracker.Domain.Entities;

public class ConsumableItem : InventoryItem
{
    public override int AvailableQuantity => Quantity;
}
