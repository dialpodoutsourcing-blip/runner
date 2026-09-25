param([switch]$Installer)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$output = Join-Path $projectRoot 'artifacts\portable\GhostUserRunner'

& $dotnet test (Join-Path $projectRoot 'GhostUserRunner.sln') -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; package was not created.' }

& $dotnet publish (Join-Path $projectRoot 'src\GhostUserRunner.App\GhostUserRunner.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

New-Item -ItemType Directory -Path (Join-Path $output 'SafeFiles') -Force | Out-Null

$env:PLAYWRIGHT_BROWSERS_PATH = '0'
& (Join-Path $output 'playwright.ps1') install chromium
if ($LASTEXITCODE -ne 0) { throw 'Chromium installation failed.' }

Get-ChildItem $output -File -Recurse | Where-Object Name -ne 'SHA256SUMS.txt' | Get-FileHash -Algorithm SHA256 | ForEach-Object {
    "{0}  {1}" -f $_.Hash, $_.Path.Substring($output.Length).TrimStart('\')
} | Set-Content (Join-Path $output 'SHA256SUMS.txt')

if ($Installer)
{
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compiler) { & $compiler.Source (Join-Path $projectRoot 'installer\GhostUserRunner.iss') }
    else { Write-Warning 'Inno Setup is not installed; the portable app was created successfully.' }
}

Write-Host "Portable app: $output\GhostUserRunner.App.exe"
