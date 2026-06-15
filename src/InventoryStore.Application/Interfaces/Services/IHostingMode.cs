namespace InventoryStore.Application.Interfaces.Services;

// Exposes deployment-time flags that the hosting provider sets via environment variables.
// In professional-services hosted mode the provider's first admin account is locked and shown
// as a SYSTEM account so the client cannot delete, modify, or suspend it.
public interface IHostingMode
{
    bool IsProfessionalServicesHosted { get; }
}
