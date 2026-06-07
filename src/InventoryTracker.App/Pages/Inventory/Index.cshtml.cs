using InventoryTracker.App.Extensions;
using InventoryTracker.App.Infrastructure;
using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages.Inventory;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IInventoryService _inventoryService;
    private readonly ICheckoutService _checkoutService;

    public IEnumerable<InventoryItemDto> Items { get; private set; } = [];
    public string? Query { get; private set; }
    public int? Open { get; private set; }

    public IndexModel(IInventoryService inventoryService, ICheckoutService checkoutService)
    {
        _inventoryService = inventoryService;
        _checkoutService = checkoutService;
    }

    public async Task OnGetAsync(string? q, int? open)
    {
        Query = q;
        Open  = open;
        Items = string.IsNullOrWhiteSpace(q)
            ? await _inventoryService.GetAllItemsAsync()
            : await _inventoryService.SearchItemsAsync(q);
    }

    public async Task<IActionResult> OnGetItemStatusAsync(int id)
    {
        var status = await _checkoutService.GetItemStatusAsync(id);
        return new JsonResult(status, AppJsonOptions.Web);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var (uid, uname) = GetUser();
        await _inventoryService.DeleteItemAsync(id, uid, uname);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateItemAsync(
        int id, string name, int quantity, string? description, string? location,
        string? sku, int minimumQuantity, string? scanWarning)
    {
        var (uid, uname) = GetUser();
        await _inventoryService.UpdateItemAsync(id, new UpdateInventoryItemDto(
            name, quantity, description, location, sku, minimumQuantity, scanWarning
        ), uid, uname);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostCheckOutItemAsync(
        int itemId, string checkedOutBy, int quantity, string? notes)
    {
        var (uid, uname) = GetUser();
        var record = await _checkoutService.CheckOutAsync(
            new CheckOutItemDto(itemId, checkedOutBy, quantity, notes), uid, uname);
        return new JsonResult(new { success = true, record }, AppJsonOptions.Web);
    }

    public async Task<IActionResult> OnPostCheckInItemAsync(int recordId, string? notes)
    {
        var (uid, uname) = GetUser();
        await _checkoutService.CheckInAsync(new CheckInItemDto(recordId, notes), uid, uname);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostMarkLostItemAsync(int recordId, string? notes)
    {
        var (uid, uname) = GetUser();
        await _checkoutService.MarkLostAsync(new MarkLostDto(recordId, notes), uid, uname);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostConsumeItemAsync(int itemId, int quantity, string? notes)
    {
        var (uid, uname) = GetUser();
        await _checkoutService.ConsumeAsync(new ConsumeItemDto(itemId, quantity, notes), uid, uname);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostRestockItemAsync(int itemId, int quantity, string? notes)
    {
        var (uid, uname) = GetUser();
        await _checkoutService.RestockAsync(new RestockItemDto(itemId, quantity, notes), uid, uname);
        return new JsonResult(new { success = true });
    }

    private (int userId, string username) GetUser() => User.GetIdentity();
}
