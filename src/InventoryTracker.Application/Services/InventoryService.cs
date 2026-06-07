using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using InventoryTracker.Domain.Entities;
using InventoryTracker.Domain.Enums;
using InventoryTracker.Domain.Interfaces.Repositories;

namespace InventoryTracker.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IActivityLogRepository _activityLogRepository;

    public InventoryService(IInventoryRepository inventoryRepository, IActivityLogRepository activityLogRepository)
    {
        _inventoryRepository  = inventoryRepository;
        _activityLogRepository = activityLogRepository;
    }

    public async Task<IEnumerable<InventoryItemDto>> GetAllItemsAsync()
        => (await _inventoryRepository.GetAllAsync()).Select(MapToDto);

    public async Task<IEnumerable<InventoryItemDto>> SearchItemsAsync(string query)
        => (await _inventoryRepository.SearchAsync(query)).Select(MapToDto);

    public async Task<InventoryItemDto?> GetItemAsync(int id)
    {
        var item = await _inventoryRepository.GetByIdAsync(id);
        return item is null ? null : MapToDto(item);
    }

    public async Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemDto dto, int userId, string username)
    {
        InventoryItem item = dto.ItemType == ItemType.Reusable
            ? new ReusableItem()
            : new ConsumableItem();

        item.Name            = dto.Name;
        item.Quantity        = dto.Quantity;
        item.Description     = dto.Description;
        item.Location        = dto.Location;
        item.SKU             = dto.SKU;
        item.MinimumQuantity = dto.MinimumQuantity;
        item.ScanWarning     = string.IsNullOrWhiteSpace(dto.ScanWarning) ? null : dto.ScanWarning.Trim();
        item.CreatedByUserId = userId;
        item.CreatedAt       = DateTime.UtcNow;
        item.UpdatedAt       = DateTime.UtcNow;

        var created = await _inventoryRepository.CreateAsync(item);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "Created", EntityType = "InventoryItem", EntityId = created.Id,
            Details = $"Created {dto.ItemType} item '{created.Name}'"
        });

        return MapToDto(created);
    }

    public async Task UpdateItemAsync(int id, UpdateInventoryItemDto dto, int userId, string username)
    {
        var item = await _inventoryRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Inventory item {id} not found.");

        if (item is ReusableItem reusable && dto.Quantity < reusable.CheckedOutCount)
            throw new InvalidOperationException(
                $"Cannot set Quantity to {dto.Quantity}: {reusable.CheckedOutCount} unit(s) are currently checked out.");

        item.Name            = dto.Name;
        item.Quantity        = dto.Quantity;
        item.Description     = dto.Description;
        item.Location        = dto.Location;
        item.SKU             = dto.SKU;
        item.MinimumQuantity = dto.MinimumQuantity;
        item.ScanWarning     = string.IsNullOrWhiteSpace(dto.ScanWarning) ? null : dto.ScanWarning.Trim();
        item.UpdatedAt       = DateTime.UtcNow;

        await _inventoryRepository.UpdateAsync(item);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "Updated", EntityType = "InventoryItem", EntityId = id,
            Details = $"Updated item '{item.Name}'"
        });
    }

    public async Task DeleteItemAsync(int id, int userId, string username)
    {
        var item = await _inventoryRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Inventory item {id} not found.");

        await _inventoryRepository.DeleteAsync(id);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "Deleted", EntityType = "InventoryItem", EntityId = id,
            Details = $"Deleted item '{item.Name}'"
        });
    }

    public async Task<IEnumerable<InventoryItemDto>> GetLowStockItemsAsync()
        => (await _inventoryRepository.GetLowStockAsync()).Select(MapToDto);

    internal static InventoryItemDto MapToDto(InventoryItem item)
    {
        var (itemType, checkedOut, lost) = item switch
        {
            ReusableItem r  => (ItemType.Reusable,   r.CheckedOutCount, r.LostCount),
            ConsumableItem  => (ItemType.Consumable,  0,                 0),
            _               => throw new InvalidOperationException($"Unknown item type: {item.GetType().Name}")
        };

        return new InventoryItemDto(
            item.Id, item.Name, item.Quantity, item.AvailableQuantity,
            item.Description, item.Location, item.SKU, item.MinimumQuantity,
            itemType, checkedOut, lost, item.IsLowStock,
            item.ScanWarning, item.CreatedAt, item.UpdatedAt
        );
    }
}
