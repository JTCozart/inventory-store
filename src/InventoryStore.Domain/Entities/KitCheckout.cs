namespace InventoryStore.Domain.Entities;

// Records one kit handed out as a unit. Groups the individual reusable-member checkout records
// so the whole kit can be checked back in or marked lost together. Consumable members are
// consumed at checkout time and are not tracked here (they don't come back).
public class KitCheckout
{
    public int Id { get; set; }
    public int KitItemId { get; set; }
    public string CheckedOutBy { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public DateTime CheckedOutAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedInAt { get; set; }
    public bool IsLost { get; set; } = false;
    public string? Notes { get; set; }
    public int? ClientId { get; set; }

    public ICollection<CheckoutRecord> ComponentCheckouts { get; set; } = new List<CheckoutRecord>();

    public bool IsCheckedIn => CheckedInAt.HasValue;
    public bool IsOut => !CheckedInAt.HasValue && !IsLost;
}
