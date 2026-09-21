using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface ICheckoutService
{
    Task<ItemStatusDto> GetItemStatusAsync(int inventoryItemId);
    Task<ItemStatusDto?> GetItemStatusBySkuAsync(string sku);
    Task<CheckoutRecordDto> CheckOutAsync(CheckOutItemDto dto, int userId, string username);
    Task<CheckoutRecordDto> CheckInAsync(CheckInItemDto dto, int userId, string username);
    Task<CheckoutRecordDto> MarkLostAsync(MarkLostDto dto, int userId, string username);
    Task<CheckoutRecordDto> MarkFoundAsync(MarkFoundDto dto, int userId, string username);
    // userId is null for non-interactive callers (the Public API), which identify by username only.
    Task ConsumeAsync(ConsumeItemDto dto, int? userId, string username);
    Task RestockAsync(RestockItemDto dto, int? userId, string username);
    Task<IEnumerable<CheckoutRecordDto>> GetAllActiveCheckoutsAsync();
    Task<IEnumerable<CheckoutRecordDto>> GetCheckoutHistoryAsync(int inventoryItemId);
    Task<IEnumerable<CheckoutRecordDto>> GetClientHistoryAsync(int clientId);
}
