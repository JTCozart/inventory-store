namespace InventoryStore.Domain.Entities;

// A single "out for maintenance" event for an item (open/closed history, like CheckoutRecord).
// While open it reduces a reusable item's available quantity; returning it closes the visit.
public class MaintenanceVisit
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public DateTime OutForMaintenanceAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReturnedAt { get; set; }
    public string? Notes { get; set; }

    public bool IsOut => !ReturnedAt.HasValue;
}
