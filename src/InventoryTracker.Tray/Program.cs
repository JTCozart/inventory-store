namespace InventoryTracker.Tray;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        using var mutex = new Mutex(true, "InventoryTrackerTray-SingleInstance", out var isNew);
        if (!isNew)
        {
            MessageBox.Show(
                "Inventory Store tray is already running.",
                "Inventory Store", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}
