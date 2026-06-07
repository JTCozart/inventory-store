using InventoryTracker.App.Services;
using InventoryTracker.App.Utilities;
using InventoryTracker.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryTracker.App.Tray;

public class TrayApplicationContext : ApplicationContext
{
    private static readonly int Port =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? 5051 : 5050;

    private readonly IServiceProvider _services;
    private readonly CancellationTokenSource _cts;
    private readonly TunnelService _tunnel;
    private readonly NotifyIcon _notifyIcon;
    private readonly SynchronizationContext _uiContext;

    private ToolStripMenuItem _tunnelUrlItem = null!;

    public TrayApplicationContext(IServiceProvider services, CancellationTokenSource cts)
    {
        _services   = services;
        _cts        = cts;
        _tunnel     = services.GetRequiredService<TunnelService>();
        _uiContext  = SynchronizationContext.Current ?? new SynchronizationContext();

        _notifyIcon = CreateNotifyIcon();

        _tunnel.OnStateChanged += OnTunnelStateChanged;
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var localIp = NetworkUtility.GetLocalIpAddress();
        var icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = $"Inventory Tracker — {localIp}:{Port}",
            Visible = true,
            ContextMenuStrip = CreateContextMenu(localIp)
        };

        icon.DoubleClick += (_, _) => OpenBrowser();
        return icon;
    }

    private ContextMenuStrip CreateContextMenu(string localIp)
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("Open Inventory Tracker", null, (_, _) => OpenBrowser());
        menu.Items.Add(new ToolStripSeparator());

        var networkItem = new ToolStripMenuItem($"Network: http://{localIp}:{Port}") { Enabled = false };
        menu.Items.Add(networkItem);

        _tunnelUrlItem = new ToolStripMenuItem("Tunnel: not running") { Enabled = false };
        menu.Items.Add(_tunnelUrlItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Reset Admin Password", null, (_, _) => ResetAdminPassword());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        return menu;
    }

    private Task OnTunnelStateChanged()
    {
        _uiContext.Post(_ =>
        {
            switch (_tunnel.State)
            {
                case TunnelService.TunnelState.Running:
                    _tunnelUrlItem.Text    = $"Tunnel: {_tunnel.PublicUrl}";
                    _tunnelUrlItem.Enabled = true;
                    _tunnelUrlItem.Click  -= CopyTunnelUrl;
                    _tunnelUrlItem.Click  += CopyTunnelUrl;
                    _notifyIcon.Text = $"Inventory Tracker — Tunnel active";
                    _notifyIcon.ShowBalloonTip(4000, "Tunnel Active",
                        $"Remote access: {_tunnel.PublicUrl}", ToolTipIcon.Info);
                    break;
                case TunnelService.TunnelState.Stopped:
                    _tunnelUrlItem.Text    = "Tunnel: not running";
                    _tunnelUrlItem.Enabled = false;
                    _notifyIcon.Text = $"Inventory Tracker — {NetworkUtility.GetLocalIpAddress()}:{Port}";
                    break;
                case TunnelService.TunnelState.Downloading:
                    _tunnelUrlItem.Text    = "Tunnel: downloading cloudflared…";
                    _tunnelUrlItem.Enabled = false;
                    break;
                case TunnelService.TunnelState.Starting:
                    _tunnelUrlItem.Text    = "Tunnel: starting…";
                    _tunnelUrlItem.Enabled = false;
                    break;
                case TunnelService.TunnelState.Error:
                    _tunnelUrlItem.Text    = $"Tunnel error: {_tunnel.Error}";
                    _tunnelUrlItem.Enabled = false;
                    break;
            }
        }, null);

        return Task.CompletedTask;
    }

    private void CopyTunnelUrl(object? sender, EventArgs e)
    {
        if (_tunnel.PublicUrl is not null)
            Clipboard.SetText(_tunnel.PublicUrl);
    }

    private static void OpenBrowser()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://localhost:{Port}",
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                $"Could not open browser.\r\nLocal: http://localhost:{Port}\r\nNetwork: http://{NetworkUtility.GetLocalIpAddress()}:{Port}",
                "Inventory Tracker", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ResetAdminPassword()
    {
        using var form = new ResetPasswordForm();
        if (form.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var scope = _services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IUserAuthService>();
            authService.ResetAdminPasswordAsync(form.NewPassword).GetAwaiter().GetResult();

            MessageBox.Show("Admin password has been reset successfully.",
                "Password Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to reset password: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExitApplication()
    {
        _tunnel.OnStateChanged -= OnTunnelStateChanged;

        if (_tunnel.State != TunnelService.TunnelState.Stopped)
            _tunnel.StopAsync(); // synchronous — cancels token, no blocking wait

        _notifyIcon.Visible = false;
        _cts.Cancel();
        System.Windows.Forms.Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tunnel.OnStateChanged -= OnTunnelStateChanged;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
