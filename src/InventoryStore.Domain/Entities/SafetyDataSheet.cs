namespace InventoryStore.Domain.Entities;

// Safety Data Sheet metadata for an inventory item. Lives in its own table keyed by
// InventoryItemId so the data is stored as metadata, never as columns on InventoryItem.
// Populated by the optional SDS module from PubChem (keyless, GHS safety data).
public class SafetyDataSheet
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string Source { get; set; } = "pubchem";
    public string ChemicalName { get; set; } = string.Empty;
    public string? Cid { get; set; }
    public string? CasNumber { get; set; }
    public string? SignalWord { get; set; }
    public string? Pictograms { get; set; }
    public string? HazardStatements { get; set; }
    public string? PrecautionaryStatements { get; set; }
    public string? SdsUrl { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
