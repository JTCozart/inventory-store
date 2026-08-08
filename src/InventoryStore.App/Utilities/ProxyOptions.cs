using Microsoft.Extensions.Configuration;

namespace InventoryStore.App.Utilities;

// Reverse-proxy / SSL-offload deployment options.
//
// Inventory Store's default deployment binds 5050 for LAN HTTP and, when HTTPS is
// configured in Settings, also 80 (ACME challenge + redirect) and 443. That model
// assumes the app owns the machine's web ports. Behind a reverse proxy (HostRouter,
// nginx, Traefik, a cloud load balancer) it does not: the proxy terminates TLS and
// forwards plain HTTP to a single app port, so binding 80/443 either fails or fights
// the proxy for them.
//
// Setting a port override switches the app into proxy mode:
//   * Kestrel binds ONLY the override port, as plain HTTP. No 80, no 443, no Kestrel
//     TLS, and Let's Encrypt / manual-certificate handling is skipped entirely --
//     even if HTTPS is still switched on under Settings. The proxy owns TLS.
//   * X-Forwarded-For / -Proto / -Host are honoured, so Request.IsHttps, the client
//     IP and generated links reflect the public request rather than the proxy hop.
//   * RedirectToTls (opt-in) sends plain-HTTP requests to https://, using the
//     forwarded scheme -- never the local port, which is always HTTP behind a proxy.
//
// Configuration comes from IConfiguration, so it can be set either way:
//
//   Windows  -- appsettings.json next to the executable:
//       "Proxy": { "Port": 8080, "RedirectToTls": true }
//
//   Linux    -- Environment= lines in the systemd unit or a drop-in
//               (/etc/systemd/system/inventorystore.service.d/hosted.conf):
//       Environment=Proxy__Port=8080
//       Environment=Proxy__RedirectToTls=true
//
//   The INVENTORYSTORE_PROXY_PORT / _REDIRECT_TO_TLS / _ENABLED / _TRUSTED_PROXIES
//   environment variables are accepted as aliases, for anyone who finds ASP.NET
//   Core's "__" section separator surprising in a unit file.
internal sealed record ProxyOptions(
    bool Enabled,
    int? Port,
    bool RedirectToTls,
    IReadOnlyList<string> TrustedProxies)
{
    private static readonly Lazy<ProxyOptions> _current = new(() => Load(BuildConfiguration()));

    // Resolved once per process, from the same sources the host itself reads. Startup,
    // the tray companion and TunnelService all go through this so they agree on the port.
    internal static ProxyOptions Current => _current.Value;

    // The port the web UI actually listens on: the override when proxy mode is on,
    // otherwise the historical 5050 (5051 in development, so a dev run does not
    // collide with an installed production service).
    internal static int EffectiveHttpPort =>
        Current.Port ?? (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? 5051 : 5050);

    private static ProxyOptions Load(IConfiguration config)
    {
        var section = config.GetSection("Proxy");

        var port = FirstInt(
            Environment.GetEnvironmentVariable("INVENTORYSTORE_PROXY_PORT"),
            section["Port"]);

        var redirect = FirstBool(
            Environment.GetEnvironmentVariable("INVENTORYSTORE_PROXY_REDIRECT_TO_TLS"),
            section["RedirectToTls"]) ?? false;

        // An explicit Enabled flag turns on forwarded-header handling without changing
        // the port (proxy listening on the default 5050). Setting a port implies it.
        var enabled = (FirstBool(
            Environment.GetEnvironmentVariable("INVENTORYSTORE_PROXY_ENABLED"),
            section["Enabled"]) ?? false) || port is > 0;

        var trusted = (Environment.GetEnvironmentVariable("INVENTORYSTORE_PROXY_TRUSTED_PROXIES")
                       ?? section["TrustedProxies"] ?? string.Empty)
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new ProxyOptions(enabled, port is > 0 ? port : null, redirect, trusted);
    }

    // Mirrors the generic host's own configuration sources. Built separately rather than
    // taken from the host's IConfiguration because these values are needed by call sites
    // that run outside (and before) the request pipeline.
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

    private static int? FirstInt(params string?[] values)
    {
        foreach (var v in values)
            if (int.TryParse(v, out var parsed)) return parsed;
        return null;
    }

    private static bool? FirstBool(params string?[] values)
    {
        foreach (var v in values)
            if (bool.TryParse(v, out var parsed)) return parsed;
        return null;
    }
}
