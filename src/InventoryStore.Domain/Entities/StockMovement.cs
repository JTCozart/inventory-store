namespace InventoryStore.Domain.Entities;

// A single stock change for a consumable item (a lightweight, item-linked ledger). Recorded on
// every consume/restock so the Consumption Forecasting module can project run-out dates from real
// usage history instead of parsing activity-log text.
public class StockMovement
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string ChangeType { get; set; } = string.Empty; // "Consume" | "Restock"
    public int Quantity { get; set; }                       // positive magnitude
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? Notes { get; set; }
}
