using InventoryTracker.Application.Interfaces.Services;
using InventoryTracker.Application.Services;
using InventoryTracker.Domain.Interfaces.Repositories;
using InventoryTracker.Infrastructure.Data;
using InventoryTracker.Infrastructure.Repositories;
using InventoryTracker.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryTracker.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IAppSettingRepository, AppSettingRepository>();
        services.AddScoped<ICheckoutRepository, CheckoutRepository>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IUserAuthService, AuthenticationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ICheckoutService, CheckoutService>();

        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
