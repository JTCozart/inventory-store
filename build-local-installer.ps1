<#
.SYNOPSIS
    Builds the Windows installer (.exe) locally, without needing CI.
    Mirrors the "build-installer" job in .github/workflows/release.yml, minus the
    icon-generation step (assets/icon.ico is already committed) and the tag/release steps.

.NOTES
    Requires Inno Setup 6 (ISCC.exe). On this machine it's a per-user winget install at:
        C:\Users\<you>\AppData\Local\Programs\Inno Setup 6\ISCC.exe
    That's a user-scope path, not Program Files, so `where.exe ISCC` and a Program Files
    search won't find it -- that's expected, not missing.

    Output: installer\Output\InventoryStore-Setup-<version>.exe
#>

[CmdletBinding()]
param(
    # Defaults to yyyyMMdd.HHmm, matching the release tag format (vYYYYMMDD.HHMM).
    [string]$Version = (Get-Date -Format 'yyyyMMdd.HHmm')
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Find-Iscc {
    $candidates = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    throw "ISCC.exe not found. Install Inno Setup 6 (winget install JRSoftware.InnoSetup) and re-run."
}

$iscc = Find-Iscc
Write-Host "Using ISCC: $iscc" -ForegroundColor DarkGray
Write-Host "Version: $Version" -ForegroundColor Cyan

Write-Host "`n== Restoring ==" -ForegroundColor Cyan
dotnet restore InventoryStore.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n== Publishing InventoryStore.App (win-x64) ==" -ForegroundColor Cyan
dotnet publish src/InventoryStore.App/InventoryStore.App.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:AssemblyVersion="1.0.0.0" `
    -p:FileVersion="1.0.0.0" `
    -p:InformationalVersion="$Version" `
    -o publish/app
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n== Publishing InventoryStore.Tray (win-x64) ==" -ForegroundColor Cyan
dotnet publish src/InventoryStore.Tray/InventoryStore.Tray.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:AssemblyVersion="1.0.0.0" `
    -p:FileVersion="1.0.0.0" `
    -p:InformationalVersion="$Version" `
    -o publish/tray
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n== Building installer ==" -ForegroundColor Cyan
& $iscc /DAppVersion="$Version" installer\setup.iss
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nDone: installer\Output\InventoryStore-Setup-$Version.exe" -ForegroundColor Green
