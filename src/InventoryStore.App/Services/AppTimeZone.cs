using InventoryStore.App.Utilities;
using InventoryStore.Application.Interfaces.Services;

namespace InventoryStore.App.Services;

/// <summary>
/// Holds the single, instance-wide display time zone (an IANA id, e.g. "America/New_York") chosen
/// for the account's location. The server stores and serves every timestamp in UTC; the browser is
/// the sole authority that renders those into this zone. This service only exposes the configured
/// id so the layout can hand it to the client — it performs no time conversion itself.
/// When empty, the browser falls back to each viewer's own device time zone.
/// </summary>
public class AppTimeZone : IAppTimeZone
{
    public const string SettingKey = "general.timezone";

    private readonly IServiceScopeFactory _scopeFactory;
    private volatile string? _id;
    private volatile bool _loaded;

    public AppTimeZone(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    /// <summary>
    /// The configured IANA id, or an empty string when no zone is set. Lazily read from settings on
    /// first use and cached thereafter (refreshed by <see cref="Set"/> when an admin changes it).
    /// </summary>
    public string Id
    {
        get
        {
            if (!_loaded)
            {
                using var scope = _scopeFactory.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                // One-time synchronous read at startup; cached afterwards (mirrors LoadHttpsConfig).
                _id = settings.GetAsync(SettingKey).GetAwaiter().GetResult();
                _loaded = true;
            }
            return _id ?? string.Empty;
        }
    }

    /// <summary>Updates the cached id after the setting changes, so it takes effect immediately.</summary>
    public void Set(string? id)
    {
        _id = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        _loaded = true;
    }

    /// <summary>Current wall-clock time in the configured zone (server-local when unset).</summary>
    public DateTime Now()
    {
        var id = Id;
        return !string.IsNullOrEmpty(id) && TimeZones.TryGetInfo(id, out var tz)
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)
            : DateTime.Now;
    }

    /// <summary>Today's calendar date in the configured zone (server-local when unset).</summary>
    public DateOnly Today() => DateOnly.FromDateTime(Now());
}
