namespace InventoryStore.Application.Interfaces.Services;

public interface ISettingsService
{
    // The admin-configured public base URL for this instance (e.g. https://inventory.example.com).
    // Used wherever an absolute, externally-reachable URL needs to be built server-side --
    // currently password-reset links -- instead of trusting the inbound request's Host header,
    // which is attacker-controllable and must never be trusted for a URL carrying a bearer token.
    const string PublicBaseUrlSettingKey = "app.publicBaseUrl";

    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string? value);
    Task<Dictionary<string, string?>> GetAllAsync();
}
