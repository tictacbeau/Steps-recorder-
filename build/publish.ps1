#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and packages StepsRecorder into a portable /dist folder.
    Run from the repository root: .\build\publish.ps1

.NOTES
    Prerequisites:
      - .NET 9 SDK (https://dot.net)
      - ffmpeg.exe placed in tools\ffmpeg.exe  (download from https://ffmpeg.org/download.html)
#>

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root "dist"

Write-Host "=== StepsRecorder — Portable Build ===" -ForegroundColor Cyan
Write-Host "Root:   $root"
Write-Host "Output: $dist"
Write-Host ""

# Clean previous dist
if (Test-Path $dist) {
    Write-Host "Cleaning previous dist..." -ForegroundColor Yellow
    Remove-Item $dist -Recurse -Force
}
New-Item $dist -ItemType Directory | Out-Null

# Build
Write-Host "Publishing (self-contained win-x64)..." -ForegroundColor Yellow
Push-Location $root
try {
    dotnet publish "src\App\App.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=embedded `
        -o $dist

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
}
finally { Pop-Location }

# Copy ffmpeg
$ffmpegSrc = Join-Path $root "tools\ffmpeg.exe"
if (Test-Path $ffmpegSrc) {
    Write-Host "Copying ffmpeg.exe..." -ForegroundColor Yellow
    Copy-Item $ffmpegSrc (Join-Path $dist "ffmpeg.exe")
} else {
    Write-Warning @"
ffmpeg.exe not found at tools\ffmpeg.exe.
Download the Windows build from https://ffmpeg.org/download.html (e.g. the gyan.dev release)
and place ffmpeg.exe in the tools\ folder, then re-run this script.
The app will still work without ffmpeg but video recording will be disabled.
"@
}

# Copy settings template (if not already there)
$templateSrc = Join-Path $root "settings.template.json"
$templateDst = Join-Path $dist "settings.template.json"
if (Test-Path $templateSrc) {
    Copy-Item $templateSrc $templateDst
}

Write-Host ""
Write-Host "=== Build complete ===" -ForegroundColor Green
Write-Host "Portable folder: $dist"
Write-Host ""
Write-Host "Contents:"
Get-ChildItem $dist | ForEach-Object { Write-Host "  $_" }
