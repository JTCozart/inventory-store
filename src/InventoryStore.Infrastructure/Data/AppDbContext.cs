using InventoryStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<CheckoutRecord> CheckoutRecords => Set<CheckoutRecord>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ProductMetadata> ProductMetadata => Set<ProductMetadata>();
    public DbSet<SafetyDataSheet> SafetyDataSheets => Set<SafetyDataSheet>();
    public DbSet<ItemCost> ItemCosts => Set<ItemCost>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<KitComponent> KitComponents => Set<KitComponent>();
    public DbSet<KitCheckout> KitCheckouts => Set<KitCheckout>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<MaintenanceVisit> MaintenanceVisits => Set<MaintenanceVisit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.Color).HasMaxLength(20);
            e.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(t => t.Name).IsUnique();
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

            e.HasOne(i => i.SelectedMetadata)
             .WithMany()
             .HasForeignKey(i => i.SelectedMetadataId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(i => i.Tags)
             .WithMany()
             .UsingEntity(
                "InventoryItemTags",
                l => l.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                r => r.HasOne(typeof(InventoryItem)).WithMany().HasForeignKey("InventoryItemId").OnDelete(DeleteBehavior.Cascade));

            // Use the existing ItemType int column as the TPH discriminator.
            // 0 = ConsumableItem, 1 = ReusableItem, 2 = KitItem — matches the ItemType enum values.
            e.HasDiscriminator<int>("ItemType")
                .HasValue<ConsumableItem>(0)
                .HasValue<ReusableItem>(1)
                .HasValue<KitItem>(2);
        });

        modelBuilder.Entity<KitComponent>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.KitItemId);
            e.HasIndex(c => c.ComponentItemId);

            // A kit's component lines are owned by the kit — drop them when the kit is deleted.
            e.HasOne(c => c.Kit)
             .WithMany(k => k.Components)
             .HasForeignKey(c => c.KitItemId)
             .OnDelete(DeleteBehavior.Cascade);

            // No cascade from the component item: deleting an item that happens to be a kit member
            // is cleaned up in InventoryService so we avoid multiple-cascade-path issues in SQLite.
            e.HasOne(c => c.ComponentItem)
             .WithMany()
             .HasForeignKey(c => c.ComponentItemId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KitCheckout>(e =>
        {
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.KitItemId);
            e.Property(k => k.CheckedOutBy).IsRequired().HasMaxLength(200);
            e.Ignore(k => k.IsCheckedIn);
            e.Ignore(k => k.IsOut);

            e.HasMany(k => k.ComponentCheckouts)
             .WithOne()
             .HasForeignKey(r => r.KitCheckoutId)
             .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<ProductMetadata>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Barcode).IsRequired().HasMaxLength(100);
            e.HasIndex(p => p.Barcode);
            e.Property(p => p.Source).IsRequired().HasMaxLength(50);
            e.Property(p => p.Name).IsRequired().HasMaxLength(500);
            e.Property(p => p.ImageUrl).HasMaxLength(2000);
            e.Property(p => p.Brand).HasMaxLength(200);
            e.Property(p => p.Category).HasMaxLength(200);
            e.Property(p => p.Size).HasMaxLength(100);
            e.Property(p => p.Weight).HasMaxLength(100);
        });

        modelBuilder.Entity<SafetyDataSheet>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.InventoryItemId);
            e.Property(s => s.Source).IsRequired().HasMaxLength(50);
            e.Property(s => s.ChemicalName).IsRequired().HasMaxLength(500);
            e.Property(s => s.Cid).HasMaxLength(50);
            e.Property(s => s.CasNumber).HasMaxLength(50);
            e.Property(s => s.SignalWord).HasMaxLength(50);
            e.Property(s => s.Pictograms).HasMaxLength(1000);
            e.Property(s => s.SdsUrl).HasMaxLength(2000);

            // SDS rows are owned by their item — remove them when the item is deleted.
            e.HasOne<InventoryItem>()
             .WithMany()
             .HasForeignKey(s => s.InventoryItemId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemCost>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.InventoryItemId).IsUnique();
            e.Property(c => c.UnitCost).HasColumnType("TEXT");
            e.HasOne<InventoryItem>()
             .WithMany()
             .HasForeignKey(c => c.InventoryItemId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.InventoryItemId);
            e.HasIndex(m => m.Timestamp);
            e.Property(m => m.ChangeType).IsRequired().HasMaxLength(20);
            e.HasOne<InventoryItem>()
             .WithMany()
             .HasForeignKey(m => m.InventoryItemId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WebhookEndpoint>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Url).IsRequired().HasMaxLength(2000);
            e.Property(w => w.Events).IsRequired().HasMaxLength(500);
            e.Property(w => w.Secret).HasMaxLength(200);
            e.Property(w => w.LastStatus).HasMaxLength(100);
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

        modelBuilder.Entity<Vendor>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Name).IsRequired().HasMaxLength(200);
            e.Property(v => v.Phone).HasMaxLength(50);
            e.Property(v => v.Email).HasMaxLength(255);
            e.Property(v => v.Address).HasMaxLength(500);
        });

        modelBuilder.Entity<MaintenanceSchedule>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.InventoryItemId).IsUnique();
            e.Ignore(s => s.NextDueDate);
            e.Ignore(s => s.IsOverdue);
            e.HasOne<InventoryItem>()
             .WithMany()
             .HasForeignKey(s => s.InventoryItemId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaintenanceVisit>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.InventoryItemId);
            e.Ignore(v => v.IsOut);
            e.HasOne<InventoryItem>()
             .WithMany()
             .HasForeignKey(v => v.InventoryItemId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.Vendor)
             .WithMany()
             .HasForeignKey(v => v.VendorId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
