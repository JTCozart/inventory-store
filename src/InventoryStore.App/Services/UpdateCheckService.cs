using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryStore.App.Services;

public class UpdateInfo
{
    public bool    HasUpdate      { get; set; }
    public string  CurrentVersion { get; set; } = "0.0.0";
    public string? LatestVersion  { get; set; }
    public string? ReleaseUrl     { get; set; }
    public string? ReleaseNotes   { get; set; }
    public DateTime? LastChecked  { get; set; }
}

public class UpdateCheckService : BackgroundService
{
    private readonly UpdateInfo _info;
    private readonly IConfiguration _config;
    private readonly ILogger<UpdateCheckService> _logger;

    public UpdateCheckService(UpdateInfo info, IConfiguration config, ILogger<UpdateCheckService> logger)
    {
        _info   = info;
        _config = config;
        _logger = logger;

        // Version comes from one of two places, in order:
        //  1. appsettings ("AppVersion") - the Windows installer rewrites this at install time.
        //  2. The assembly's InformationalVersion - stamped into both the Windows and Linux
        //     builds by the release workflow (-p:InformationalVersion=...). We read it from the
        //     assembly attribute rather than FileVersionInfo because Assembly.Location is empty
        //     in a single-file published app (the Linux build), which would otherwise fall to "dev".
        var configured = config["AppVersion"];
        if (!string.IsNullOrWhiteSpace(configured) && configured != "dev")
        {
            _info.CurrentVersion = configured.TrimStart('v');
        }
        else
        {
            _info.CurrentVersion = ReadAssemblyVersion() ?? "dev";
        }
    }

    private static string? ReadAssemblyVersion()
    {
        // The release workflow stamps this (-p:InformationalVersion=YYYYMMDD.HHMM) on both the
        // Windows and Linux builds. Unset in local dev, where it defaults to the assembly version
        // ("1.0.0"); treat that as no version so dev shows "dev". May carry a "+<commit>" suffix.
        var info = typeof(UpdateCheckService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?.Split('+')[0].TrimStart('v');

        return string.IsNullOrWhiteSpace(info) || info == "1.0.0" || info == "0.0.0" ? null : info;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Initial check after a short delay to not block startup
        await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
        if (!ct.IsCancellationRequested) await CheckAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(24), ct).ConfigureAwait(false);
            if (!ct.IsCancellationRequested) await CheckAsync(ct);
        }
    }

    public Task CheckNowAsync() => CheckAsync(CancellationToken.None);

    private async Task CheckAsync(CancellationToken ct)
    {
        var repo = _config["GitHub:Repository"];
        if (string.IsNullOrWhiteSpace(repo)) return;

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryStore/1.0");
            http.Timeout = TimeSpan.FromSeconds(10);

            var json = await http.GetStringAsync(
                $"https://api.github.com/repos/{repo}/releases/latest", ct);

            using var doc   = JsonDocument.Parse(json);
            var root        = doc.RootElement;
            var tagName     = root.GetProperty("tag_name").GetString() ?? "";
            var latestVer   = tagName.TrimStart('v');
            var releaseUrl  = root.GetProperty("html_url").GetString();
            var body        = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            _info.LatestVersion = latestVer;
            _info.ReleaseUrl    = releaseUrl;
            _info.ReleaseNotes  = body;
            _info.LastChecked   = DateTime.UtcNow;
            _info.HasUpdate     = IsNewer(latestVer, _info.CurrentVersion);

            _logger.LogInformation("Update check: current={Current} latest={Latest} hasUpdate={Has}",
                _info.CurrentVersion, latestVer, _info.HasUpdate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Update check failed: {Msg}", ex.Message);
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        // Tags are vYYYYMMDD.HHMM — strip 'v' and compare lexicographically (date format sorts correctly)
        if (current == "dev") return false;
        var l = latest.TrimStart('v');
        var c = current.TrimStart('v');
        return string.Compare(l, c, StringComparison.Ordinal) > 0;
    }
}
