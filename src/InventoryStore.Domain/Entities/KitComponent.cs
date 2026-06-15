namespace InventoryStore.Domain.Entities;

// A single line in a kit's contents: "this kit needs N of item X".
public class KitComponent
{
    public int Id { get; set; }
    public int KitItemId { get; set; }
    public KitItem? Kit { get; set; }

    public int ComponentItemId { get; set; }
    public InventoryItem? ComponentItem { get; set; }

    // Units of the component required per single kit.
    public int Quantity { get; set; } = 1;
}
