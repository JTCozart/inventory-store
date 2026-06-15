namespace InventoryStore.Domain.Entities;

// A maintenance vendor — the organization that services items sent "out for maintenance".
// Mirrors Client (searchable, case-insensitive name match) but uses a single organization name.
public class Vendor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
