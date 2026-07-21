using InventoryStore.Domain.Entities;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.App.Ai;

public sealed record ItemSummary(
    int Id, string Name, string ItemType, int Quantity, int AvailableQuantity,
    string? Location, int MinimumQuantity, bool IsLowStock, string? Category);

public sealed record CheckoutSummary(
    int ItemId, string ItemName, string CheckedOutBy, int Quantity, DateTime CheckedOutAt, string? Notes);

public sealed record LostItemSummary(
    int ItemId, string ItemName, string CheckedOutBy, int Quantity, DateTime CheckedOutAt, string? Notes);

public sealed record IncompleteKitSummary(
    int KitId, string KitName, int Buildable, string BottleneckComponentName, int BottleneckAvailable, int BottleneckNeededPerKit);

public sealed record MaintenanceDueSummary(
    int ItemId, string ItemName, DateOnly? LastMaintainedDate, DateOnly? NextDueDate, bool Overdue);

public sealed record OutForMaintenanceSummary(
    int ItemId, string ItemName, DateTime OutSince, string? VendorName, string? Notes);

public sealed record ClientSummary(int Id, string DisplayName, string? Phone, string? Email);
public sealed record ClientsResult(int TotalCount, List<ClientSummary> Sample);

public sealed record UserSummary(int Id, string Username, string DisplayName, string Role, bool IsActive, DateTime? LastLoginAt);

public sealed record ActivityLogEntry(DateTime Timestamp, string Username, string Action, string? EntityType, int? EntityId, string? Details);

// The only surface through which the chat assistant can touch inventory-store's data --
// every method here is a narrow, purpose-built EF Core query returning DTOs, never a
// generic "run this SQL" escape hatch. This is what makes the scope-lock in
// ChatOrchestrationService's system prompt more than a suggestion: even a fully
// "jailbroken" model has nothing to call except these methods.
public sealed class ChatTools(AppDbContext db)
{
    // itemType, when given, must be "Consumable", "Reusable", or "Kit" (case-insensitive) --
    // lets "show me consumables" / "list all kits" filter by item kind instead of guessing a
    // name substring. categoryContains matches the item's assigned Category (a separate field
    // from its name), for "items in the Tools category" style questions.
    public async Task<List<ItemSummary>> SearchItemsAsync(string? nameContains, string? locationContains, string? itemType, string? categoryContains, CancellationToken cancellationToken)
    {
        var query = db.InventoryItems.Include(i => i.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(nameContains))
            query = query.Where(i => i.Name.Contains(nameContains));
        if (!string.IsNullOrWhiteSpace(locationContains))
            query = query.Where(i => i.Location != null && i.Location.Contains(locationContains));
        if (!string.IsNullOrWhiteSpace(categoryContains))
            query = query.Where(i => i.Category != null && i.Category.Name.Contains(categoryContains));
        query = itemType?.Trim().ToLowerInvariant() switch
        {
            "consumable" => query.OfType<ConsumableItem>(),
            "reusable"   => query.OfType<ReusableItem>(),
            "kit"        => query.OfType<KitItem>(),
            _            => query
        };

        var items = await query.OrderBy(i => i.Name).Take(50).ToListAsync(cancellationToken);
        return items.Select(ToSummary).ToList();
    }

    // Checking out a kit records the kit itself in KitCheckouts, and separately writes one
    // CheckoutRecord per Reusable member (see KitService.CheckOutKitAsync) -- so a checkout
    // record's InventoryItemId is always a *member* item, never the kit's own id. Asking about
    // a kit by its own name only turns up here, in KitCheckouts. Both sources are queried and
    // merged so "who has the <kit name> out" isn't silently empty just because the kit's own
    // row lives in a different table than a plain item's.
    public async Task<List<CheckoutSummary>> GetActiveCheckoutsAsync(string? itemNameContains, string? checkedOutByContains, CancellationToken cancellationToken)
    {
        var itemQuery =
            from c in db.CheckoutRecords
            join i in db.InventoryItems on c.InventoryItemId equals i.Id
            where c.CheckedInAt == null && !c.IsLost
            select new { ItemId = c.InventoryItemId, ItemName = i.Name, c.CheckedOutBy, c.Quantity, c.CheckedOutAt, c.Notes };

        var kitQuery =
            from k in db.KitCheckouts
            join i in db.InventoryItems on k.KitItemId equals i.Id
            where k.CheckedInAt == null && !k.IsLost
            select new { ItemId = k.KitItemId, ItemName = i.Name, k.CheckedOutBy, k.Quantity, k.CheckedOutAt, k.Notes };

        if (!string.IsNullOrWhiteSpace(itemNameContains))
        {
            itemQuery = itemQuery.Where(x => x.ItemName.Contains(itemNameContains));
            kitQuery  = kitQuery.Where(x => x.ItemName.Contains(itemNameContains));
        }
        if (!string.IsNullOrWhiteSpace(checkedOutByContains))
        {
            itemQuery = itemQuery.Where(x => x.CheckedOutBy.Contains(checkedOutByContains));
            kitQuery  = kitQuery.Where(x => x.CheckedOutBy.Contains(checkedOutByContains));
        }

        var items = await itemQuery.ToListAsync(cancellationToken);
        var kits  = await kitQuery.ToListAsync(cancellationToken);

        return items.Concat(kits)
            .OrderByDescending(x => x.CheckedOutAt)
            .Take(50)
            .Select(x => new CheckoutSummary(x.ItemId, x.ItemName, x.CheckedOutBy, x.Quantity, x.CheckedOutAt, x.Notes))
            .ToList();
    }

    // A checkout record is "lost" once IsLost is set (it stays IsLost regardless of
    // CheckedInAt), independent of whether it's also been checked back in on paper --
    // matches CheckoutRecord.IsOut's own definition of what counts as active vs. not. Kit
    // checkouts get merged in for the same reason as GetActiveCheckoutsAsync above.
    public async Task<List<LostItemSummary>> GetLostItemsAsync(string? itemNameContains, string? checkedOutByContains, CancellationToken cancellationToken)
    {
        var itemQuery =
            from c in db.CheckoutRecords
            join i in db.InventoryItems on c.InventoryItemId equals i.Id
            where c.IsLost
            select new { ItemId = c.InventoryItemId, ItemName = i.Name, c.CheckedOutBy, c.Quantity, c.CheckedOutAt, c.Notes };

        var kitQuery =
            from k in db.KitCheckouts
            join i in db.InventoryItems on k.KitItemId equals i.Id
            where k.IsLost
            select new { ItemId = k.KitItemId, ItemName = i.Name, k.CheckedOutBy, k.Quantity, k.CheckedOutAt, k.Notes };

        if (!string.IsNullOrWhiteSpace(itemNameContains))
        {
            itemQuery = itemQuery.Where(x => x.ItemName.Contains(itemNameContains));
            kitQuery  = kitQuery.Where(x => x.ItemName.Contains(itemNameContains));
        }
        if (!string.IsNullOrWhiteSpace(checkedOutByContains))
        {
            itemQuery = itemQuery.Where(x => x.CheckedOutBy.Contains(checkedOutByContains));
            kitQuery  = kitQuery.Where(x => x.CheckedOutBy.Contains(checkedOutByContains));
        }

        var items = await itemQuery.ToListAsync(cancellationToken);
        var kits  = await kitQuery.ToListAsync(cancellationToken);

        return items.Concat(kits)
            .OrderByDescending(x => x.CheckedOutAt)
            .Take(50)
            .Select(x => new LostItemSummary(x.ItemId, x.ItemName, x.CheckedOutBy, x.Quantity, x.CheckedOutAt, x.Notes))
            .ToList();
    }

    public async Task<List<ItemSummary>> GetLowStockItemsAsync(CancellationToken cancellationToken)
    {
        var items = await db.InventoryItems.Include(i => i.Category).OrderBy(i => i.Name).ToListAsync(cancellationToken);
        return items.Where(i => i.IsLowStock).Take(50).Select(ToSummary).ToList();
    }

    // A kit is "incomplete" once it can't be fully assembled from current stock -- Buildable
    // is the scarcest member's available-quantity / per-kit-quantity, matching
    // KitItem.AvailableQuantity's own logic (see that entity's class comment).
    public async Task<List<IncompleteKitSummary>> GetIncompleteKitsAsync(CancellationToken cancellationToken)
    {
        var kits = await db.InventoryItems
            .OfType<KitItem>()
            .Include(k => k.Components)
            .ThenInclude(c => c.ComponentItem)
            .OrderBy(k => k.Name)
            .ToListAsync(cancellationToken);

        var results = new List<IncompleteKitSummary>();
        foreach (var kit in kits)
        {
            if (kit.Components.Count == 0)
            {
                results.Add(new IncompleteKitSummary(kit.Id, kit.Name, 0, "(no components configured)", 0, 0));
                continue;
            }

            KitComponent? bottleneck = null;
            var minBuildable = int.MaxValue;
            foreach (var component in kit.Components)
            {
                var available = component.ComponentItem?.AvailableQuantity ?? 0;
                var perKit = component.Quantity <= 0 ? 1 : component.Quantity;
                var buildable = available / perKit;
                if (buildable < minBuildable)
                {
                    minBuildable = buildable;
                    bottleneck = component;
                }
            }

            if (minBuildable == 0)
            {
                results.Add(new IncompleteKitSummary(
                    kit.Id, kit.Name, 0,
                    bottleneck?.ComponentItem?.Name ?? "(unknown item)",
                    bottleneck?.ComponentItem?.AvailableQuantity ?? 0,
                    bottleneck?.Quantity ?? 0));
            }
        }

        return results.Take(50).ToList();
    }

    // Overdue or due within daysAhead (default 30). MaintenanceSchedule.NextDueDate is a
    // computed (non-mapped) property, so the due/overdue filter runs client-side after
    // loading each item's single schedule row.
    public async Task<List<MaintenanceDueSummary>> GetMaintenanceDueAsync(int? daysAhead, CancellationToken cancellationToken)
    {
        var horizon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Math.Max(0, daysAhead ?? 30));

        var rows = await (
            from s in db.MaintenanceSchedules
            join i in db.InventoryItems on s.InventoryItemId equals i.Id
            select new { Schedule = s, ItemName = i.Name }
        ).ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return rows
            .Where(x => x.Schedule.NextDueDate is { } due && due <= horizon)
            .OrderBy(x => x.Schedule.NextDueDate)
            .Take(50)
            .Select(x => new MaintenanceDueSummary(
                x.Schedule.InventoryItemId, x.ItemName, x.Schedule.LastMaintainedDate,
                x.Schedule.NextDueDate, x.Schedule.NextDueDate < today))
            .ToList();
    }

    // Units physically out at a vendor right now (MaintenanceVisit.ReturnedAt == null) -- this
    // is what actually drives ReusableItem.OutForMaintenanceCount and thus AvailableQuantity, and
    // is independent of get_maintenance_due's schedule/due-date view: a unit can be out with no
    // schedule at all (an unplanned repair), so due-date logic alone can't explain a stock gap.
    public async Task<List<OutForMaintenanceSummary>> GetItemsOutForMaintenanceAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from v in db.MaintenanceVisits
            join i in db.InventoryItems on v.InventoryItemId equals i.Id
            where v.ReturnedAt == null
            select new { Visit = v, ItemName = i.Name }
        ).ToListAsync(cancellationToken);

        var vendorNames = await db.Vendors.ToDictionaryAsync(v => v.Id, v => v.Name, cancellationToken);

        return rows
            .OrderByDescending(x => x.Visit.OutForMaintenanceAt)
            .Take(50)
            .Select(x => new OutForMaintenanceSummary(
                x.Visit.InventoryItemId, x.ItemName, x.Visit.OutForMaintenanceAt,
                x.Visit.VendorId is { } vid ? vendorNames.GetValueOrDefault(vid) : null,
                x.Visit.Notes))
            .ToList();
    }

    // TotalCount is the real count (not capped) so "how many clients do I have" gets an
    // accurate answer even when the client list is longer than the sample returned.
    public async Task<ClientsResult> SearchClientsAsync(string? nameContains, CancellationToken cancellationToken)
    {
        var query = db.Clients.AsQueryable();
        if (!string.IsNullOrWhiteSpace(nameContains))
            query = query.Where(c => c.FirstName.Contains(nameContains) || (c.LastName != null && c.LastName.Contains(nameContains)));

        var total = await query.CountAsync(cancellationToken);
        var sample = await query.OrderBy(c => c.FirstName).Take(50).ToListAsync(cancellationToken);

        return new ClientsResult(total, sample.Select(c => new ClientSummary(c.Id, c.DisplayName, c.Phone, c.Email)).ToList());
    }

    public async Task<List<UserSummary>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users.OrderByDescending(u => u.LastLoginAt).Take(100).ToListAsync(cancellationToken);
        return users
            .Select(u => new UserSummary(u.Id, u.Username, u.DisplayName, u.Role.ToString(), u.IsActive, u.LastLoginAt))
            .ToList();
    }

    // Recent audit-trail entries (logins, checkouts/check-ins, stock changes, item edits, ...).
    // Not filtered to any one entity type -- this is the general "what happened" tool.
    public async Task<List<ActivityLogEntry>> GetRecentActivityAsync(string? action, string? usernameContains, int? take, CancellationToken cancellationToken)
    {
        var query = db.ActivityLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.Contains(action)); // SQLite LIKE is ASCII case-insensitive by default
        if (!string.IsNullOrWhiteSpace(usernameContains))
            query = query.Where(a => a.Username.Contains(usernameContains));

        var limit = Math.Clamp(take ?? 20, 1, 50);
        var rows = await query.OrderByDescending(a => a.Timestamp).Take(limit).ToListAsync(cancellationToken);

        return rows
            .Select(a => new ActivityLogEntry(a.Timestamp, a.Username, a.Action, a.EntityType, a.EntityId, a.Details))
            .ToList();
    }

    // "How do I..." questions aren't answered from the database at all -- they're answered from
    // the same user guide published at inventorystore.app, bundled into the app itself (see
    // HelpKnowledgeBase) so this works offline and always matches the installed version.
    public Task<List<HelpSection>> SearchHelpAsync(string query, CancellationToken cancellationToken) =>
        Task.FromResult(HelpKnowledgeBase.Search(query));

    private static ItemSummary ToSummary(InventoryItem i) => new(
        i.Id, i.Name,
        i switch { KitItem => "Kit", ReusableItem => "Reusable", _ => "Consumable" },
        i.Quantity, i.AvailableQuantity, i.Location, i.MinimumQuantity, i.IsLowStock, i.Category?.Name);
}
