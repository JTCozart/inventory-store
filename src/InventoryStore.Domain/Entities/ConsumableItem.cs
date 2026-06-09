namespace InventoryStore.Domain.Entities;

public class ConsumableItem : InventoryItem
{
    public override int AvailableQuantity => Quantity;
}
