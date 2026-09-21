using System.Text.Json;
using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Enums;

namespace InventoryStore.App.Api;

// The Public API: keyed, consumables-only access to stock levels and usage. Mounted under
// /api/v1 and documented by Swagger (see the "v1" document in Program.cs).
internal static class PublicApi
{
    private const int MaxNotesLength = 500;

    internal static IResult Error(int status, string code, string message, int? requested = null, int? available = null) =>
        Results.Json(new ApiErrorResponse(new ApiError(code, message, requested, available)), statusCode: status);

    internal static IEndpointRouteBuilder MapPublicApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1")
            .WithGroupName("v1")
            .WithTags("Consumables")
            .AddEndpointFilter<PublicApiFilter>();

        api.MapGet("/consumables", ListAsync)
            .WithSummary("List consumables")
            .WithDescription("Returns every consumable item. Use q to filter by name, SKU or location, and lowStock=true to return only items at or below their minimum quantity.")
            .Produces<ConsumableListResponse>()
            .ProducesApiErrors(401, 403, 404);

        api.MapGet("/consumables/{id:int}", GetByIdAsync)
            .WithSummary("Get a consumable by id")
            .Produces<ConsumableDto>()
            .ProducesApiErrors(401, 403, 404, 409);

        api.MapGet("/consumables/by-sku/{sku}", GetBySkuAsync)
            .WithSummary("Get a consumable by SKU")
            .WithDescription("SKU match is exact and case-insensitive.")
            .Produces<ConsumableDto>()
            .ProducesApiErrors(401, 403, 404);

        api.MapPost("/consumables/{id:int}/consume", ConsumeAsync)
            .WithSummary("Record usage")
            .WithDescription("Deducts quantity from stock. Fails with INSUFFICIENT_QUANTITY (409) when the amount requested is more than what is in stock, and the response includes both requested and available.")
            .Accepts<ConsumeItemRequest>("application/json")
            .Produces<StockChangeResponse>()
            .ProducesApiErrors(400, 401, 403, 404, 409);

        api.MapPost("/consumables/{id:int}/restock", RestockAsync)
            .WithSummary("Add stock")
            .WithDescription("Adds quantity to stock, for example to correct an over-reported usage.")
            .Accepts<RestockItemRequest>("application/json")
            .Produces<StockChangeResponse>()
            .ProducesApiErrors(400, 401, 403, 404, 409);

        return endpoints;
    }

    private static RouteHandlerBuilder ProducesApiErrors(this RouteHandlerBuilder b, params int[] statuses)
    {
        foreach (var s in statuses) b.Produces<ApiErrorResponse>(s);
        return b;
    }

    // ── Handlers ──────────────────────────────────────────────────────────
    private static async Task<IResult> ListAsync(string? q, bool? lowStock, IInventoryService inventory)
    {
        var items = (await inventory.GetAllItemsAsync()).Where(i => i.ItemType == ItemType.Consumable);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            items = items.Where(i =>
                i.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (i.SKU?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Location?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (lowStock == true)
            items = items.Where(i => i.IsLowStock);

        var list = items.OrderBy(i => i.Name).Select(ToDto).ToList();
        return Results.Ok(new ConsumableListResponse(list.Count, list));
    }

    private static async Task<IResult> GetByIdAsync(int id, IInventoryService inventory)
    {
        var item = await inventory.GetItemAsync(id);
        if (item is null) return ItemNotFound();
        if (item.ItemType != ItemType.Consumable) return NotConsumable(item);
        return Results.Ok(ToDto(item));
    }

    private static async Task<IResult> GetBySkuAsync(string sku, IInventoryService inventory)
    {
        sku = sku.Trim();
        var match = (await inventory.SearchItemsAsync(sku))
            .FirstOrDefault(i => string.Equals(i.SKU, sku, StringComparison.OrdinalIgnoreCase));
        // A SKU that belongs to a reusable or kit is reported as not found: this API only exposes consumables.
        if (match is null || match.ItemType != ItemType.Consumable) return ItemNotFound();
        return Results.Ok(ToDto(match));
    }

    private static async Task<IResult> ConsumeAsync(
        int id, HttpContext http, IInventoryService inventory, ICheckoutService checkout)
    {
        var (body, bad) = await ReadBodyAsync<ConsumeItemRequest>(http);
        if (bad is not null) return bad;

        var invalid = Validate(body!.Quantity, body.Notes);
        if (invalid is not null) return invalid;

        var item = await inventory.GetItemAsync(id);
        if (item is null) return ItemNotFound();
        if (item.ItemType != ItemType.Consumable) return NotConsumable(item);

        await checkout.ConsumeAsync(new ConsumeItemDto(id, body.Quantity, Clean(body.Notes)), null, ActorName(http));
        return await ChangeResultAsync(inventory, item, "consume", body.Quantity);
    }

    private static async Task<IResult> RestockAsync(
        int id, HttpContext http, IInventoryService inventory, ICheckoutService checkout)
    {
        var (body, bad) = await ReadBodyAsync<RestockItemRequest>(http);
        if (bad is not null) return bad;

        var invalid = Validate(body!.Quantity, body.Notes);
        if (invalid is not null) return invalid;

        var item = await inventory.GetItemAsync(id);
        if (item is null) return ItemNotFound();
        if (item.ItemType != ItemType.Consumable) return NotConsumable(item);

        await checkout.RestockAsync(new RestockItemDto(id, body.Quantity, Clean(body.Notes)), null, ActorName(http));
        return await ChangeResultAsync(inventory, item, "restock", body.Quantity);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    // Re-read after the change rather than computing previous - amount, so the response reflects
    // what is actually stored even if another caller changed the stock in between.
    private static async Task<IResult> ChangeResultAsync(IInventoryService inventory, InventoryItemDto before, string change, int amount)
    {
        var after = await inventory.GetItemAsync(before.Id) ?? before;
        return Results.Ok(new StockChangeResponse(
            before.Id, before.Name, before.SKU, change, amount,
            before.Quantity, after.Quantity, after.IsLowStock));
    }

    private static string ActorName(HttpContext http) =>
        "api:" + (http.Items[PublicApiFilter.KeyNameItem] as string ?? "unknown");

    private static string? Clean(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    private static IResult? Validate(int quantity, string? notes)
    {
        if (quantity <= 0)
            return Error(StatusCodes.Status400BadRequest, "INVALID_QUANTITY", "quantity must be a whole number greater than zero.");
        if (notes is { Length: > MaxNotesLength })
            return Error(StatusCodes.Status400BadRequest, "INVALID_REQUEST", $"notes must be {MaxNotesLength} characters or fewer.");
        return null;
    }

    private static async Task<(T? Body, IResult? Error)> ReadBodyAsync<T>(HttpContext http) where T : class
    {
        try
        {
            var body = await http.Request.ReadFromJsonAsync<T>();
            if (body is null)
                return (null, Error(StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A JSON request body is required."));
            return (body, null);
        }
        catch (JsonException)
        {
            return (null, Error(StatusCodes.Status400BadRequest, "INVALID_REQUEST",
                "The request body is not valid JSON. Expected {\"quantity\": 1, \"notes\": \"optional\"}."));
        }
        catch (InvalidOperationException)
        {
            // ReadFromJsonAsync throws this when Content-Type is not JSON.
            return (null, Error(StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Content-Type must be application/json."));
        }
    }

    private static IResult ItemNotFound() =>
        Error(StatusCodes.Status404NotFound, "ITEM_NOT_FOUND", "No item with that id or SKU exists.");

    private static IResult NotConsumable(InventoryItemDto item) =>
        Error(StatusCodes.Status409Conflict, "NOT_CONSUMABLE",
            $"'{item.Name}' is a {item.ItemType.ToString().ToLowerInvariant()} item. The API only handles consumables.");

    private static ConsumableDto ToDto(InventoryItemDto i) => new(
        i.Id, i.Name, i.SKU, i.Quantity, i.MinimumQuantity, i.IsLowStock,
        i.Location, i.CategoryName, i.ExpiryDate, i.UpdatedAt);
}
