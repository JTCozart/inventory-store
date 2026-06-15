using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface IKitService
{
    // Edit-mode wiring: link / unlink / re-quantify a kit's members.
    Task LinkComponentAsync(LinkKitComponentDto dto, int userId, string username);
    Task UnlinkComponentAsync(int kitItemId, int componentItemId, int userId, string username);
    Task SetComponentQuantityAsync(int kitItemId, int componentItemId, int quantity, int userId, string username);

    // Whole-kit actions. Checkout/consume return a result that may ask the UI to confirm a
    // partial run when some members are short.
    Task<KitActionResultDto> CheckOutKitAsync(KitActionDto dto, int userId, string username);
    Task<KitActionResultDto> ConsumeKitAsync(KitActionDto dto, int userId, string username);
    Task RestockKitAsync(int kitItemId, int quantity, int userId, string username);
    Task CheckInKitAsync(int kitCheckoutId, int userId, string username);
    Task MarkKitLostAsync(int kitCheckoutId, int userId, string username);
}
