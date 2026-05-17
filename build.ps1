#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$ProgressPreference    = "SilentlyContinue"

$Root  = $PSScriptRoot
$Dist  = Join-Path $Root "dist"
$Tools = Join-Path $Root "tools"

Write-Host "=== StepsRecorder Build ===" -ForegroundColor Cyan

# 1. .NET 9 SDK
Write-Host "`nChecking .NET 9 SDK..." -ForegroundColor Yellow
$has9 = (dotnet --list-sdks 2>$null) -match '^9\.'
if (-not $has9) {
    Write-Host "Installing .NET 9 SDK..." -ForegroundColor Yellow
    $tmp = "$env:TEMP\dotnet-install.ps1"
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $tmp
    & $tmp -Channel 9.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
    $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
}
Write-Host "dotnet $(dotnet --version)" -ForegroundColor Green

# 2. ffmpeg
Write-Host "`nChecking ffmpeg..." -ForegroundColor Yellow
New-Item $Tools -ItemType Directory -Force | Out-Null
$ffmpeg = Join-Path $Tools "ffmpeg.exe"
if (-not (Test-Path $ffmpeg)) {
    Write-Host "Downloading ffmpeg (~75 MB)..." -ForegroundColor Yellow
    $zip = "$env:TEMP\ffmpeg.zip"
    $out = "$env:TEMP\ffmpeg-out"
    Invoke-WebRequest "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip" -OutFile $zip
    Expand-Archive $zip -DestinationPath $out -Force
    $exe = Get-ChildItem $out -Recurse -Filter ffmpeg.exe | Where-Object { $_.FullName -like "*\bin\*" } | Select-Object -First 1
    Copy-Item $exe.FullName $ffmpeg
    Remove-Item $zip,$out -Recurse -Force
}
Write-Host "ffmpeg.exe ready" -ForegroundColor Green

# 3. Restore
Write-Host "`nRestoring packages..." -ForegroundColor Yellow
Push-Location $Root
dotnet restore StepsRecorder.sln
if ($LASTEXITCODE -ne 0) { throw "restore failed" }

# 4. Publish
Write-Host "`nPublishing..." -ForegroundColor Yellow
if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }

dotnet publish src\App\App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $Dist

if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# 5. Copy extras
Copy-Item $ffmpeg          (Join-Path $Dist "ffmpeg.exe")
Copy-Item "settings.template.json" (Join-Path $Dist "settings.template.json")

Pop-Location

Write-Host "`n=== Done! ===" -ForegroundColor Green
Write-Host "Output: $Dist"
Get-ChildItem $Dist | Format-Table Name,@{N="Size";E={"{0:N0} KB" -f ($_.Length/1KB)}}
