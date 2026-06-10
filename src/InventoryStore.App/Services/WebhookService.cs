using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InventoryStore.App.Modules;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.App.Services;

// Delivers inventory events to configured webhook endpoints. Mirrors NtfyService: config is read
// up front (awaited), then HTTP sends are fire-and-forget so callers are never blocked or thrown into.
public class WebhookService : IWebhookService
{
    private readonly IModuleRegistry _modules;
    private readonly IWebhookEndpointRepository _repo;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(IModuleRegistry modules, IWebhookEndpointRepository repo,
        IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory, ILogger<WebhookService> logger)
    {
        _modules      = modules;
        _repo         = repo;
        _scopeFactory = scopeFactory;
        _httpFactory  = httpFactory;
        _logger       = logger;
    }

    public async Task DispatchAsync(string eventName, object payload)
    {
        try
        {
            if (!await _modules.IsEnabledAsync("webhooks")) return;

            var targets = (await _repo.GetEnabledAsync())
                .Where(e => e.Subscribes(eventName))
                .ToList();
            if (targets.Count == 0) return;

            var body = JsonSerializer.Serialize(new
            {
                @event    = eventName,
                timestamp = DateTime.UtcNow,
                data      = payload
            });

            foreach (var ep in targets)
                FireOne(ep.Id, ep.Url, ep.Secret, eventName, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "webhook dispatch failed");
        }
    }

    private void FireOne(int id, string url, string? secret, string eventName, string body)
    {
        _ = Task.Run(async () =>
        {
            string status;
            try
            {
                using var http = _httpFactory.CreateClient("webhook");
                using var req  = BuildRequest(url, secret, eventName, body);
                var res = await http.SendAsync(req);
                status = ((int)res.StatusCode).ToString();
            }
            catch (Exception ex)
            {
                status = "error";
                _logger.LogWarning(ex, "webhook send to {Url} failed", url);
            }
            await RecordStatusAsync(id, status);
        });
    }

    private async Task RecordStatusAsync(int id, string status)
    {
        // The request scope is gone by now, so persist via a fresh scope.
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
            var ep   = await repo.GetByIdAsync(id);
            if (ep is not null)
            {
                ep.LastStatus = status;
                ep.LastSentAt = DateTime.UtcNow;
                await repo.UpdateAsync(ep);
            }
        }
        catch { /* best-effort */ }
    }

    public async Task<(bool ok, int status, string? error)> SendTestAsync(int endpointId)
    {
        var ep = await _repo.GetByIdAsync(endpointId);
        if (ep is null) return (false, 0, "Endpoint not found.");

        var body = JsonSerializer.Serialize(new
        {
            @event    = "test",
            timestamp = DateTime.UtcNow,
            data      = new { message = "Test delivery from Inventory Store." }
        });

        try
        {
            using var http = _httpFactory.CreateClient("webhook");
            using var req  = BuildRequest(ep.Url, ep.Secret, "test", body);
            var res = await http.SendAsync(req);
            var code = (int)res.StatusCode;
            ep.LastStatus = code.ToString();
            ep.LastSentAt = DateTime.UtcNow;
            await _repo.UpdateAsync(ep);
            return (code < 300, code, null);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    private static HttpRequestMessage BuildRequest(string url, string? secret, string eventName, string body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-InventoryStore-Event", eventName);
        if (!string.IsNullOrWhiteSpace(secret))
            req.Headers.Add("X-InventoryStore-Signature", "sha256=" + Sign(body, secret));
        return req;
    }

    private static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
