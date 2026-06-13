# Inventory Store

A self-hosted inventory management system for small teams. Runs as a Windows service with a web UI accessible from any device on your network.

[![Release](https://img.shields.io/github/v/release/JTCozart/inventory-store?label=latest)](https://github.com/JTCozart/inventory-store/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Features

- **Reusable items** - check out and check in equipment with person tracking
- **Consumable items** - track stock levels, consume and restock
- **Convert item types** - switch an item between Reusable and Consumable after creation (check in all units first for reusables)
- **Terminal mode** - a touch-first `/terminal` screen for a shared tablet or front desk: scan or search, then check out, check in, consume, and (for Admins/Managers) restock
- **Categories** - organize items into color-coded groups with filtering
- **Tags** - add any number of free-form labels to an item with a type-ahead field; auto-colored pills show on the item, Terminal, and inventory search; manage and rename them under Settings → Tags
- **Expiry dates** - track expiry on any item; Expired and Expiring Soon badges; dedicated expiry report
- **Barcode scanning** - camera-based scanner (UPC, EAN, CODE_128, QR codes) or manual SKU entry; print barcode sheets
- **Smart product lookup** - scan a barcode and pull product details from free public databases (Open Library, UPC Item DB, Open Food Facts); auto-fills name, image, brand, category, size, and weight when adding or editing items
- **Product metadata library** - browse, refresh, and delete the product data cached from lookups; deleting unlinks it from any items automatically
- **Modules** - optional add-ons you switch on or off under Settings → Modules; each stays out of the way until enabled
- **Safety Data Sheets (module)** - look up chemical hazard data (signal word, GHS pictograms, CAS number, hazard statements) for an item by chemical name from PubChem, free and keyless; pictograms show on the inventory list, scan, and item cards
- **Cost & Valuation (module)** - record a unit cost per item; total inventory value, category breakdown, and straight-line depreciation, with a Valuation report
- **Consumption Forecasting (module)** - projects run-out dates for consumables from a real usage history, with a Forecast report
- **Webhooks & Integrations (module)** - send signed JSON to any URL on inventory events (checkout, low stock, item changes); connect Slack, Zapier, Make, or your own scripts
- **CSV import / export** - bulk-load your inventory from a spreadsheet or export for backup, including a Tags column
- **Low stock alerts** - configurable minimum quantity thresholds
- **Reports** - stock levels, active checkouts, lost items, expiry, take-inventory sheet, barcode sheet, activity log
- **Remote access** - built-in Cloudflare Quick Tunnel or Serveo (free persistent subdomain)
- **Multi-user** - Admin, Manager, Staff, and Viewer roles; Staff are scoped to the Terminal for check out and consume
- **Auto-update notifications** - checks GitHub releases and shows a notice in the sidebar
- **Windows service** - runs in the background, starts at boot

---

## Installation

### Using the installer (recommended)

1. Download `InventoryStore-Setup-<version>.exe` from the [latest release](https://github.com/JTCozart/inventory-store/releases/latest)
2. Run the installer as Administrator
3. The installer:
   - Stops any running instance before copying files
   - Installs the web server as a Windows service (auto-starts at boot)
   - Adds a tray icon to your system startup
   - Opens the tray icon immediately after install
4. Double-click the tray icon (or navigate to **http://localhost:5050**) to open the web UI
5. On first visit, create your admin account

### System requirements

- Windows 10 version 1803 or later (x64)
- OpenSSH Client (built-in on Windows 10 1809+) - required only for Serveo remote access
- No other software required - the installer is self-contained

### Linux (Ubuntu / Debian)

Linux runs the same web app headless as a systemd service (no tray icon - manage it with `systemctl` and reach the UI in your browser).

1. Download `InventoryStore-linux-x64-<version>.tar.gz` from the [latest release](https://github.com/JTCozart/inventory-store/releases/latest)
2. Extract it and run the installer as root:
   ```bash
   tar xzf InventoryStore-linux-x64-<version>.tar.gz
   cd InventoryStore-linux-x64-<version>
   sudo ./install.sh
   ```
3. The installer creates an `inventorystore` service account, installs to `/opt/inventorystore`, stores data in `/var/lib/inventorystore`, and enables the service at boot
4. Open **http://localhost:5050** (or `http://<host-ip>:5050`) to create your admin account

Service management:

```bash
systemctl status inventorystore     # check state
journalctl -u inventorystore -f     # follow logs
sudo systemctl restart inventorystore
```

**Linux system requirements:**

- A 64-bit Ubuntu/Debian-based distribution with systemd (the release archive is self-contained, no .NET install required)
- `openssh-client` (`sudo apt install openssh-client`) - required only for Serveo remote access

---

## Quick start

See [QUICKSTART.md](QUICKSTART.md) for step-by-step instructions on the most common tasks.

See the **[User Guide](https://inventorystore.app/user-guide.html)** for full documentation.

---

## Remote access

Inventory Store can expose the web UI over the internet so staff can access it from outside the office without VPN or port forwarding.

Go to **Settings → Remote Access** and choose:

| Option | Description |
|---|---|
| Quick Tunnel | Zero-config Cloudflare tunnel - URL changes each session |
| Serveo | Free persistent subdomain (`yourname.serveousercontent.com`) - requires a free Serveo account |
| localtunnel | Free subdomain via loca.lt - shows an interstitial page to visitors |

**Serveo setup** (one-time):
1. Settings → Remote Access → Serveo → **Generate SSH Key**
2. Copy the public key and add it to your account at [console.serveo.net](https://console.serveo.net)
3. Choose a subdomain and save
4. Click the Serveo button to start the tunnel

---

## Development

### Prerequisites

- .NET 8 SDK
- Windows for the tray/installer build (WinForms); Linux builds and runs headless

### Run locally

```powershell
cd src/InventoryStore.App
dotnet run
```

On Windows the app starts in tray mode; on Linux it runs headless. Either way it hosts the web UI at **http://localhost:5051** (dev mode uses 5051 to avoid conflicting with a running production service on 5050).

The build target is selected by the runtime identifier: a `linux-*` RID compiles a headless `net8.0` build (no WinForms, no tray), and anything else compiles the Windows `net8.0-windows` build with the tray companion. Plain `dotnet run` with no RID uses the Windows configuration.

### Build for Linux

```bash
dotnet publish src/InventoryStore.App/InventoryStore.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish/linux-app
```

Stage the release archive alongside the systemd files in `linux/`:

```bash
mkdir -p stage/app && cp -r publish/linux-app/. stage/app/
cp linux/install.sh linux/inventorystore.service stage/
tar czf InventoryStore-linux-x64.tar.gz -C stage .
```

### Generate the icon (optional)

```powershell
# Requires ImageMagick: choco install imagemagick.app
pwsh tools/New-Icon.ps1
```

### Build the installer

```powershell
# Requires Inno Setup: choco install innosetup
$v = (Get-Date -Format "yyyyMMdd.HHmm")
dotnet publish src/InventoryStore.App  -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/app
dotnet publish src/InventoryStore.Tray -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/tray
iscc /DAppVersion="$v" /DAppSemVer="1.$v" installer/setup.iss
```

### Release

Push a tag in the format `vYYYYMMDD.HHMM` to trigger the GitHub Actions release workflow:

```bash
git tag v20260606.1430
git push origin v20260606.1430
```

GitHub Actions will build the installer and create a release automatically.

---

## Architecture

```
InventoryStore.Domain          - Entities, repository interfaces
InventoryStore.Application     - Services, DTOs, service interfaces
InventoryStore.Infrastructure  - EF Core (SQLite), repository implementations
InventoryStore.App             - ASP.NET Core web server + Windows service host
InventoryStore.Tray            - Lightweight tray companion (manages the service)
```

Data is stored in `%APPDATA%\InventoryStore\inventory.db` (SQLite). The schema is automatically migrated on startup - no manual migration steps required.

---

## License

MIT - see [LICENSE](LICENSE).

Third-party software notices: see [NOTICES.md](NOTICES.md).
