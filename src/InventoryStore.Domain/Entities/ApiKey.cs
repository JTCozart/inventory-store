namespace InventoryStore.Domain.Entities;

// A credential for the Public API module. Only the SHA-256 hash of the key is stored; the raw
// key is shown once, at creation or rotation. Prefix is the first few characters, kept so an
// admin can tell keys apart in the list.
public class ApiKey
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }

    // Rotation. When a key is rotated with a grace period, the old secret keeps working until
    // PreviousKeyExpiresAt so callers can switch over without downtime.
    public DateTime? RotatedAt { get; set; }
    public string? PreviousKeyHash { get; set; }
    public DateTime? PreviousKeyExpiresAt { get; set; }

    public bool GraceActive(DateTime nowUtc) =>
        PreviousKeyHash is not null && PreviousKeyExpiresAt is { } exp && exp > nowUtc;
}
