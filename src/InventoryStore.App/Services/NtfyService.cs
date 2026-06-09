using InventoryStore.Application.Interfaces.Services;

namespace InventoryStore.App.Services;

public class NtfyService : INtfyService
{
    private readonly ISettingsService _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<NtfyService> _logger;

    private bool _loaded;
    private string _server   = "https://ntfy.sh";
    private string? _topic;
    private string? _token;
    private bool _onCheckout;
    private bool _onCheckin;
    private bool _onLost;
    private bool _onLowStock;
    private bool _onLogin;

    public NtfyService(ISettingsService settings, IHttpClientFactory httpFactory, ILogger<NtfyService> logger)
    {
        _settings    = settings;
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_topic);

    private async Task LoadAsync()
    {
        if (_loaded) return;
        _server     = (await _settings.GetAsync("ntfy.server"))?.TrimEnd('/') ?? "https://ntfy.sh";
        _topic      = await _settings.GetAsync("ntfy.topic");
        _token      = await _settings.GetAsync("ntfy.token");
        _onCheckout = await _settings.GetAsync("ntfy.notify.checkout") == "true";
        _onCheckin  = await _settings.GetAsync("ntfy.notify.checkin")  == "true";
        _onLost     = await _settings.GetAsync("ntfy.notify.lost")     == "true";
        _onLowStock = await _settings.GetAsync("ntfy.notify.lowstock") == "true";
        _onLogin    = await _settings.GetAsync("ntfy.notify.login")    == "true";
        _loaded = true;
    }

    private void FireAndForget(string title, string body, string tags, string priority)
    {
        var server = _server;
        var topic  = _topic!;
        var token  = _token;
        _ = Task.Run(async () =>
        {
            try
            {
                using var http = _httpFactory.CreateClient("ntfy");
                using var req  = new HttpRequestMessage(HttpMethod.Post, $"{server}/{topic}");
                req.Content    = new StringContent(body);
                req.Headers.Add("Title",    title);
                req.Headers.Add("Priority", priority);
                req.Headers.Add("Tags",     tags);
                if (!string.IsNullOrWhiteSpace(token))
                    req.Headers.Add("Authorization", $"Bearer {token}");
                await http.SendAsync(req);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ntfy send failed");
            }
        });
    }

    public async Task NotifyCheckoutAsync(string itemName, string checkedOutBy, string? clientName)
    {
        await LoadAsync();
        if (!IsConfigured || !_onCheckout) return;
        var body = clientName is not null
            ? $"{checkedOutBy} ({clientName}) checked out {itemName}"
            : $"{checkedOutBy} checked out {itemName}";
        FireAndForget("Item Checked Out", body, "arrow_up", "default");
    }

    public async Task NotifyCheckinAsync(string itemName, string checkedInBy)
    {
        await LoadAsync();
        if (!IsConfigured || !_onCheckin) return;
        FireAndForget("Item Returned", $"{checkedInBy} returned {itemName}", "white_check_mark", "low");
    }

    public async Task NotifyLostAsync(string itemName, string checkedOutBy)
    {
        await LoadAsync();
        if (!IsConfigured || !_onLost) return;
        FireAndForget("Item Marked Lost", $"{itemName} (last with {checkedOutBy}) marked as lost", "x", "high");
    }

    public async Task NotifyLowStockAsync(string itemName, int available, int minimum)
    {
        await LoadAsync();
        if (!IsConfigured || !_onLowStock) return;
        FireAndForget("Low Stock Alert", $"{itemName} is low: {available} available (min {minimum})", "warning", "high");
    }

    public async Task NotifyLoginAsync(string username)
    {
        await LoadAsync();
        if (!IsConfigured || !_onLogin) return;
        FireAndForget("User Login", $"{username} logged in", "key", "low");
    }

    public async Task<(bool ok, int statusCode)> SendTestAsync()
    {
        await LoadAsync();
        if (!IsConfigured) return (false, 0);
        try
        {
            using var http = _httpFactory.CreateClient("ntfy");
            using var req  = new HttpRequestMessage(HttpMethod.Post, $"{_server}/{_topic}");
            req.Content    = new StringContent("This is a test notification from Inventory Store.");
            req.Headers.Add("Title",    "Test Notification");
            req.Headers.Add("Priority", "default");
            req.Headers.Add("Tags",     "bell");
            if (!string.IsNullOrWhiteSpace(_token))
                req.Headers.Add("Authorization", $"Bearer {_token}");
            var res = await http.SendAsync(req);
            return ((int)res.StatusCode < 300, (int)res.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ntfy test failed");
            return (false, 0);
        }
    }
}
