using InventoryTracker.Application.DTOs;

namespace InventoryTracker.Application.Interfaces.Services;

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
    Task<string> ExportToCsvAsync();
    Task<(int Imported, int Failed, IReadOnlyList<string> Errors)> ImportFromCsvAsync(Stream csvStream, int userId, string username);
}
