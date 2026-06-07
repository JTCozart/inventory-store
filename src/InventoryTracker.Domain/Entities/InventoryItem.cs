namespace InventoryTracker.Domain.Entities;

public abstract class InventoryItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? SKU { get; set; }
    public int MinimumQuantity { get; set; }
    public string? ScanWarning { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }

    public abstract int AvailableQuantity { get; }
    public bool IsLowStock => MinimumQuantity > 0 && AvailableQuantity <= MinimumQuantity;
}
