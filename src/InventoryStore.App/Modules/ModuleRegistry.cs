using InventoryStore.Application.Interfaces.Services;

namespace InventoryStore.App.Modules;

public class ModuleRegistry : IModuleRegistry
{
    private readonly ISettingsService _settings;

    // Built-in modules. Future user-added modules would extend this list (e.g. from a plugins
    // folder); the descriptor + this interface are the seam, so nothing else needs to change.
    private static readonly IReadOnlyList<ModuleDescriptor> _modules = new[]
    {
        new ModuleDescriptor("sds", "Safety Data Sheets",
            "Look up chemical hazard data (GHS) for items from PubChem and keep it on file.",
            "bi-eyedropper", HasConfigure: true),
        new ModuleDescriptor("cost", "Cost & Valuation",
            "Record a unit cost per item and see total inventory value and depreciation.",
            "bi-cash-coin", HasConfigure: true),
        new ModuleDescriptor("forecast", "Consumption Forecasting",
            "Project run-out dates for consumables from their real usage history.",
            "bi-graph-down-arrow", HasConfigure: true),
        new ModuleDescriptor("webhooks", "Webhooks & Integrations",
            "Send signed JSON to external URLs when inventory events happen.",
            "bi-broadcast", HasConfigure: true),
        new ModuleDescriptor("kits", "Kits & Bundles",
            "Group items into kits that can be checked out, used, or restocked all at once.",
            "bi-box-seam", HasConfigure: true),
        new ModuleDescriptor("maintenance", "Maintenance",
            "Track service schedules, send items out to vendors, and get due / overdue alerts.",
            "bi-tools", HasConfigure: true),
        new ModuleDescriptor("ai", "AI Assistant",
            "Ask questions about your inventory in plain language — checkouts, stock levels, kits, and maintenance.",
            "bi-robot", HasConfigure: true),
        new ModuleDescriptor("email", "Email Delivery",
            "Send password-reset links by email using your own Mailjet account.",
            "bi-envelope", HasConfigure: true),
    };

    public ModuleRegistry(ISettingsService settings) => _settings = settings;

    public IReadOnlyList<ModuleDescriptor> All => _modules;

    public ModuleDescriptor? Find(string key) =>
        _modules.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

    public async Task<bool> IsEnabledAsync(string key)
    {
        var desc = Find(key);
        if (desc is null) return false;
        return await _settings.GetAsync(desc.SettingKey) == "true";
    }

    public async Task SetEnabledAsync(string key, bool enabled)
    {
        var desc = Find(key) ?? throw new ArgumentException($"Unknown module '{key}'.", nameof(key));
        await _settings.SetAsync(desc.SettingKey, enabled ? "true" : null);
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetEnabledMapAsync()
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _modules)
            map[m.Key] = await _settings.GetAsync(m.SettingKey) == "true";
        return map;
    }
}
