using InventoryStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<CheckoutRecord> CheckoutRecords => Set<CheckoutRecord>();
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.Color).HasMaxLength(20);
            e.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<InventoryItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).IsRequired().HasMaxLength(200);
            e.Property(i => i.SKU).HasMaxLength(100);
            e.Property(i => i.Location).HasMaxLength(200);
            e.Ignore(i => i.AvailableQuantity);
            e.Ignore(i => i.IsLowStock);

            e.HasOne(i => i.Category)
             .WithMany()
             .HasForeignKey(i => i.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);

            // Use the existing ItemType int column as the TPH discriminator.
            // 0 = ConsumableItem, 1 = ReusableItem — matches the ItemType enum values.
            e.HasDiscriminator<int>("ItemType")
                .HasValue<ConsumableItem>(0)
                .HasValue<ReusableItem>(1);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).IsRequired().HasMaxLength(100);
            e.Property(u => u.Email).HasMaxLength(255);
            e.Property(u => u.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<ActivityLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Action).IsRequired().HasMaxLength(100);
            e.Property(l => l.Username).HasMaxLength(100);
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.Key).IsUnique();
            e.Property(s => s.Key).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<CheckoutRecord>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.CheckedOutBy).IsRequired().HasMaxLength(200);
            e.Ignore(c => c.IsCheckedIn);
            e.Ignore(c => c.IsOut);
        });

        modelBuilder.Entity<Client>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
            e.Property(c => c.LastName).HasMaxLength(100);
            e.Property(c => c.Phone).HasMaxLength(50);
            e.Property(c => c.Email).HasMaxLength(255);
            e.Property(c => c.Address).HasMaxLength(500);
            e.Ignore(c => c.DisplayName);
        });
    }
}
