namespace InventoryStore.Domain.Entities;

public class ProductMetadata
{
    public int Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public string? Size { get; set; }
    public string? Weight { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
