using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace InventoryTracker.App.Services;

public sealed partial class TunnelService : IAsyncDisposable
{
    // ── Cloudflared paths ─────────────────────────────────────────────────
    private static readonly string CloudflaredPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InventoryTracker", "tools", "cloudflared.exe");

    private const string CloudflaredDownloadUrl =
        "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";

    // ── State ─────────────────────────────────────────────────────────────
    public enum TunnelState { Stopped, Downloading, Starting, Running, Error }
    public enum TunnelMode  { Quick, Named, LocalTunnel }

    private Process?                  _cfProcess;
    private CancellationTokenSource?  _ltCts;
    private readonly List<Task>       _ltWorkers = new();
    private readonly ILogger<TunnelService> _logger;

    public TunnelState State     { get; private set; } = TunnelState.Stopped;
    public string?     PublicUrl { get; private set; }
    public string?     Error     { get; private set; }

    public event Func<Task>? OnStateChanged;

    public TunnelService(ILogger<TunnelService> logger) => _logger = logger;

    // ── Public API ────────────────────────────────────────────────────────

    public Task StartQuickAsync()                              => StartCloudflaredAsync(null, null);
    public Task StartNamedAsync(string token, string? url)    => StartCloudflaredAsync(token, url);
    public Task StartLocalTunnelAsync(string subdomain)        => StartLocalTunnelImplAsync(subdomain);

    public Task StopAsync()
    {
        // Kill cloudflared immediately
        try { _cfProcess?.Kill(); } catch { }
        _cfProcess?.Dispose();
        _cfProcess = null;

        // Cancel localtunnel workers — TCP sockets close as soon as the token fires.
        // Don't await workers; the OS releases the loca.lt subdomain when connections drop.
        _ltCts?.Cancel();
        _ltCts?.Dispose();
        _ltCts = null;
        _ltWorkers.Clear();

        PublicUrl = null;
        Error     = null;
        State     = TunnelState.Stopped;

        // Fire-and-forget notification (we may be shutting down)
        _ = NotifyAsync();
        return Task.CompletedTask;
    }

    // ── Cloudflared (Quick + Named) ───────────────────────────────────────

    private async Task StartCloudflaredAsync(string? token, string? knownUrl)
    {
        if (State is TunnelState.Downloading or TunnelState.Starting or TunnelState.Running) return;

        Error = null; PublicUrl = null;

        try
        {
            if (!File.Exists(CloudflaredPath))
            {
                State = TunnelState.Downloading;
                _logger.LogInformation("Downloading cloudflared...");
                await NotifyAsync();
                await DownloadFileAsync(CloudflaredPath, CloudflaredDownloadUrl);
            }

            State = TunnelState.Starting;
            await NotifyAsync();

            var args = token is not null
                ? $"tunnel run --token {token} --no-autoupdate"
                : "tunnel --url http://localhost:5050 --no-autoupdate";

            var psi = new ProcessStartInfo(CloudflaredPath, args)
            {
                RedirectStandardError  = true,
                RedirectStandardOutput = true,
                UseShellExecute  = false,
                CreateNoWindow   = true
            };

            _cfProcess = Process.Start(psi)!;

            if (token is not null && !string.IsNullOrWhiteSpace(knownUrl))
            {
                await Task.Delay(3000);
                if (_cfProcess.HasExited)
                    throw new Exception("Tunnel process exited early — check your token.");
                PublicUrl = knownUrl.StartsWith("https://") ? knownUrl : $"https://{knownUrl}";
                State     = TunnelState.Running;
                _logger.LogInformation("Named tunnel active: {Url}", PublicUrl);
            }
            else
            {
                var url = await WaitForCloudflareUrlAsync(_cfProcess);
                if (url is not null)
                {
                    PublicUrl = url;
                    State     = TunnelState.Running;
                    _logger.LogInformation("Quick tunnel active: {Url}", url);
                }
                else
                {
                    State = TunnelState.Error;
                    Error = "Tunnel did not return a URL within 30 seconds.";
                    try { _cfProcess.Kill(); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            State = TunnelState.Error;
            Error = ex.Message;
            _logger.LogError(ex, "Cloudflared start failed.");
        }

        await NotifyAsync();
    }

    private static async Task<string?> WaitForCloudflareUrlAsync(Process process)
    {
        var tcs = new TaskCompletionSource<string?>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        cts.Token.Register(() => tcs.TrySetResult(null));

        _ = Task.Run(async () =>
        {
            try
            {
                while (!process.StandardError.EndOfStream)
                {
                    var line = await process.StandardError.ReadLineAsync();
                    if (line is null) break;
                    var m = TrycloudflareUrlRegex().Match(line);
                    if (m.Success) { tcs.TrySetResult(m.Value); return; }
                }
                tcs.TrySetResult(null);
            }
            catch { tcs.TrySetResult(null); }
        });

        return await tcs.Task;
    }

    // ── localtunnel (native C# TCP proxy) ────────────────────────────────

    private async Task StartLocalTunnelImplAsync(string subdomain)
    {
        if (State is TunnelState.Downloading or TunnelState.Starting or TunnelState.Running) return;

        Error = null; PublicUrl = null;

        try
        {
            State = TunnelState.Starting;
            await NotifyAsync();

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryTracker/1.0");

            var info = await http.GetFromJsonAsync<LocalTunnelInfo>(
                $"https://localtunnel.me/{Uri.EscapeDataString(subdomain)}");

            if (info is null || info.Port == 0)
                throw new Exception("localtunnel.me returned an unexpected response.");

            PublicUrl = info.Url;
            State     = TunnelState.Running;
            _logger.LogInformation("localtunnel active: {Url} (server port {Port})", info.Url, info.Port);
            await NotifyAsync();

            // Start worker pool — each worker maintains one tunnel ↔ localhost pipe
            _ltCts = new CancellationTokenSource();
            var ct         = _ltCts.Token;
            var workerCount = Math.Min(info.MaxConnCount > 0 ? info.MaxConnCount : 10, 10);

            _ltWorkers.Clear();
            for (int i = 0; i < workerCount; i++)
                _ltWorkers.Add(Task.Run(() => LocalTunnelWorkerLoopAsync("localtunnel.me", info.Port, ct)));
        }
        catch (Exception ex)
        {
            State = TunnelState.Error;
            Error = ex.Message;
            _logger.LogError(ex, "localtunnel start failed.");
            await NotifyAsync();
        }
    }

    private async Task LocalTunnelWorkerLoopAsync(string host, int port, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProxyOneRequestAsync(host, port, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "localtunnel worker error — retrying");
            }

            if (!ct.IsCancellationRequested)
                await Task.Delay(250, ct).ConfigureAwait(false);
        }
    }

    private static async Task ProxyOneRequestAsync(string tunnelHost, int tunnelPort, CancellationToken ct)
    {
        using var tunnelTcp = new TcpClient();
        await tunnelTcp.ConnectAsync(tunnelHost, tunnelPort, ct);

        var tunnelStream = tunnelTcp.GetStream();

        // Wait for first byte — the tunnel server sends this once it has paired an
        // incoming request to our connection. Only then open the local connection.
        var firstByte = new byte[1];
        if (await tunnelStream.ReadAsync(firstByte.AsMemory(), ct) == 0) return;

        using var localTcp = new TcpClient();
        await localTcp.ConnectAsync("127.0.0.1", 5050, ct);
        var localStream = localTcp.GetStream();

        // Forward the peeked first byte to the local server
        await localStream.WriteAsync(firstByte.AsMemory(0, 1), ct);
        await localStream.FlushAsync(ct);

        // Bidirectional pipe until either side closes
        using var pair = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await Task.WhenAny(
            PipeAsync(tunnelStream, localStream, pair.Token),
            PipeAsync(localStream, tunnelStream, pair.Token)
        );
        pair.Cancel();
    }

    private static async Task PipeAsync(Stream src, Stream dst, CancellationToken ct)
    {
        var buf = new byte[65536];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = await src.ReadAsync(buf.AsMemory(), ct);
                if (n == 0) break;
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                await dst.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    private static async Task DownloadFileAsync(string destPath, string url)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryTracker/1.0");
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(destPath, bytes);
    }

    private Task NotifyAsync()
    {
        var h = OnStateChanged;
        if (h is null) return Task.CompletedTask;
        return Task.WhenAll(h.GetInvocationList().Cast<Func<Task>>().Select(f => f()));
    }

    public ValueTask DisposeAsync() { StopAsync(); return ValueTask.CompletedTask; }

    // ── Regex / DTOs ──────────────────────────────────────────────────────

    [GeneratedRegex(@"https://[a-z0-9-]+\.trycloudflare\.com")]
    private static partial Regex TrycloudflareUrlRegex();

    private sealed class LocalTunnelInfo
    {
        [JsonPropertyName("id")]            public string Id           { get; set; } = "";
        [JsonPropertyName("port")]          public int    Port         { get; set; }
        [JsonPropertyName("url")]           public string Url          { get; set; } = "";
        [JsonPropertyName("max_conn_count")]public int    MaxConnCount { get; set; }
    }
}
