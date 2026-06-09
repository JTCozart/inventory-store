using System.ComponentModel.DataAnnotations;
using InventoryStore.App.Extensions;
using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Inventory;

[Authorize(Roles = "Admin,Manager")]
public class CreateModel : PageModel
{
    private readonly IInventoryService _inventoryService;
    private readonly ICategoryService _categoryService;

    [BindProperty]
    public CreateInputModel Input { get; set; } = new();

    public IEnumerable<CategoryDto> Categories { get; private set; } = [];
    public IEnumerable<string> Locations { get; private set; } = [];

    public CreateModel(IInventoryService inventoryService, ICategoryService categoryService)
    {
        _inventoryService = inventoryService;
        _categoryService  = categoryService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Categories = await _categoryService.GetAllAsync();
        Locations  = (await _inventoryService.GetAllLocationsAsync()).Select(l => l.Name);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateCategoryAsync(string name, string? color)
    {
        try
        {
            var cat = await _categoryService.CreateAsync(new CreateCategoryDto(name, color));
            return new JsonResult(new { id = cat.Id, name = cat.Name, color = cat.Color });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Categories = await _categoryService.GetAllAsync();
            return Page();
        }

        var (userId, username) = User.GetIdentity();
        await _inventoryService.CreateItemAsync(new CreateInventoryItemDto(
            Input.Name, Input.Quantity, Input.Description, Input.Location, Input.SKU,
            Input.MinimumQuantity, Input.ItemType, Input.ScanWarning, Input.CategoryId, Input.ExpiryDate
        ), userId, username);

        return RedirectToPage("./Index");
    }

    public class CreateInputModel
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        public string? Description { get; set; }
        public string? Location { get; set; }
        public string? SKU { get; set; }

        [Range(0, int.MaxValue)]
        public int MinimumQuantity { get; set; }

        public ItemType ItemType { get; set; } = ItemType.Consumable;

        [MaxLength(500)]
        public string? ScanWarning { get; set; }

        public int? CategoryId { get; set; }
        public DateOnly? ExpiryDate { get; set; }
    }
}
