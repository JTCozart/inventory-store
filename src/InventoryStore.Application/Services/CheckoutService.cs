using System.Collections.Concurrent;
using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Enums;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class CheckoutService : ICheckoutService
{
    private readonly IInventoryRepository _inventoryRepo;
    private readonly ICheckoutRepository _checkoutRepo;
    private readonly IActivityLogRepository _activityRepo;
    private readonly IStockMovementRepository _stockMovementRepo;
    private readonly INtfyService _ntfy;
    private readonly IWebhookService _webhooks;

    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _itemLocks = new();

    private static SemaphoreSlim GetItemLock(int itemId) =>
        _itemLocks.GetOrAdd(itemId, _ => new SemaphoreSlim(1, 1));

    public CheckoutService(
        IInventoryRepository inventoryRepo,
        ICheckoutRepository checkoutRepo,
        IActivityLogRepository activityRepo,
        IStockMovementRepository stockMovementRepo,
        INtfyService ntfy,
        IWebhookService webhooks)
    {
        _inventoryRepo     = inventoryRepo;
        _checkoutRepo      = checkoutRepo;
        _activityRepo      = activityRepo;
        _stockMovementRepo = stockMovementRepo;
        _ntfy              = ntfy;
        _webhooks          = webhooks;
    }

    private static object ItemPayload(InventoryItem item) =>
        new { id = item.Id, name = item.Name, sku = item.SKU, quantity = item.Quantity };

    public async Task<ItemStatusDto> GetItemStatusAsync(int inventoryItemId)
    {
        var item = await _inventoryRepo.GetByIdAsync(inventoryItemId)
            ?? throw new KeyNotFoundException($"Item {inventoryItemId} not found.");
        return await BuildStatusDtoAsync(item);
    }

    public async Task<ItemStatusDto?> GetItemStatusBySkuAsync(string sku)
    {
        var items = await _inventoryRepo.SearchAsync(sku);
        var item  = items.FirstOrDefault(i => string.Equals(i.SKU, sku, StringComparison.OrdinalIgnoreCase));
        return item is null ? null : await BuildStatusDtoAsync(item);
    }

    public async Task<CheckoutRecordDto> CheckOutAsync(CheckOutItemDto dto, int userId, string username)
    {
        var itemLock = GetItemLock(dto.InventoryItemId);
        await itemLock.WaitAsync();
        try
        {
            var item = await _inventoryRepo.GetByIdAsync(dto.InventoryItemId)
                ?? throw new KeyNotFoundException("Item not found.");

            if (item is not ReusableItem reusable)
                throw new InvalidOperationException("Only reusable items can be checked out.");

            if (reusable.AvailableQuantity < dto.Quantity)
                throw new InvalidOperationException(
                    $"Only {reusable.AvailableQuantity} available. Cannot check out {dto.Quantity}.");

            var record = new CheckoutRecord
            {
                InventoryItemId = dto.InventoryItemId,
                CheckedOutBy    = dto.CheckedOutBy,
                Quantity        = dto.Quantity,
                Notes           = dto.Notes,
                CheckedOutAt    = DateTime.UtcNow,
                ClientId        = dto.ClientId
            };

            var created = await _checkoutRepo.CreateAsync(record);

            reusable.CheckedOutCount += dto.Quantity;
            reusable.UpdatedAt        = DateTime.UtcNow;
            await _inventoryRepo.UpdateAsync(reusable);

            await _activityRepo.CreateAsync(new ActivityLog
            {
                UserId = userId, Username = username,
                Action = "CheckOut", EntityType = "InventoryItem", EntityId = item.Id,
                Details = $"'{dto.CheckedOutBy}' checked out {dto.Quantity}x '{item.Name}'"
            });

            _ = _ntfy.NotifyCheckoutAsync(item.Name, dto.CheckedOutBy, null);
            _ = _webhooks.DispatchAsync("item.checkout",
                new { item = ItemPayload(item), actor = username, checkedOutBy = dto.CheckedOutBy, quantity = dto.Quantity });
            return MapRecord(created, item.Name);
        }
        finally
        {
            itemLock.Release();
        }
    }

    public async Task<CheckoutRecordDto> CheckInAsync(CheckInItemDto dto, int userId, string username)
    {
        var record = await _checkoutRepo.GetByIdAsync(dto.CheckoutRecordId)
            ?? throw new KeyNotFoundException("Checkout record not found.");

        if (record.IsCheckedIn)
            throw new InvalidOperationException("This item is already checked in.");

        if (record.IsLost)
            throw new InvalidOperationException("This item was marked lost and cannot be checked in.");

        var item = await _inventoryRepo.GetByIdAsync(record.InventoryItemId)
            ?? throw new KeyNotFoundException("Item not found.");

        if (item is not ReusableItem reusable)
            throw new InvalidOperationException("Expected a reusable item for this checkout record.");

        record.CheckedInAt = DateTime.UtcNow;
        record.Notes       = dto.Notes ?? record.Notes;
        await _checkoutRepo.UpdateAsync(record);

        reusable.CheckedOutCount = Math.Max(0, reusable.CheckedOutCount - record.Quantity);
        reusable.UpdatedAt       = DateTime.UtcNow;
        await _inventoryRepo.UpdateAsync(reusable);

        await _activityRepo.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "CheckIn", EntityType = "InventoryItem", EntityId = item.Id,
            Details = $"'{record.CheckedOutBy}' checked in {record.Quantity}x '{item.Name}'"
        });

        _ = _ntfy.NotifyCheckinAsync(item.Name, record.CheckedOutBy);
        _ = _webhooks.DispatchAsync("item.checkin",
            new { item = ItemPayload(item), actor = username, checkedOutBy = record.CheckedOutBy, quantity = record.Quantity });
        return MapRecord(record, item.Name);
    }

    public async Task<CheckoutRecordDto> MarkLostAsync(MarkLostDto dto, int userId, string username)
    {
        var record = await _checkoutRepo.GetByIdAsync(dto.CheckoutRecordId)
            ?? throw new KeyNotFoundException("Checkout record not found.");

        if (record.IsCheckedIn)
            throw new InvalidOperationException("Cannot mark a returned item as lost.");

        var item = await _inventoryRepo.GetByIdAsync(record.InventoryItemId)
            ?? throw new KeyNotFoundException("Item not found.");

        if (item is not ReusableItem reusable)
            throw new InvalidOperationException("Expected a reusable item for this checkout record.");

        record.IsLost      = true;
        record.CheckedInAt = DateTime.UtcNow;
        record.Notes       = dto.Notes ?? record.Notes;
        await _checkoutRepo.UpdateAsync(record);

        reusable.CheckedOutCount = Math.Max(0, reusable.CheckedOutCount - record.Quantity);
        reusable.LostCount      += record.Quantity;
        reusable.UpdatedAt       = DateTime.UtcNow;
        await _inventoryRepo.UpdateAsync(reusable);

        await _activityRepo.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "Lost", EntityType = "InventoryItem", EntityId = item.Id,
            Details = $"{record.Quantity}x '{item.Name}' marked as lost (checked out by '{record.CheckedOutBy}')"
        });

        _ = _ntfy.NotifyLostAsync(item.Name, record.CheckedOutBy);
        _ = _webhooks.DispatchAsync("item.lost",
            new { item = ItemPayload(item), actor = username, checkedOutBy = record.CheckedOutBy, quantity = record.Quantity });
        return MapRecord(record, item.Name);
    }

    public async Task<CheckoutRecordDto> MarkFoundAsync(MarkFoundDto dto, int userId, string username)
    {
        var record = await _checkoutRepo.GetByIdAsync(dto.CheckoutRecordId)
            ?? throw new KeyNotFoundException("Checkout record not found.");

        if (!record.IsLost)
            throw new InvalidOperationException("Item is not marked as lost.");

        var item = await _inventoryRepo.GetByIdAsync(record.InventoryItemId)
            ?? throw new KeyNotFoundException("Item not found.");

        if (item is not ReusableItem reusable)
            throw new InvalidOperationException("Expected a reusable item for this checkout record.");

        record.IsLost      = false;
        record.CheckedInAt = null;
        record.Notes       = dto.Notes ?? record.Notes;
        await _checkoutRepo.UpdateAsync(record);

        reusable.LostCount       = Math.Max(0, reusable.LostCount - record.Quantity);
        reusable.CheckedOutCount += record.Quantity;
        reusable.UpdatedAt        = DateTime.UtcNow;
        await _inventoryRepo.UpdateAsync(reusable);

        await _activityRepo.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "Found", EntityType = "InventoryItem", EntityId = item.Id,
            Details = $"{record.Quantity}x '{item.Name}' marked as found (was checked out by '{record.CheckedOutBy}')"
        });

        _ = _webhooks.DispatchAsync("item.found",
            new { item = ItemPayload(item), actor = username, checkedOutBy = record.CheckedOutBy, quantity = record.Quantity });
        return MapRecord(record, item.Name);
    }

    public async Task ConsumeAsync(ConsumeItemDto dto, int userId, string username)
    {
        var itemLock = GetItemLock(dto.InventoryItemId);
        await itemLock.WaitAsync();
        try
        {
            var item = await _inventoryRepo.GetByIdAsync(dto.InventoryItemId)
                ?? throw new KeyNotFoundException("Item not found.");

            if (item is not ConsumableItem consumable)
                throw new InvalidOperationException("Use checkout/checkin for reusable items.");

            if (consumable.Quantity < dto.Quantity)
                throw new InvalidOperationException(
                    $"Only {consumable.Quantity} in stock. Cannot consume {dto.Quantity}.");

            consumable.Quantity  -= dto.Quantity;
            consumable.UpdatedAt  = DateTime.UtcNow;
            await _inventoryRepo.UpdateAsync(consumable);

            await _activityRepo.CreateAsync(new ActivityLog
            {
                UserId = userId, Username = username,
                Action = "Consume", EntityType = "InventoryItem", EntityId = item.Id,
                Details = $"Consumed {dto.Quantity}x '{item.Name}'"
                          + (dto.Notes is not null ? $": {dto.Notes}" : "")
            });

            // Ledger entry feeding the Consumption Forecasting module (recorded regardless of
            // whether that module is enabled, so history is ready when it is turned on).
            await _stockMovementRepo.AddAsync(new StockMovement
            {
                InventoryItemId = item.Id, ChangeType = "Consume", Quantity = dto.Quantity,
                UserId = userId, Username = username, Notes = dto.Notes, Timestamp = DateTime.UtcNow
            });

            _ = _webhooks.DispatchAsync("item.consumed",
                new { item = ItemPayload(item), actor = username, quantity = dto.Quantity, notes = dto.Notes });

            if (consumable.IsLowStock)
            {
                _ = _ntfy.NotifyLowStockAsync(item.Name, consumable.Quantity, consumable.MinimumQuantity);
                _ = _webhooks.DispatchAsync("item.lowstock",
                    new { item = ItemPayload(item), available = consumable.Quantity, minimum = consumable.MinimumQuantity });
            }
        }
        finally
        {
            itemLock.Release();
        }
    }

    public async Task RestockAsync(RestockItemDto dto, int userId, string username)
    {
        var item = await _inventoryRepo.GetByIdAsync(dto.InventoryItemId)
            ?? throw new KeyNotFoundException("Item not found.");

        if (item is not ConsumableItem consumable)
            throw new InvalidOperationException("Restock is only for consumable items. Edit quantity directly for reusable items.");

        consumable.Quantity += dto.Quantity;
        consumable.UpdatedAt = DateTime.UtcNow;
        await _inventoryRepo.UpdateAsync(consumable);

        await _activityRepo.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "Restock", EntityType = "InventoryItem", EntityId = item.Id,
            Details = $"Restocked {dto.Quantity}x '{item.Name}'"
                      + (dto.Notes is not null ? $": {dto.Notes}" : "")
        });

        await _stockMovementRepo.AddAsync(new StockMovement
        {
            InventoryItemId = item.Id, ChangeType = "Restock", Quantity = dto.Quantity,
            UserId = userId, Username = username, Notes = dto.Notes, Timestamp = DateTime.UtcNow
        });

        _ = _webhooks.DispatchAsync("item.restocked",
            new { item = ItemPayload(item), actor = username, quantity = dto.Quantity, notes = dto.Notes });
    }

    public async Task<IEnumerable<CheckoutRecordDto>> GetAllActiveCheckoutsAsync()
    {
        var records = await _checkoutRepo.GetAllActiveAsync();
        var result  = new List<CheckoutRecordDto>();
        foreach (var r in records)
        {
            var item = await _inventoryRepo.GetByIdAsync(r.InventoryItemId);
            result.Add(MapRecord(r, item?.Name ?? "Unknown"));
        }
        return result;
    }

    public async Task<IEnumerable<CheckoutRecordDto>> GetCheckoutHistoryAsync(int inventoryItemId)
    {
        var item = await _inventoryRepo.GetByIdAsync(inventoryItemId)
            ?? throw new KeyNotFoundException("Item not found.");
        var records = await _checkoutRepo.GetAllByItemAsync(inventoryItemId);
        return records.Select(r => MapRecord(r, item.Name));
    }

    public async Task<IEnumerable<CheckoutRecordDto>> GetClientHistoryAsync(int clientId)
    {
        var records = (await _checkoutRepo.GetByClientIdAsync(clientId)).ToList();
        var itemCache = new Dictionary<int, string>();
        var result = new List<CheckoutRecordDto>(records.Count);
        foreach (var r in records)
        {
            if (!itemCache.TryGetValue(r.InventoryItemId, out var name))
            {
                var item = await _inventoryRepo.GetByIdAsync(r.InventoryItemId);
                name = item?.Name ?? "Unknown";
                itemCache[r.InventoryItemId] = name;
            }
            result.Add(MapRecord(r, name));
        }
        return result;
    }

    private async Task<ItemStatusDto> BuildStatusDtoAsync(InventoryItem item)
    {
        IEnumerable<CheckoutRecord> active = [];
        IEnumerable<CheckoutRecord> lostRecords = [];
        if (item is ReusableItem)
        {
            active      = await _checkoutRepo.GetActiveByItemAsync(item.Id);
            lostRecords = await _checkoutRepo.GetLostByItemAsync(item.Id);
        }

        var (itemType, checkedOut, lost) = item switch
        {
            ReusableItem r  => (ItemType.Reusable,  r.CheckedOutCount, r.LostCount),
            ConsumableItem  => (ItemType.Consumable, 0,                 0),
            _               => throw new InvalidOperationException($"Unknown item type: {item.GetType().Name}")
        };

        return new ItemStatusDto(
            item.Id, item.Name, item.SKU, item.Location,
            itemType, item.Quantity, item.AvailableQuantity,
            checkedOut, lost, item.IsLowStock,
            item.MinimumQuantity, item.ScanWarning,
            active.Select(r => MapRecord(r, item.Name)),
            lostRecords.Select(r => MapRecord(r, item.Name)),
            item.ExpiryDate, item.Category?.Name, item.Category?.Color,
            item.Tags.OrderBy(t => t.Name).Select(t => new TagDto(t.Id, t.Name)).ToList(),
            item.SelectedMetadata?.ImageUrl, item.SelectedMetadata?.Brand, item.SelectedMetadata?.Category, item.SelectedMetadata?.Description
        );
    }

    private static CheckoutRecordDto MapRecord(CheckoutRecord r, string itemName) => new(
        r.Id, r.InventoryItemId, itemName, r.CheckedOutBy,
        r.Quantity, r.CheckedOutAt, r.CheckedInAt, r.IsLost, r.Notes, r.ClientId
    );
}
