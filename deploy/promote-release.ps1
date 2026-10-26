# Promotes a prerelease (a vYY.MMDD.HHMM-rc tag) to a stable release. The exact bits that ran on
# the prerelease servers are the ones the stable servers install: nothing is rebuilt.
#
#   pwsh deploy/promote-release.ps1 v26.1026.0900-rc
#
# Servers on CHANNEL=stable pick it up on their next timer run (within about 10 minutes).
# Run from the app repo so gh knows which repository to use.
param(
    [Parameter(Mandatory = $true)][string]$Tag
)

$ErrorActionPreference = 'Stop'

if ($Tag -notmatch '^v\d{2}\.\d{4}\.\d{4}-rc$') {
    throw "Expected a prerelease tag like v26.1026.0900-rc, got '$Tag'."
}

$release = gh release view $Tag --json isPrerelease,isDraft,tagName | ConvertFrom-Json
if ($release.isDraft) { throw "$Tag is still a draft." }
if (-not $release.isPrerelease) { Write-Host "$Tag is already a stable release."; exit 0 }

Write-Host "Promoting $Tag to stable. Servers on the stable channel will install it."
gh release edit $Tag --prerelease=false --latest
Write-Host "Done. Check on a stable server with: APP_CONF=/etc/<slug>/app.conf /usr/local/bin/<slug>-autoupdate --check"
