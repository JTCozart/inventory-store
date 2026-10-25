namespace InventoryStore.App.Logging;

/// <summary>
/// Where the app's log file goes. The standard is in the GitHub folder's .claude/logging.md, and the PulseWatch agent
/// on each server follows the file this produces. Keep this file identical in HostRouter, BingoStage, CRM Plus and
/// Inventory Store, apart from the namespace.
/// </summary>
public static class CentralLogFolder
{
    public const string RootEnvironmentVariable = "COZART_LOG_DIR";

    public static string DefaultRoot(bool isWindows, string commonApplicationData) =>
        isWindows ? Path.Combine(commonApplicationData, "Cozart", "Logs") : "/var/log/cozart";

    public static string ChooseRoot(string? configuredRoot, bool isWindows, string commonApplicationData) =>
        string.IsNullOrWhiteSpace(configuredRoot) ? DefaultRoot(isWindows, commonApplicationData) : configuredRoot.Trim();

    /// <summary>True when the folder exists (or can be created) and a file can be written in it.</summary>
    public static bool CanWriteTo(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public static class CentralLogging
{
    public const string SourceEnvironmentVariable = "COZART_LOG_SOURCE";

    /// <summary>
    /// Sets the two variables the shared log4net.config reads and returns the log file's path. When the central folder
    /// cannot be written (not created yet, wrong owner), the app logs under <paramref name="fallbackRoot"/> instead of
    /// losing its logs, and says so on stderr because the PulseWatch agent will not find that file.
    /// </summary>
    public static string PrepareEnvironment(string sourceId, string fallbackRoot)
    {
        var configuredRoot = Environment.GetEnvironmentVariable(CentralLogFolder.RootEnvironmentVariable);
        var root = CentralLogFolder.ChooseRoot(configuredRoot, OperatingSystem.IsWindows(),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

        if (!CentralLogFolder.CanWriteTo(Path.Combine(root, sourceId)))
        {
            Console.Error.WriteLine($"Log folder {root} is not writable for {sourceId}. Logging under {fallbackRoot} instead; " +
                                    "the PulseWatch agent will not see these logs until the folder is fixed.");
            root = fallbackRoot;
        }

        Environment.SetEnvironmentVariable(CentralLogFolder.RootEnvironmentVariable, root);
        Environment.SetEnvironmentVariable(SourceEnvironmentVariable, sourceId);
        return Path.Combine(root, sourceId, sourceId + ".log");
    }

    public static ILoggingBuilder AddCentralLog4Net(this ILoggingBuilder logging, string sourceId, string? fallbackRoot = null)
    {
        PrepareEnvironment(sourceId, fallbackRoot ?? Path.Combine(AppContext.BaseDirectory, "logs"));
        return logging.AddLog4Net(Path.Combine(AppContext.BaseDirectory, "log4net.config"));
    }
}
