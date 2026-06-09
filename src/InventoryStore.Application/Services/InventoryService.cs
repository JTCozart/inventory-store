using System.Text;
using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Enums;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly ICategoryRepository _categoryRepository;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        IActivityLogRepository activityLogRepository,
        ICategoryRepository categoryRepository)
    {
        _inventoryRepository  = inventoryRepository;
        _activityLogRepository = activityLogRepository;
        _categoryRepository   = categoryRepository;
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

        item.Name            = dto.Name.Trim();
        item.Quantity        = dto.Quantity;
        item.Description     = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        item.Location        = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim();
        item.SKU             = string.IsNullOrWhiteSpace(dto.SKU) ? null : dto.SKU.Trim();
        item.MinimumQuantity = dto.MinimumQuantity;
        item.ScanWarning     = string.IsNullOrWhiteSpace(dto.ScanWarning) ? null : dto.ScanWarning.Trim();
        item.CategoryId      = dto.CategoryId;
        item.ExpiryDate      = dto.ExpiryDate;
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

        item.Name            = dto.Name.Trim();
        item.Quantity        = dto.Quantity;
        item.Description     = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        item.Location        = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim();
        item.SKU             = string.IsNullOrWhiteSpace(dto.SKU) ? null : dto.SKU.Trim();
        item.MinimumQuantity = dto.MinimumQuantity;
        item.ScanWarning     = string.IsNullOrWhiteSpace(dto.ScanWarning) ? null : dto.ScanWarning.Trim();
        item.CategoryId      = dto.CategoryId;
        item.ExpiryDate      = dto.ExpiryDate;
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

    public async Task<IEnumerable<InventoryItemDto>> GetPublicItemsAsync()
        => (await _inventoryRepository.GetPublicAsync()).Select(MapToDto);

    public async Task SetItemPublicAsync(int id, bool isPublic)
    {
        var item = await _inventoryRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Inventory item {id} not found.");
        item.IsPublic  = isPublic;
        item.UpdatedAt = DateTime.UtcNow;
        await _inventoryRepository.UpdateAsync(item);
    }

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
            item.ScanWarning, item.CreatedAt, item.UpdatedAt,
            item.CategoryId, item.Category?.Name, item.ExpiryDate,
            item.IsPublic, item.Category?.Color
        );
    }

    public async Task<IEnumerable<LocationSummaryDto>> GetAllLocationsAsync()
    {
        var locations = await _inventoryRepository.GetAllLocationsAsync();
        return locations.Select(l => new LocationSummaryDto(l.Location, l.Count));
    }

    public async Task RenameLocationAsync(string from, string to, int userId, string username)
    {
        from = from.Trim();
        to   = to.Trim();
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("New location name cannot be empty.");
        if (from == to) return;

        await _inventoryRepository.BulkUpdateLocationAsync(from, to);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "LocationRenamed",
            Details = $"Location '{from}' renamed to '{to}'"
        });
    }

    public async Task SetItemLocationAsync(int itemId, string? location, int userId, string username)
    {
        var item = await _inventoryRepository.GetByIdAsync(itemId)
            ?? throw new KeyNotFoundException($"Item {itemId} not found.");

        var oldLocation = item.Location;
        item.Location  = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await _inventoryRepository.UpdateAsync(item);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "LocationChanged", EntityType = "InventoryItem", EntityId = itemId,
            Details = $"'{item.Name}' location changed from '{oldLocation ?? "none"}' to '{item.Location ?? "none"}'"
        });
    }

    public async Task SetItemCategoryAsync(int itemId, int? categoryId, int userId, string username)
    {
        var item = await _inventoryRepository.GetByIdAsync(itemId)
            ?? throw new KeyNotFoundException($"Item {itemId} not found.");

        var oldName = item.Category?.Name ?? "none";
        item.CategoryId = categoryId;
        item.UpdatedAt  = DateTime.UtcNow;
        await _inventoryRepository.UpdateAsync(item);

        var newName = categoryId.HasValue
            ? (await _categoryRepository.GetByIdAsync(categoryId.Value))?.Name ?? categoryId.ToString()
            : "none";

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "CategoryChanged", EntityType = "InventoryItem", EntityId = itemId,
            Details = $"'{item.Name}' category changed from '{oldName}' to '{newName}'"
        });
    }

    public async Task ClearLocationAsync(string location, int userId, string username)
    {
        location = location.Trim();
        await _inventoryRepository.BulkUpdateLocationAsync(location, null);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = "LocationCleared",
            Details = $"Location '{location}' cleared from all items"
        });
    }

    public async Task<string> ExportToCsvAsync()
    {
        var items = await _inventoryRepository.GetAllAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Type,Name,Quantity,MinimumQuantity,SKU,Location,Category,ExpiryDate,Description,ScanWarning");
        foreach (var item in items)
        {
            var type = item is ReusableItem ? "Reusable" : "Consumable";
            sb.AppendLine(string.Join(",",
                CsvEscape(type),
                CsvEscape(item.Name),
                item.Quantity.ToString(),
                item.MinimumQuantity.ToString(),
                CsvEscape(item.SKU),
                CsvEscape(item.Location),
                CsvEscape(item.Category?.Name),
                CsvEscape(item.ExpiryDate?.ToString("yyyy-MM-dd")),
                CsvEscape(item.Description),
                CsvEscape(item.ScanWarning)));
        }
        return sb.ToString();
    }

    public async Task<(int Imported, int Failed, IReadOnlyList<string> Errors)> ImportFromCsvAsync(
        Stream csvStream, int userId, string username)
    {
        var errors = new List<string>();
        int imported = 0, failed = 0;

        var categories = (await _categoryRepository.GetAllAsync()).ToList();

        using var reader = new System.IO.StreamReader(csvStream);
        var header = await reader.ReadLineAsync();
        if (header is null) return (0, 0, errors);

        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Length < 2)
            {
                errors.Add($"Row {lineNum}: too few columns.");
                failed++;
                continue;
            }

            var typeStr  = cols.Length > 0 ? cols[0].Trim() : "";
            var name     = cols.Length > 1 ? cols[1].Trim() : "";
            var qtyStr   = cols.Length > 2 ? cols[2].Trim() : "0";
            var minStr   = cols.Length > 3 ? cols[3].Trim() : "0";
            var sku        = cols.Length > 4 ? cols[4].Trim() : null;
            var location   = cols.Length > 5 ? cols[5].Trim() : null;
            var catName    = cols.Length > 6 ? cols[6].Trim() : null;
            var expiryStr  = cols.Length > 7 ? cols[7].Trim() : null;
            var desc       = cols.Length > 8 ? cols[8].Trim() : null;
            var warning    = cols.Length > 9 ? cols[9].Trim() : null;

            DateOnly? expiryDate = null;
            if (!string.IsNullOrWhiteSpace(expiryStr) && DateOnly.TryParse(expiryStr, out var parsedExpiry))
                expiryDate = parsedExpiry;

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"Row {lineNum}: Name is required.");
                failed++;
                continue;
            }

            ItemType itemType;
            if (string.Equals(typeStr, "Reusable", StringComparison.OrdinalIgnoreCase))
                itemType = ItemType.Reusable;
            else if (string.Equals(typeStr, "Consumable", StringComparison.OrdinalIgnoreCase))
                itemType = ItemType.Consumable;
            else
            {
                errors.Add($"Row {lineNum}: Type must be 'Reusable' or 'Consumable' (got '{typeStr}').");
                failed++;
                continue;
            }

            if (!int.TryParse(qtyStr, out var qty) || qty < 0)
            {
                errors.Add($"Row {lineNum}: Quantity must be a non-negative integer.");
                failed++;
                continue;
            }

            int.TryParse(minStr, out var minQty);

            // Resolve category by name, creating it if needed
            int? categoryId = null;
            if (!string.IsNullOrWhiteSpace(catName))
            {
                var cat = categories.FirstOrDefault(c =>
                    string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase));
                if (cat is null)
                {
                    cat = await _categoryRepository.CreateAsync(new Category { Name = catName });
                    categories.Add(cat);
                }
                categoryId = cat.Id;
            }

            try
            {
                await CreateItemAsync(new CreateInventoryItemDto(
                    name, qty, string.IsNullOrWhiteSpace(desc) ? null : desc,
                    string.IsNullOrWhiteSpace(location) ? null : location,
                    string.IsNullOrWhiteSpace(sku) ? null : sku,
                    minQty, itemType,
                    string.IsNullOrWhiteSpace(warning) ? null : warning,
                    categoryId, expiryDate), userId, username);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {lineNum}: {ex.Message}");
                failed++;
            }
        }

        return (imported, failed, errors);
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        i++;
                        if (i < line.Length && line[i] == '"') { sb.Append('"'); i++; }
                        else break;
                    }
                    else { sb.Append(line[i]); i++; }
                }
                fields.Add(sb.ToString());
                if (i < line.Length && line[i] == ',') i++;
            }
            else
            {
                int start = i;
                while (i < line.Length && line[i] != ',') i++;
                fields.Add(line[start..i]);
                if (i < line.Length) i++;
            }
        }
        return fields.ToArray();
    }
}
