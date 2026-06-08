using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace InventoryTracker.Tray;

public class TrayApplicationContext : ApplicationContext
{
    private const string BaseUrl  = "http://localhost:5050";
    private const int    PollMs   = 10_000;

    private readonly HttpClient          _http;
    private readonly NotifyIcon          _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem   _openItem;
    private readonly ToolStripMenuItem   _networkItem;
    private readonly ToolStripMenuItem   _tunnelItem;
    private readonly ToolStripMenuItem   _resetItem;

    private bool   _serviceUp  = false;
    private string _tunnelUrl  = string.Empty;

    public TrayApplicationContext()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

        _openItem    = new ToolStripMenuItem("Open Inventory Tracker",    null, (_, _) => OpenBrowser());
        _networkItem = new ToolStripMenuItem($"Local: {BaseUrl}")         { Enabled = false };
        _tunnelItem  = new ToolStripMenuItem("Tunnel: checking…")         { Enabled = false };
        _resetItem   = new ToolStripMenuItem("Reset Admin Password",      null, (_, _) => ResetAdminPassword());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_networkItem);
        menu.Items.Add(_tunnelItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_resetItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new NotifyIcon
        {
            Icon             = BuildIcon(running: false),
            Text             = "Inventory Tracker — connecting…",
            Visible          = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => OpenBrowser();

        _timer = new System.Windows.Forms.Timer { Interval = PollMs };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();

        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        try
        {
            var doc = await _http.GetFromJsonAsync<JsonDocument>($"{BaseUrl}/api/local/status");
            if (doc is null) throw new Exception();

            var root       = doc.RootElement;
            var tunnelState = root.TryGetProperty("tunnelState", out var ts) ? ts.GetString() : null;
            var tunnelUrl   = root.TryGetProperty("tunnelUrl",   out var tu) ? tu.GetString() : null;
            var networkUrl  = root.TryGetProperty("networkUrl",  out var nu) ? nu.GetString() : null;

            SetServiceUp(networkUrl, tunnelState, tunnelUrl);
        }
        catch
        {
            SetServiceDown();
        }
    }

    private void SetServiceUp(string? networkUrl, string? tunnelState, string? tunnelUrl)
    {
        _serviceUp = true;
        _tray.Icon = BuildIcon(running: true);

        var netLabel = networkUrl ?? BaseUrl;
        _networkItem.Text = $"Local: {netLabel}";

        if (tunnelState == "Running" && !string.IsNullOrEmpty(tunnelUrl))
        {
            _tunnelUrl = tunnelUrl;
            _tunnelItem.Text    = $"Tunnel: {tunnelUrl}";
            _tunnelItem.Enabled = true;
            _tunnelItem.Click  -= CopyTunnelUrl;
            _tunnelItem.Click  += CopyTunnelUrl;
            _tray.Text = "Inventory Tracker — tunnel active";
        }
        else
        {
            _tunnelUrl = string.Empty;
            _tunnelItem.Text    = tunnelState switch
            {
                "Starting"    => "Tunnel: starting…",
                "Downloading" => "Tunnel: downloading…",
                "Error"       => "Tunnel: error",
                _             => "Tunnel: not running",
            };
            _tunnelItem.Enabled = false;
            _tunnelItem.Click  -= CopyTunnelUrl;
            _tray.Text = "Inventory Tracker — running";
        }

        _resetItem.Enabled = true;
        _openItem.Enabled  = true;
    }

    private void SetServiceDown()
    {
        _serviceUp = false;
        _tray.Icon          = BuildIcon(running: false);
        _tray.Text          = "Inventory Tracker — service not running";
        _networkItem.Text   = "Service not running";
        _tunnelItem.Text    = "Tunnel: unavailable";
        _tunnelItem.Enabled = false;
        _resetItem.Enabled  = false;
        _openItem.Enabled   = false;
    }

    private void CopyTunnelUrl(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_tunnelUrl))
            Clipboard.SetText(_tunnelUrl);
    }

    private static void OpenBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BaseUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open browser:\n{ex.Message}",
                "Inventory Tracker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void ResetAdminPassword()
    {
        using var form = new ResetPasswordForm();
        if (form.ShowDialog() != DialogResult.OK) return;

        try
        {
            var payload = JsonSerializer.Serialize(new { newPassword = form.NewPassword });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp    = await _http.PostAsync($"{BaseUrl}/api/local/reset-admin", content);

            if (resp.IsSuccessStatusCode)
            {
                var body     = await resp.Content.ReadFromJsonAsync<JsonElement>();
                var username = body.TryGetProperty("username", out var u) ? u.GetString() : "admin";
                MessageBox.Show($"Password for '{username}' has been reset successfully.",
                    "Inventory Tracker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                MessageBox.Show($"Reset failed: {body}",
                    "Inventory Tracker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not reach service:\n{ex.Message}",
                "Inventory Tracker", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExitApp()
    {
        _timer.Stop();
        _tray.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _http.Dispose();
        }
        base.Dispose(disposing);
    }

    // Draws the "IT" icon at runtime — works without an embedded .ico file.
    // In release builds the .ico is also embedded as the exe icon (for Explorer/taskbar).
    private static Icon BuildIcon(bool running)
    {
        const int S = 32;
        using var bmp = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var bg = running
            ? Color.FromArgb(37, 99, 235)   // blue-600
            : Color.FromArgb(107, 114, 128); // gray-500

        // Rounded-rect background
        int r = 6;
        using var path = new GraphicsPath();
        path.AddArc(0,     0,     r * 2, r * 2, 180, 90);
        path.AddArc(S - r * 2, 0, r * 2, r * 2, 270, 90);
        path.AddArc(S - r * 2, S - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(0, S - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(bg);
        g.FillPath(brush, path);

        // "IT" text
        using var font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("IT", font, Brushes.White, new RectangleF(0, 0, S, S), sf);

        return Icon.FromHandle(bmp.GetHicon());
    }
}
