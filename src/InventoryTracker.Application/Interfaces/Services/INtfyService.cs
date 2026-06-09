namespace InventoryTracker.Application.Interfaces.Services;

public interface INtfyService
{
    Task NotifyCheckoutAsync(string itemName, string checkedOutBy, string? clientName);
    Task NotifyCheckinAsync(string itemName, string checkedInBy);
    Task NotifyLostAsync(string itemName, string checkedOutBy);
    Task NotifyLowStockAsync(string itemName, int available, int minimum);
    Task NotifyLoginAsync(string username);
    Task<(bool ok, int statusCode)> SendTestAsync();
}
