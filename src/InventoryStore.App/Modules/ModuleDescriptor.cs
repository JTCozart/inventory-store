namespace InventoryStore.App.Modules;

// Describes one optional, toggleable module. Built-in modules are registered in ModuleRegistry;
// the descriptor is the seam that future user-added modules would also plug into.
public record ModuleDescriptor(
    string Key,            // stable id, e.g. "sds", "cost", "forecast", "webhooks"
    string Name,           // display name
    string Description,    // one-line blurb shown on the Modules screen
    string Icon,           // bootstrap-icon class, e.g. "bi-eyedropper"
    bool HasConfigure,     // whether a Settings > Modules > [module] > Configure screen exists
    string RequiredRole = "Admin")
{
    // The AppSetting key that stores whether this module is enabled.
    public string SettingKey => $"module.{Key}.enabled";
}
