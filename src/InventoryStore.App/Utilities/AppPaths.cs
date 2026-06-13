namespace InventoryStore.App.Utilities;

internal static class AppPaths
{
    // Root directory for persistent data (SQLite db, downloaded tools, keys).
    //
    // Set INVENTORYSTORE_DATA to override — the Linux systemd unit points it at
    // /var/lib/inventorystore, which the service user can write to. When unset it
    // falls back to the per-OS common application-data folder (C:\ProgramData
    // on Windows), preserving the original Windows behaviour.
    internal static string DataDir
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable("INVENTORYSTORE_DATA");
            return !string.IsNullOrWhiteSpace(overridden)
                ? overridden
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "InventoryStore");
        }
    }
}
