# Inventory Tracker

A self-hosted inventory management system for small teams. Runs as a Windows service with a web UI accessible from any device on your network.

[![Release](https://img.shields.io/github/v/release/JTCozart/inventory-tracker?label=latest)](https://github.com/JTCozart/inventory-tracker/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Features

- **Reusable items** — check out and check in equipment with person tracking
- **Consumable items** — track stock levels, consume and restock
- **Barcode scanning** — camera-based scanner or manual SKU entry
- **Low stock alerts** — configurable minimum quantity thresholds
- **Activity log** — full audit trail with date/time filtering
- **Reports** — stock levels, active checkouts, lost items, take-inventory sheet, barcode sheet
- **Remote access** — built-in Cloudflare tunnel or LocalTunnel support
- **Multi-user** — Admin, Manager, and Viewer roles
- **Windows service** — runs in the background, starts at boot

---

## Installation

### Using the installer (recommended)

1. Download `InventoryTracker-Setup-<version>.exe` from the [latest release](https://github.com/JTCozart/inventory-tracker/releases/latest)
2. Run the installer as Administrator
3. The installer:
   - Installs the web server as a Windows service (auto-starts at boot)
   - Adds a tray icon to your system startup
   - Opens the tray icon immediately after install
4. Double-click the tray icon (or navigate to **http://localhost:5050**) to open the web UI
5. On first visit, create your admin account

### System requirements

- Windows 10 version 1803 or later (x64)
- No other software required — the installer is self-contained

---

## Quick start

See [QUICKSTART.md](QUICKSTART.md) for step-by-step instructions on:
- Adding your first items
- Checking items out and in
- Setting up barcode labels
- Configuring remote access

---

## Remote access

Inventory Tracker can expose the web UI to the internet via a tunnel, so staff can access it from outside the office without VPN or port forwarding.

Go to **Settings → Remote Access** and choose:

| Option | Description |
|---|---|
| Quick tunnel | Zero-config Cloudflare tunnel — URL changes each session |
| Named tunnel | Permanent custom domain via Cloudflare |
| LocalTunnel | Free public URL via localtunnel.me |

---

## Development

### Prerequisites

- .NET 8 SDK
- Windows (WinForms required)

### Run locally

```powershell
cd src/InventoryTracker.App
dotnet run
```

The app starts in tray mode and hosts the web UI at http://localhost:5050.

### Generate the icon (optional)

```powershell
# Requires ImageMagick: choco install imagemagick.app
pwsh tools/New-Icon.ps1
```

### Build the installer

```powershell
# Requires Inno Setup: choco install innosetup
dotnet publish src/InventoryTracker.App  -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/app
dotnet publish src/InventoryTracker.Tray -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/tray
iscc /DAppVersion="dev" installer/setup.iss
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
InventoryTracker.Domain          — Entities, repository interfaces
InventoryTracker.Application     — Services, DTOs, service interfaces
InventoryTracker.Infrastructure  — EF Core (SQLite), repository implementations
InventoryTracker.App             — ASP.NET Core web server + Windows service host
InventoryTracker.Tray            — Lightweight tray companion (manages the service)
```

Data is stored in `%APPDATA%\InventoryTracker\inventory.db` (SQLite).

---

## License

MIT — see [LICENSE](LICENSE).

Third-party software notices: see [NOTICES.md](NOTICES.md).
