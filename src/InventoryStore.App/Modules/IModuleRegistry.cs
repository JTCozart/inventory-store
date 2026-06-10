namespace InventoryStore.App.Modules;

// Central list of optional modules plus their enabled-state, backed by the AppSetting store.
// Endpoints, the Settings screen, and the layout all read module state through here so adding
// a new module is a matter of registering one descriptor.
public interface IModuleRegistry
{
    IReadOnlyList<ModuleDescriptor> All { get; }
    ModuleDescriptor? Find(string key);
    Task<bool> IsEnabledAsync(string key);
    Task SetEnabledAsync(string key, bool enabled);
    Task<IReadOnlyDictionary<string, bool>> GetEnabledMapAsync();
}
