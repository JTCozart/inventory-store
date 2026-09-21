using System.Text.Json.Serialization;

namespace InventoryStore.App.Api;

// ── Error envelope ────────────────────────────────────────────────────────
// Every non-2xx response from /api/v1 has this shape, so callers can branch on Error.Code
// instead of parsing messages. Requested / Available are only present on INSUFFICIENT_QUANTITY.
public sealed record ApiErrorResponse(ApiError Error);

public sealed record ApiError(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Requested = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Available = null);

// ── Requests ──────────────────────────────────────────────────────────────
public sealed record ConsumeItemRequest(int Quantity, string? Notes = null);
public sealed record RestockItemRequest(int Quantity, string? Notes = null);

// ── Responses ─────────────────────────────────────────────────────────────
public sealed record ConsumableDto(
    int Id,
    string Name,
    string? Sku,
    int Quantity,
    int MinimumQuantity,
    bool IsLowStock,
    string? Location,
    string? Category,
    DateOnly? ExpiryDate,
    DateTime UpdatedAt);

public sealed record ConsumableListResponse(int Count, IReadOnlyList<ConsumableDto> Items);

// Result of a consume / restock: the change applied and the stock level afterwards.
public sealed record StockChangeResponse(
    int Id,
    string Name,
    string? Sku,
    string Change,
    int Amount,
    int PreviousQuantity,
    int Quantity,
    bool IsLowStock);
