using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Clients;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IClientService _clientService;

    public IEnumerable<ClientDto> Clients { get; private set; } = [];
    public string? Query { get; private set; }
    public int? Open { get; private set; }
    public bool CanManage => User.IsInRole("Admin") || User.IsInRole("Manager");

    public IndexModel(IClientService clientService) => _clientService = clientService;

    public async Task OnGetAsync(string? q, int? open)
    {
        Query = q;
        Open  = open;
        Clients = string.IsNullOrWhiteSpace(q)
            ? await _clientService.GetAllAsync()
            : await _clientService.SearchAsync(q);
    }
}
