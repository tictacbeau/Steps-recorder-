#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$ProgressPreference    = "SilentlyContinue"

$Root  = $PSScriptRoot
$Dist  = Join-Path $Root "dist"
$Tools = Join-Path $Root "tools"

Write-Host "=== StepsRecorder Build ===" -ForegroundColor Cyan

# ── 1. .NET 9 SDK ─────────────────────────────────────────────
Write-Host "`nChecking .NET 9 SDK..." -ForegroundColor Yellow

# Check if dotnet exists at all
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue

if ($dotnetCmd) {
    $has9 = (& dotnet --list-sdks 2>&1) -match '^9\.'
} else {
    $has9 = $false
}

if (-not $has9) {
    Write-Host ".NET 9 SDK not found — installing..." -ForegroundColor Yellow

    $installScript = "$env:TEMP\dotnet-install.ps1"
    $dotnetDir     = "$env:LOCALAPPDATA\Microsoft\dotnet"

    Write-Host "  Downloading dotnet-install.ps1..."
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript -UseBasicParsing

    Write-Host "  Installing .NET 9 SDK to $dotnetDir ..."
    & powershell -ExecutionPolicy Bypass -File $installScript -Channel 9.0 -InstallDir $dotnetDir

    # Add to PATH for this session
    if ($env:PATH -notlike "*$dotnetDir*") {
        $env:PATH = "$dotnetDir;$env:PATH"
    }

    # Verify install worked
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCmd) {
        # Try explicit path
        $dotnetExe = Join-Path $dotnetDir "dotnet.exe"
        if (Test-Path $dotnetExe) {
            Set-Alias dotnet $dotnetExe -Scope Script
        } else {
            Write-Host "ERROR: .NET install failed. Please install manually from https://dot.net" -ForegroundColor Red
            exit 1
        }
    }
}

$ver = & dotnet --version
Write-Host "dotnet $ver" -ForegroundColor Green

# ── 2. ffmpeg ─────────────────────────────────────────────────
Write-Host "`nChecking ffmpeg..." -ForegroundColor Yellow
New-Item $Tools -ItemType Directory -Force | Out-Null
$ffmpegExe = Join-Path $Tools "ffmpeg.exe"

if (-not (Test-Path $ffmpegExe)) {
    Write-Host "Downloading ffmpeg (~75 MB)..." -ForegroundColor Yellow
    $zip = "$env:TEMP\ffmpeg.zip"
    $out = "$env:TEMP\ffmpeg-out"

    Invoke-WebRequest "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip" -OutFile $zip -UseBasicParsing
    Write-Host "  Extracting..."
    Expand-Archive $zip -DestinationPath $out -Force

    $found = Get-ChildItem $out -Recurse -Filter ffmpeg.exe |
             Where-Object { $_.FullName -like "*\bin\*" } |
             Select-Object -First 1

    if (-not $found) {
        Write-Host "ERROR: Could not find ffmpeg.exe in zip" -ForegroundColor Red
        exit 1
    }

    Copy-Item $found.FullName $ffmpegExe
    Remove-Item $zip, $out -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "ffmpeg.exe ready" -ForegroundColor Green

# ── 3. Restore ────────────────────────────────────────────────
Write-Host "`nRestoring packages..." -ForegroundColor Yellow
Push-Location $Root
& dotnet restore StepsRecorder.sln
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: restore failed" -ForegroundColor Red; exit 1 }

# ── 4. Publish ────────────────────────────────────────────────
Write-Host "`nBuilding..." -ForegroundColor Yellow
if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }

& dotnet publish src\App\App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -o $Dist

if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: publish failed" -ForegroundColor Red; exit 1 }

Copy-Item $ffmpegExe             (Join-Path $Dist "ffmpeg.exe")
Copy-Item "settings.template.json" (Join-Path $Dist "settings.template.json")

Pop-Location

# ── Done ──────────────────────────────────────────────────────
Write-Host "`n=== Done! ===" -ForegroundColor Green
Write-Host "Output folder: $Dist" -ForegroundColor Green
Write-Host ""
Get-ChildItem $Dist | Format-Table Name, @{N="Size";E={"{0:N0} KB" -f ($_.Length/1KB)}}
