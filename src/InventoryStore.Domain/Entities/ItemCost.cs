namespace InventoryStore.Domain.Entities;

// Cost metadata for an inventory item, kept off the InventoryItem entity in its own table
// (one row per item) so the optional Cost & Valuation module owns it. Drives total inventory
// value and straight-line depreciation for reusable items.
public class ItemCost
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public decimal UnitCost { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public int? UsefulLifeMonths { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
