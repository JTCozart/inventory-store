namespace InventoryStore.Application.Interfaces.Services;

/// <summary>
/// The single source for the account's configured display time zone. Instants are stored in UTC and
/// localized in the browser; this exists for the few <em>calendar-date</em> decisions the server must
/// make ("what day is it here?") which a browser cannot do after the fact — e.g. stamping a
/// last-maintained date or deciding whether something is overdue / expiring today.
/// </summary>
public interface IAppTimeZone
{
    /// <summary>Configured IANA id, or empty when unset (server-local is then used for date math).</summary>
    string Id { get; }

    /// <summary>Current wall-clock time in the configured zone (server-local when unset).</summary>
    DateTime Now();

    /// <summary>Today's calendar date in the configured zone (server-local when unset).</summary>
    DateOnly Today();

    /// <summary>Refreshes the cached zone after an admin changes the setting.</summary>
    void Set(string? id);
}
