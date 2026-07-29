#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Install or uninstall the Inventory Tracker Windows Service.
.PARAMETER Action
    install | uninstall
.EXAMPLE
    .\install-service.ps1 install
    .\install-service.ps1 uninstall
#>
param(
    [ValidateSet("install", "uninstall")]
    [string]$Action = "install"
)

$ServiceName = "InventoryStore"
$DisplayName = "Inventory Tracker"
$Description = "Inventory Tracker web server. Accessible at http://localhost:5050"
$ExePath = Join-Path $PSScriptRoot "publish\InventoryStore.App.exe"

if ($Action -eq "install") {
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Service already exists. Stopping and removing first..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    }

    if (-not (Test-Path $ExePath)) {
        Write-Error "Executable not found at: $ExePath`nRun 'dotnet publish' first."
        exit 1
    }

    New-Service -Name $ServiceName `
                -BinaryPathName "`"$ExePath`" --service" `
                -DisplayName $DisplayName `
                -Description $Description `
                -StartupType Automatic

    # Error-level log entries (e.g. a failed Mailjet send) are also written to the Windows
    # Event Log; the source must exist before anything can log to it. Safe to skip if it's
    # already registered or if creation fails for some reason -- the rolling file log still works.
    if (-not [System.Diagnostics.EventLog]::SourceExists("InventoryStore")) {
        try {
            [System.Diagnostics.EventLog]::CreateEventSource("InventoryStore", "Application")
        } catch {
            Write-Warning "Could not register the 'InventoryStore' event log source: $_"
        }
    }

    Start-Service -Name $ServiceName
    Write-Host "Service '$ServiceName' installed and started." -ForegroundColor Green
    Write-Host "Access the web UI at: http://localhost:5050" -ForegroundColor Cyan
}
elseif ($Action -eq "uninstall") {
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Write-Host "Service '$ServiceName' removed." -ForegroundColor Yellow
    }
    else {
        Write-Host "Service '$ServiceName' not found."
    }
}
