using InventoryStore.Application.Interfaces.Services;

namespace InventoryStore.App.Services;

// Reads deployment flags from environment variables once at startup. Registered as a singleton.
// PROFESSIONAL_SERVICES_HOSTED=true marks this as a provider-managed hosted instance, which locks
// the first admin account and presents it as a SYSTEM account.
public class HostingMode : IHostingMode
{
    public bool IsProfessionalServicesHosted { get; } =
        string.Equals(
            Environment.GetEnvironmentVariable("PROFESSIONAL_SERVICES_HOSTED"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
