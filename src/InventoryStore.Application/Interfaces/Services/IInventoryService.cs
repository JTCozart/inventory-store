using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface IInventoryService
{
    Task<IEnumerable<InventoryItemDto>> GetAllItemsAsync();
    Task<IEnumerable<InventoryItemDto>> SearchItemsAsync(string query);
    Task<InventoryItemDto?> GetItemAsync(int id);
    Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemDto dto, int userId, string username);
    Task UpdateItemAsync(int id, UpdateInventoryItemDto dto, int userId, string username);
    Task DeleteItemAsync(int id, int userId, string username);
    Task<IEnumerable<InventoryItemDto>> GetLowStockItemsAsync();
    Task<IEnumerable<InventoryItemDto>> GetPublicItemsAsync();
    Task SetItemPublicAsync(int id, bool isPublic);
    Task<IEnumerable<LocationSummaryDto>> GetAllLocationsAsync();
    Task RenameLocationAsync(string from, string to, int userId, string username);
    Task ClearLocationAsync(string location, int userId, string username);
    Task SetItemLocationAsync(int itemId, string? location, int userId, string username);
    Task SetItemCategoryAsync(int itemId, int? categoryId, int userId, string username);
    Task<string> ExportToCsvAsync();
    Task<(int Imported, int Failed, IReadOnlyList<string> Errors)> ImportFromCsvAsync(Stream csvStream, int userId, string username);
}
