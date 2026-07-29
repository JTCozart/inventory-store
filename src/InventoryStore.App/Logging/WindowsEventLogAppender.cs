#if !LINUX
using System.Diagnostics;
using log4net.Appender;
using log4net.Core;

namespace InventoryStore.App.Logging;

// log4net's own EventLogAppender isn't available on the netstandard build it ships for
// .NET Core/.NET 8+ (Windows Event Log access needs the System.Diagnostics.EventLog package,
// which log4net's core package doesn't reference), so this writes directly instead. The
// "InventoryStore" event source must already exist -- install-service.ps1 and the Inno Setup
// installer both register it; if it's missing (e.g. running unelevated in dev), writes are
// silently skipped rather than throwing.
public sealed class WindowsEventLogAppender : AppenderSkeleton
{
    public string LogName { get; set; } = "Application";
    public string ApplicationName { get; set; } = "InventoryStore";

    protected override void Append(LoggingEvent loggingEvent)
    {
        if (!OperatingSystem.IsWindows() || !EventLog.SourceExists(ApplicationName))
            return;

        try
        {
            var entryType = loggingEvent.Level >= Level.Error ? EventLogEntryType.Error
                : loggingEvent.Level >= Level.Warn ? EventLogEntryType.Warning
                : EventLogEntryType.Information;

            EventLog.WriteEntry(ApplicationName, RenderLoggingEvent(loggingEvent), entryType);
        }
        catch
        {
            // Never let a logging failure take down the request/service it's logging about.
        }
    }
}
#endif
