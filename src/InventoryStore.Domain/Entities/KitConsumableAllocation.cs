namespace InventoryStore.Domain.Entities;

// Records how much of a consumable member was deducted for one kit checkout, so that when the kit
// comes back the unused remainder can be reconciled (returned to stock). Allocated is set at
// checkout; Used and ReconciledAt are filled in at reconciliation time.
public class KitConsumableAllocation
{
    public int Id { get; set; }
    public int KitCheckoutId { get; set; }
    public int ConsumableItemId { get; set; }
    public int AllocatedQuantity { get; set; }   // amount actually deducted at checkout
    public int UsedQuantity { get; set; }         // set at reconcile; the remainder is returned
    public DateTime? ReconciledAt { get; set; }
}
