using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using InventoryTracker.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages.Management;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly IUserAuthService _authService;

    public IEnumerable<UserDto> Users { get; private set; } = [];
    public string Tab { get; private set; } = "users";
    public string? CreateError { get; private set; }

    [BindProperty]
    public CreateUserInputModel NewUser { get; set; } = new();

    public IndexModel(IUserAuthService authService)
    {
        _authService = authService;
    }

    public async Task OnGetAsync(string tab = "users")
    {
        Tab = tab;
        Users = await _authService.GetAllUsersAsync();
    }

    public async Task<IActionResult> OnPostCreateUserAsync()
    {
        try
        {
            await _authService.CreateUserAsync(new CreateUserDto(
                NewUser.Username, null, null, NewUser.Email, NewUser.Password, NewUser.Role));
            return RedirectToPage(new { tab = "users" });
        }
        catch (Exception ex)
        {
            CreateError = ex.Message;
            Users = await _authService.GetAllUsersAsync();
            Tab = "users";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(int userId)
    {
        try
        {
            await _authService.DeleteUserAsync(userId);
            return RedirectToPage(new { tab = "users" });
        }
        catch (Exception ex)
        {
            CreateError = ex.Message;
            Users = await _authService.GetAllUsersAsync();
            Tab = "users";
            return Page();
        }
    }

    public class CreateUserInputModel
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Viewer;
    }
}
