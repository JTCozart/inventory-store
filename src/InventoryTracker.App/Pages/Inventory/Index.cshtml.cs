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
    private readonly ICategoryService _categoryService;

    public IEnumerable<InventoryItemDto> Items { get; private set; } = [];
    public IEnumerable<CategoryDto> Categories { get; private set; } = [];
    public string? Query { get; private set; }
    public int? Open { get; private set; }
    public int? CategoryFilter { get; private set; }
    public string? ImportResult { get; private set; }
    public int TotalCount { get; private set; }
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Query) || CategoryFilter.HasValue;

    public IndexModel(IInventoryService inventoryService, ICheckoutService checkoutService, ICategoryService categoryService)
    {
        _inventoryService = inventoryService;
        _checkoutService  = checkoutService;
        _categoryService  = categoryService;
    }

    public async Task OnGetAsync(string? q, int? open, int? category, string? importResult)
    {
        Query          = q;
        Open           = open;
        CategoryFilter = category;
        ImportResult   = importResult;
        Categories     = await _categoryService.GetAllAsync();

        var items = string.IsNullOrWhiteSpace(q)
            ? await _inventoryService.GetAllItemsAsync()
            : await _inventoryService.SearchItemsAsync(q);

        TotalCount = items.Count();

        Items = category.HasValue
            ? items.Where(i => i.CategoryId == category.Value)
            : items;
    }

    public async Task<IActionResult> OnGetItemStatusAsync(int id)
    {
        var status = await _checkoutService.GetItemStatusAsync(id);
        return new JsonResult(status, AppJsonOptions.Web);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!CanWrite()) return Forbid();
        var (uid, uname) = GetUser();
        await _inventoryService.DeleteItemAsync(id, uid, uname);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetEditItemDataAsync(int id)
    {
        var item = await _inventoryService.GetItemAsync(id);
        if (item is null) return new JsonResult(new { success = false, error = "Item not found." });
        var categories = (await _categoryService.GetAllAsync())
            .Select(c => new { c.Id, c.Name, c.Color });
        var locations = (await _inventoryService.GetAllLocationsAsync())
            .Select(l => l.Name);
        return new JsonResult(new { success = true, item, categories, locations }, AppJsonOptions.Web);
    }

    public async Task<IActionResult> OnPostEditItemAsync(
        int id, string name, int quantity, int minimumQuantity,
        string? sku, string? location, int? categoryId, string? expiryDate,
        string? scanWarning, string? description)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        var (uid, uname) = GetUser();
        DateOnly? expiry = null;
        if (!string.IsNullOrWhiteSpace(expiryDate) && DateOnly.TryParse(expiryDate, out var parsed))
            expiry = parsed;
        try
        {
            await _inventoryService.UpdateItemAsync(id, new UpdateInventoryItemDto(
                name, quantity, description, location, sku, minimumQuantity, scanWarning, categoryId, expiry
            ), uid, uname);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostCreateCategoryAsync(string name, string? color)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        try
        {
            var cat = await _categoryService.CreateAsync(new CreateCategoryDto(name, color));
            return new JsonResult(new { id = cat.Id, name = cat.Name, color = cat.Color });
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    public async Task<IActionResult> OnPostUpdateItemAsync(
        int id, string name, int quantity, string? description, string? location,
        string? sku, int minimumQuantity, string? scanWarning)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        var (uid, uname) = GetUser();
        var existing = await _inventoryService.GetItemAsync(id);
        await _inventoryService.UpdateItemAsync(id, new UpdateInventoryItemDto(
            name, quantity, description, location, sku, minimumQuantity, scanWarning,
            existing?.CategoryId, existing?.ExpiryDate
        ), uid, uname);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostImportCsvAsync(IFormFile? csvFile)
    {
        if (!CanWrite()) return RedirectToPage();
        if (csvFile is null || csvFile.Length == 0)
            return RedirectToPage(new { importResult = "error:No file selected." });

        var (uid, uname) = GetUser();
        await using var stream = csvFile.OpenReadStream();
        var (imported, failed, errors) = await _inventoryService.ImportFromCsvAsync(stream, uid, uname);

        var msg = failed == 0
            ? $"ok:{imported} item(s) imported successfully."
            : $"warn:{imported} imported, {failed} failed. {string.Join(" | ", errors.Take(5))}";
        return RedirectToPage(new { importResult = msg });
    }

    public async Task<IActionResult> OnPostCheckOutItemAsync(
        int itemId, string checkedOutBy, int quantity, string? notes)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        var (uid, uname) = GetUser();
        try
        {
            var record = await _checkoutService.CheckOutAsync(
                new CheckOutItemDto(itemId, checkedOutBy, quantity, notes), uid, uname);
            return new JsonResult(new { success = true, record }, AppJsonOptions.Web);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostCheckInItemAsync(int recordId, string? notes)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        var (uid, uname) = GetUser();
        await _checkoutService.CheckInAsync(new CheckInItemDto(recordId, notes), uid, uname);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostMarkLostItemAsync(int recordId, string? notes)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        var (uid, uname) = GetUser();
        await _checkoutService.MarkLostAsync(new MarkLostDto(recordId, notes), uid, uname);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostMarkFoundItemAsync(int recordId, string? notes)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        var (uid, uname) = GetUser();
        await _checkoutService.MarkFoundAsync(new MarkFoundDto(recordId, notes), uid, uname);
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostConsumeItemAsync(int itemId, int quantity, string? notes)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        var (uid, uname) = GetUser();
        try
        {
            await _checkoutService.ConsumeAsync(new ConsumeItemDto(itemId, quantity, notes), uid, uname);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostRestockItemAsync(int itemId, int quantity, string? notes)
    {
        if (!CanWrite()) return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        var (uid, uname) = GetUser();
        await _checkoutService.RestockAsync(new RestockItemDto(itemId, quantity, notes), uid, uname);
        return new JsonResult(new { success = true });
    }

    private (int userId, string username) GetUser() => User.GetIdentity();
    private bool CanWrite() => User.IsInRole("Admin") || User.IsInRole("Manager");
}
