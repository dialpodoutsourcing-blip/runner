$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @('index.html', 'site.css', 'site.js', 'assets/ghost-runner-icon.svg', 'vercel.json')
$missing = $required | Where-Object { -not (Test-Path (Join-Path $root $_)) }
if ($missing) { throw "Missing Vercel files: $($missing -join ', ')" }

$html = Get-Content -Raw (Join-Path $root 'index.html')
if ($html -notmatch 'Download for Windows') { throw 'Landing page has no Windows download action.' }
if ($html -notmatch 'releases/latest/download/GhostUserRunner-Setup\.exe') { throw 'Windows button must link directly to the installer asset.' }
if ($html -notmatch 'http://localhost:5000') { throw 'Landing page has no installed-dashboard link.' }
if ($html -match '/api/session/') { throw 'Public page must not invoke the local control API.' }

$vercel = Get-Content -Raw (Join-Path $root 'vercel.json') | ConvertFrom-Json
if ($vercel.cleanUrls -ne $true) { throw 'Vercel cleanUrls must be enabled.' }
Write-Host 'Vercel landing-page checks passed.'
