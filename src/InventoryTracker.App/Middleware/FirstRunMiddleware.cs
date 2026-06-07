using InventoryTracker.Application.Interfaces.Services;

namespace InventoryTracker.App.Middleware;

public class FirstRunMiddleware
{
    private readonly RequestDelegate _next;

    public FirstRunMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserAuthService authService)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isSetupPath = path.StartsWith("/Auth/Setup", StringComparison.OrdinalIgnoreCase);
        var isStaticFile = path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);

        if (!isSetupPath && !isStaticFile && await authService.IsSetupRequiredAsync())
        {
            context.Response.Redirect("/Auth/Setup");
            return;
        }

        await _next(context);
    }
}
