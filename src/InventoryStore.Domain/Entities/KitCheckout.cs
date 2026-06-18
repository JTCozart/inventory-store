namespace InventoryStore.Domain.Entities;

// Records one kit handed out as a unit. Groups the individual reusable-member checkout records
// so the whole kit can be checked back in or marked lost together. Consumable members are deducted
// at checkout time; their per-checkout allocation is tracked in ConsumableAllocations so the unused
// remainder can be reconciled (returned to stock) when the kit comes back.
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

    // Set when the kit was checked in without recording consumable usage, so it shows up on the
    // reconciliation report until someone counts what was actually used.
    public bool NeedsReconciliation { get; set; } = false;
    public DateTime? ReconciledAt { get; set; }

    public ICollection<CheckoutRecord> ComponentCheckouts { get; set; } = new List<CheckoutRecord>();
    public ICollection<KitConsumableAllocation> ConsumableAllocations { get; set; } = new List<KitConsumableAllocation>();

    public bool IsCheckedIn => CheckedInAt.HasValue;
    public bool IsOut => !CheckedInAt.HasValue && !IsLost;
}
