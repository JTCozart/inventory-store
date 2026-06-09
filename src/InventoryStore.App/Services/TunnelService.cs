using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace InventoryStore.App.Services;

public sealed partial class TunnelService : IAsyncDisposable
{
    // ── Cloudflared paths ─────────────────────────────────────────────────
    private static readonly int AppPort =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? 5051 : 5050;

    private static readonly string CloudflaredPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InventoryStore", "tools", "cloudflared.exe");

    private const string CloudflaredDownloadUrl =
        "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";

    // ── Serveo / SSH paths ────────────────────────────────────────────────
    private static readonly string ServeoKeyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InventoryStore", "serveo_ed25519");

    // ── State ─────────────────────────────────────────────────────────────
    public enum TunnelState { Stopped, Downloading, Starting, Running, Error }
    public enum TunnelMode  { Quick, Named, LocalTunnel, Serveo }

    private Process?                  _cfProcess;
    private Process?                  _sshProcess;
    private CancellationTokenSource?  _ltCts;
    private readonly List<Task>       _ltWorkers = new();
    private readonly ILogger<TunnelService> _logger;

    public TunnelState State     { get; private set; } = TunnelState.Stopped;
    public string?     PublicUrl { get; private set; }
    public string?     Error     { get; private set; }

    public event Func<Task>? OnStateChanged;

    private readonly IServiceScopeFactory _scopeFactory;

    public TunnelService(ILogger<TunnelService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger       = logger;
        _scopeFactory = scopeFactory;
    }

    // ── Public API ────────────────────────────────────────────────────────

    public Task StartQuickAsync()                              => StartCloudflaredAsync(null, null);
    public Task StartNamedAsync(string token, string? url)    => StartCloudflaredAsync(token, url);
    public Task StartLocalTunnelAsync(string subdomain)        => StartLocalTunnelImplAsync(subdomain);
    public Task StartServeoAsync(string subdomain)             => StartServeoImplAsync(subdomain);

    public Task StopAsync()
    {
        // Kill cloudflared
        try { _cfProcess?.Kill(); } catch { }
        _cfProcess?.Dispose();
        _cfProcess = null;

        // Kill SSH (Serveo)
        try { _sshProcess?.Kill(); } catch { }
        _sshProcess?.Dispose();
        _sshProcess = null;

        // Cancel localtunnel workers
        _ltCts?.Cancel();
        _ltCts?.Dispose();
        _ltCts = null;
        _ltWorkers.Clear();

        PublicUrl = null;
        Error     = null;
        State     = TunnelState.Stopped;

        _ = NotifyAsync();
        return Task.CompletedTask;
    }

    // ── Serveo SSH key management ─────────────────────────────────────────

    public async Task<string> EnsureServeoKeyAsync()
    {
        using var scope    = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<Application.Interfaces.Services.ISettingsService>();

        var existing = await settings.GetAsync("serveo.ssh.public");
        if (!string.IsNullOrWhiteSpace(existing)) return existing;

        // Generate key pair using Windows built-in ssh-keygen
        var keygenExe = FindOpenSshExe("ssh-keygen.exe");
        var tmpKey    = Path.Combine(Path.GetTempPath(), $"serveo_tmp_{Guid.NewGuid():N}");
        try
        {
            var psi = new ProcessStartInfo(keygenExe,
                $"-t rsa -b 4096 -f \"{tmpKey}\" -N \"\" -q -C \"InventoryStore\"")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
                throw new Exception("ssh-keygen failed — check that OpenSSH Client is installed.");

            var privateKey = await File.ReadAllTextAsync(tmpKey);
            var publicKey  = (await File.ReadAllTextAsync(tmpKey + ".pub")).Trim();

            await settings.SetAsync("serveo.ssh.private", privateKey);
            await settings.SetAsync("serveo.ssh.public",  publicKey);
            _logger.LogInformation("Serveo SSH key generated and stored.");
            return publicKey;
        }
        finally
        {
            try { File.Delete(tmpKey); }       catch { }
            try { File.Delete(tmpKey + ".pub"); } catch { }
        }
    }

    public async Task<string> RegenerateServeoKeyAsync()
    {
        using var scope  = _scopeFactory.CreateScope();
        var settings     = scope.ServiceProvider.GetRequiredService<Application.Interfaces.Services.ISettingsService>();
        await settings.SetAsync("serveo.ssh.private", null);
        await settings.SetAsync("serveo.ssh.public",  null);
        return await EnsureServeoKeyAsync();
    }

    public async Task<string?> GetServeoPublicKeyAsync()
    {
        using var scope    = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<Application.Interfaces.Services.ISettingsService>();
        return await settings.GetAsync("serveo.ssh.public");
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
                : $"tunnel --url http://localhost:{AppPort} --no-autoupdate";

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

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryStore/1.0");

            LocalTunnelInfo? info = null;
            int[] retryDelaysMs = [2000, 4000, 8000, 15000];
            Exception? lastEx = null;

            for (int attempt = 0; attempt <= retryDelaysMs.Length; attempt++)
            {
                try
                {
                    info = await http.GetFromJsonAsync<LocalTunnelInfo>(
                        $"https://localtunnel.me/{Uri.EscapeDataString(subdomain)}");
                    lastEx = null;
                    break;
                }
                catch (Exception ex) when (ex is HttpRequestException or SocketException
                    || ex.InnerException is SocketException)
                {
                    lastEx = ex;
                    if (attempt < retryDelaysMs.Length)
                    {
                        _logger.LogWarning(ex,
                            "localtunnel DNS/network not ready (attempt {Attempt}/{Total}), retrying in {Delay}ms...",
                            attempt + 1, retryDelaysMs.Length + 1, retryDelaysMs[attempt]);
                        await Task.Delay(retryDelaysMs[attempt]);
                    }
                }
            }

            if (lastEx is not null) throw lastEx;

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
        await localTcp.ConnectAsync("127.0.0.1", AppPort, ct);
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

    // ── Serveo (SSH) ──────────────────────────────────────────────────────

    private async Task StartServeoImplAsync(string subdomain)
    {
        if (State is TunnelState.Downloading or TunnelState.Starting or TunnelState.Running) return;

        Error = null; PublicUrl = null;

        try
        {
            State = TunnelState.Starting;
            await NotifyAsync();

            using var scope = _scopeFactory.CreateScope();
            var settings    = scope.ServiceProvider.GetRequiredService<Application.Interfaces.Services.ISettingsService>();
            var privateKey  = await settings.GetAsync("serveo.ssh.private");
            if (string.IsNullOrWhiteSpace(privateKey))
                throw new Exception("SSH key not found. Complete the Serveo setup in Settings first.");

            var keyFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "InventoryStore", "serveo_key");
            Directory.CreateDirectory(Path.GetDirectoryName(keyFile)!);
            await File.WriteAllTextAsync(keyFile, privateKey);

            await FixKeyFilePermissionsAsync(keyFile);

            string sshExe;
            try   { sshExe = FindOpenSshExe("ssh.exe"); }
            catch (Exception ex)
            {
                throw new Exception($"ssh.exe not found: {ex.Message} — " +
                    "Enable OpenSSH Client via Settings > Apps > Optional Features.");
            }

            _logger.LogWarning("Serveo: ssh.exe = {Exe}", sshExe);
            _logger.LogWarning("Serveo: key file = {File}", keyFile);

            var args = string.Join(" ",
                $"-i \"{keyFile}\"",
                "-F none",
                $"-R {subdomain}:80:localhost:{AppPort}",
                "serveo.net",
                "-N",
                "-v",
                "-o StrictHostKeyChecking=accept-new",
                "-o ServerAliveInterval=30",
                "-o ServerAliveCountMax=3",
                "-o ExitOnForwardFailure=yes",
                "-o PasswordAuthentication=no");

            _logger.LogWarning("Serveo: args = {Args}", args.Replace(keyFile, "<keyfile>"));

            var psi = new ProcessStartInfo(sshExe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            _sshProcess = Process.Start(psi)!;
            _logger.LogWarning("Serveo: process started PID={Pid}", _sshProcess.Id);

            var (url, output) = await WaitForServeoReadyAsync(_sshProcess, subdomain);

            var exitedEarly = _sshProcess.HasExited;
            if (exitedEarly)
                _logger.LogWarning("Serveo: process exited with code {Code}", _sshProcess.ExitCode);

            if (url is not null)
            {
                PublicUrl = url;
                State     = TunnelState.Running;
                _logger.LogWarning("Serveo: tunnel active at {Url}", url);
            }
            else
            {
                State = TunnelState.Error;
                // Surface the raw SSH output directly in the UI error so the user can see it
                var detail = string.IsNullOrWhiteSpace(output)
                    ? (exitedEarly ? $"Process exited (code {_sshProcess.ExitCode}) with no output." : "No output received within 45s.")
                    : output;
                Error = $"Serveo tunnel failed.\n{detail}";
                _logger.LogError("Serveo failed. Detail: {Detail}", detail);
                try { _sshProcess?.Kill(); } catch { }
            }
        }
        catch (Exception ex)
        {
            State = TunnelState.Error;
            Error = ex.Message;
            _logger.LogError(ex, "Serveo start failed.");
        }

        await NotifyAsync();
    }

    private async Task<(string? Url, string? AllOutput)> WaitForServeoReadyAsync(Process process, string subdomain)
    {
        var tcs        = new TaskCompletionSource<string?>();
        var outputLog  = new System.Text.StringBuilder();
        using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        cts.Token.Register(() =>
        {
            _logger.LogWarning("Serveo: timed out after 45s. Output so far:\n{Output}", outputLog);
            tcs.TrySetResult(null);
        });

        var expectedUrl = $"https://{subdomain}.serveousercontent.com";

        _ = Task.Run(async () =>
        {
            try
            {
                async Task ReadStream(StreamReader reader, string label)
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                    {
                        _logger.LogWarning("Serveo [{Label}]: {Line}", label, line);
                        lock (outputLog) outputLog.AppendLine($"[{label}] {line}");

                        if (line.Contains("Forwarding HTTP") || line.Contains(".serveousercontent.com")
                            || line.Contains("remote forward success")
                            || line.Contains("all expected forwarding replies received"))
                            tcs.TrySetResult(expectedUrl);
                        else if (line.Contains("Permission denied"))
                            tcs.TrySetResult($"__ERR__SSH auth failed (Permission denied). Make sure your public key is added to your Serveo account at console.serveo.net.");
                        else if (line.Contains("already in use") || line.Contains("Address already in use"))
                            tcs.TrySetResult($"__ERR__Subdomain '{subdomain}' is already in use. Choose a different name.");
                        else if (line.Contains("Could not request") || line.Contains("remote port forwarding failed"))
                            tcs.TrySetResult($"__ERR__Port forwarding rejected: {line.Trim()}");
                        else if (line.Contains("Connection refused") || line.Contains("Connection timed out")
                              || line.Contains("No route to host"))
                            tcs.TrySetResult($"__ERR__Cannot reach serveo.net: {line.Trim()}. Check port 22 is not blocked.");
                    }
                }

                await Task.WhenAll(
                    ReadStream(process.StandardOutput, "out"),
                    ReadStream(process.StandardError,  "err"));

                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Serveo: stream read exception.");
                tcs.TrySetResult(null);
            }
        });

        var result = await tcs.Task;

        if (result is not null && result.StartsWith("__ERR__"))
            return (null, result[7..]);

        return (result, outputLog.ToString());
    }

    private async Task FixKeyFilePermissionsAsync(string keyFile)
    {
        // OpenSSH on Windows requires the private key to be accessible only by the owner.
        // Use icacls to remove inherited permissions and grant full control to current user only.
        try
        {
            var icacls = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "icacls.exe");
            if (!File.Exists(icacls))
            {
                _logger.LogWarning("Serveo: icacls.exe not found, skipping key permission fix.");
                return;
            }

            var username = $"{Environment.UserDomainName}\\{Environment.UserName}";

            // Remove inherited ACEs, then grant current user full control only
            foreach (var args in new[]
            {
                $"\"{keyFile}\" /inheritance:r",
                $"\"{keyFile}\" /grant:r \"{username}:F\""
            })
            {
                var psi = new ProcessStartInfo(icacls, args)
                {
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                };
                using var proc = Process.Start(psi)!;
                await proc.WaitForExitAsync();
                _logger.LogInformation("Serveo: icacls {Args} => exit {Code}", args, proc.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Serveo: failed to set key file permissions (SSH may reject the key).");
        }
    }

    private static string FindOpenSshExe(string exeName)
    {
        // Windows built-in OpenSSH (Windows 10 1809+)
        var win = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "OpenSSH", exeName);
        if (File.Exists(win)) return win;

        // Fall back to PATH
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var p = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(p)) return p;
        }

        throw new InvalidOperationException(
            $"{exeName} not found. Enable OpenSSH Client via Settings > Apps > Optional Features.");
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    private static async Task DownloadFileAsync(string destPath, string url)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryStore/1.0");
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
