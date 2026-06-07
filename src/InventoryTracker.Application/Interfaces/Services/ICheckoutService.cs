using InventoryTracker.Application.DTOs;

namespace InventoryTracker.Application.Interfaces.Services;

public interface ICheckoutService
{
    Task<ItemStatusDto> GetItemStatusAsync(int inventoryItemId);
    Task<ItemStatusDto?> GetItemStatusBySkuAsync(string sku);
    Task<CheckoutRecordDto> CheckOutAsync(CheckOutItemDto dto, int userId, string username);
    Task<CheckoutRecordDto> CheckInAsync(CheckInItemDto dto, int userId, string username);
    Task<CheckoutRecordDto> MarkLostAsync(MarkLostDto dto, int userId, string username);
    Task ConsumeAsync(ConsumeItemDto dto, int userId, string username);
    Task RestockAsync(RestockItemDto dto, int userId, string username);
    Task<IEnumerable<CheckoutRecordDto>> GetAllActiveCheckoutsAsync();
    Task<IEnumerable<CheckoutRecordDto>> GetCheckoutHistoryAsync(int inventoryItemId);
}
