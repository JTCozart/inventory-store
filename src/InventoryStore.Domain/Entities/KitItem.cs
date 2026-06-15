namespace InventoryStore.Domain.Entities;

// A "kit" is a shell that bundles other inventory items. It holds no stock of its own;
// instead it points at member items (KitComponents) with a per-kit quantity each.
public class KitItem : InventoryItem
{
    // When false, a kit must be whole (every member fully in stock) to be checked out or consumed.
    // When true, staff may proceed with whatever is currently available.
    public bool AllowPartial { get; set; }

    public ICollection<KitComponent> Components { get; set; } = new List<KitComponent>();

    // How many complete kits can currently be assembled — the scarcest member governs.
    // 0 when the kit has no members or any member's per-kit quantity is invalid.
    public override int AvailableQuantity
    {
        get
        {
            if (Components.Count == 0) return 0;
            var min = int.MaxValue;
            foreach (var c in Components)
            {
                var available = c.ComponentItem?.AvailableQuantity ?? 0;
                var perKit = c.Quantity <= 0 ? int.MaxValue : c.Quantity;
                var buildable = perKit == int.MaxValue ? 0 : available / perKit;
                if (buildable < min) min = buildable;
            }
            return min == int.MaxValue ? 0 : min;
        }
    }
}
