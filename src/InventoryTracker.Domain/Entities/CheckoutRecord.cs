namespace InventoryTracker.Domain.Entities;

public class CheckoutRecord
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string CheckedOutBy { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public DateTime CheckedOutAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedInAt { get; set; }
    public bool IsLost { get; set; } = false;
    public string? Notes { get; set; }
    public int? ClientId { get; set; }

    public bool IsCheckedIn => CheckedInAt.HasValue;
    public bool IsOut => !CheckedInAt.HasValue && !IsLost;
}
