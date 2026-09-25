$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @('assets/GhostUserRunner.ico', '.github/workflows/release.yml')
$missing = $required | Where-Object { -not (Test-Path (Join-Path $root $_)) }
if ($missing) { throw "Missing release files: $($missing -join ', ')" }

$installer = Get-Content -Raw (Join-Path $root 'installer/GhostUserRunner.iss')
if ($installer -notmatch 'SetupIconFile=') { throw 'Installer does not use the branded icon.' }
if ($installer -notmatch '\{userstartup\}') { throw 'Installer does not start the local agent at user sign-in.' }
if ($installer -notmatch '--background') { throw 'Startup entry must not open the dashboard on sign-in.' }

$project = Get-Content -Raw (Join-Path $root 'src/GhostUserRunner.App/GhostUserRunner.App.csproj')
if ($project -notmatch '<ApplicationIcon>') { throw 'Windows executable has no application icon.' }

$workflow = Get-Content -Raw (Join-Path $root '.github/workflows/release.yml')
if ($workflow -notmatch 'GhostUserRunner-Setup\.exe') { throw 'Release workflow does not publish the installer.' }
Write-Host 'Release package checks passed.'
