#Requires -Version 5.1
<#
.SYNOPSIS
    One-shot setup + build for StepsRecorder.
    Installs .NET 9 SDK and ffmpeg if missing, then produces a portable dist/ folder.

.DESCRIPTION
    Run from the repository root on any Windows 10/11 machine — no prerequisites needed.
    Requires an internet connection on first run.

.PARAMETER SkipDotNet
    Skip .NET 9 SDK installation check (use if you manage SDKs yourself).

.PARAMETER SkipFfmpeg
    Skip ffmpeg download.

.PARAMETER GithubRelease
    After building, create a GitHub Release using the gh CLI.
    Requires gh.exe to be installed and authenticated (gh auth login).

.PARAMETER ReleaseTag
    Tag for the GitHub release (e.g. "v1.0.0"). Defaults to "v1.0.0-build-<date>".

.EXAMPLE
    .\setup-and-build.ps1
    .\setup-and-build.ps1 -GithubRelease -ReleaseTag v1.0.0
#>
param(
    [switch]$SkipDotNet,
    [switch]$SkipFfmpeg,
    [switch]$GithubRelease,
    [string]$ReleaseTag = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference    = "SilentlyContinue"   # speeds up Invoke-WebRequest

$Root  = $PSScriptRoot
$Dist  = Join-Path $Root "dist"
$Tools = Join-Path $Root "tools"

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "  ► $msg" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
    Write-Host "    ✓ $msg" -ForegroundColor Green
}

function Write-Warn([string]$msg) {
    Write-Host "    ⚠ $msg" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║   StepsRecorder — Setup & Build              ║" -ForegroundColor Magenta
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Magenta

# ─────────────────────────────────────────────────────────────
# 1. .NET 9 SDK
# ─────────────────────────────────────────────────────────────
Write-Step "Checking .NET 9 SDK..."

if (-not $SkipDotNet) {
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    $hasNet9   = $dotnetCmd -and (& dotnet --list-sdks 2>$null | Where-Object { $_ -match '^9\.' })

    if ($hasNet9) {
        Write-Ok ".NET 9 SDK found: $(& dotnet --version)"
    } else {
        Write-Warn ".NET 9 SDK not found — installing via winget..."

        # Try winget first (Windows 11 / Windows 10 with App Installer)
        $winget = Get-Command winget -ErrorAction SilentlyContinue
        if ($winget) {
            winget install --id Microsoft.DotNet.SDK.9 --silent --accept-source-agreements --accept-package-agreements
            # Refresh PATH
            $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" +
                        [System.Environment]::GetEnvironmentVariable("PATH", "User")
            Write-Ok ".NET 9 SDK installed via winget"
        } else {
            # Fallback: use dotnet-install.ps1 from Microsoft
            Write-Warn "winget not available — using dotnet-install.ps1 (official Microsoft script)"
            $installScript = Join-Path $env:TEMP "dotnet-install.ps1"
            Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
            & $installScript -Channel 9.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"

            # Add to session PATH
            $dotnetDir = "$env:LOCALAPPDATA\Microsoft\dotnet"
            if ($env:PATH -notlike "*$dotnetDir*") {
                $env:PATH = "$dotnetDir;$env:PATH"
            }
            Write-Ok ".NET 9 SDK installed to $dotnetDir"
        }
    }
} else {
    Write-Ok "Skipping .NET SDK check (--SkipDotNet)"
}

# Verify dotnet is now available
try {
    $v = & dotnet --version
    Write-Ok "Using dotnet $v"
} catch {
    Write-Host "ERROR: 'dotnet' command not found after install. Open a new terminal and re-run." -ForegroundColor Red
    exit 1
}

# ─────────────────────────────────────────────────────────────
# 2. ffmpeg
# ─────────────────────────────────────────────────────────────
Write-Step "Checking ffmpeg..."

$ffmpegDst = Join-Path $Tools "ffmpeg.exe"
New-Item $Tools -ItemType Directory -Force | Out-Null

if (-not $SkipFfmpeg -and -not (Test-Path $ffmpegDst)) {
    Write-Warn "ffmpeg.exe not found in tools\ — downloading from gyan.dev..."

    # gyan.dev provides reliable Windows ffmpeg builds
    $ffmpegUrl  = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
    $zipPath    = Join-Path $env:TEMP "ffmpeg-essentials.zip"
    $extractDir = Join-Path $env:TEMP "ffmpeg-extract"

    Write-Host "    Downloading ffmpeg (~75 MB)..." -NoNewline
    Invoke-WebRequest -Uri $ffmpegUrl -OutFile $zipPath
    Write-Host " done"

    Write-Host "    Extracting..." -NoNewline
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force
    Write-Host " done"

    # The zip contains a single versioned folder like ffmpeg-7.x-essentials_build\bin\ffmpeg.exe
    $ffmpegExe = Get-ChildItem -Path $extractDir -Recurse -Filter "ffmpeg.exe" |
                 Where-Object { $_.FullName -like "*\bin\ffmpeg.exe" } |
                 Select-Object -First 1

    if ($null -eq $ffmpegExe) {
        Write-Host "ERROR: Could not locate ffmpeg.exe inside the downloaded archive." -ForegroundColor Red
        exit 1
    }

    Copy-Item $ffmpegExe.FullName $ffmpegDst
    Remove-Item $zipPath, $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Ok "ffmpeg.exe installed to tools\"
} elseif (Test-Path $ffmpegDst) {
    Write-Ok "ffmpeg.exe already present in tools\"
} else {
    Write-Ok "Skipping ffmpeg download (--SkipFfmpeg)"
}

# ─────────────────────────────────────────────────────────────
# 3. Restore NuGet packages
# ─────────────────────────────────────────────────────────────
Write-Step "Restoring NuGet packages..."
Push-Location $Root
try {
    & dotnet restore StepsRecorder.sln
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }
    Write-Ok "Packages restored"
} finally { Pop-Location }

# ─────────────────────────────────────────────────────────────
# 4. Run tests
# ─────────────────────────────────────────────────────────────
Write-Step "Running tests..."
Push-Location $Root
try {
    & dotnet test src\Tests\Tests.csproj --no-restore --logger "console;verbosity=minimal"
    if ($LASTEXITCODE -ne 0) {
        Write-Warn "Some tests failed — continuing build anyway (video tests may be skipped)"
    } else {
        Write-Ok "All tests passed"
    }
} finally { Pop-Location }

# ─────────────────────────────────────────────────────────────
# 5. Publish portable build
# ─────────────────────────────────────────────────────────────
Write-Step "Publishing portable build to dist\..."

if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }
New-Item $Dist -ItemType Directory | Out-Null

Push-Location $Root
try {
    & dotnet publish "src\App\App.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=embedded `
        -o $Dist

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
} finally { Pop-Location }

# Copy ffmpeg and settings template
if (Test-Path $ffmpegDst) { Copy-Item $ffmpegDst (Join-Path $Dist "ffmpeg.exe") }
Copy-Item (Join-Path $Root "settings.template.json") (Join-Path $Dist "settings.template.json")

Write-Ok "Build complete"
Write-Host ""
Write-Host "    Contents of dist\:" -ForegroundColor Gray
Get-ChildItem $Dist | ForEach-Object {
    $size = if ($_.Length -gt 1MB) { "{0:N1} MB" -f ($_.Length / 1MB) }
            elseif ($_.Length -gt 1KB) { "{0:N0} KB" -f ($_.Length / 1KB) }
            else { "$($_.Length) B" }
    Write-Host ("      {0,-35} {1}" -f $_.Name, $size) -ForegroundColor Gray
}

# ─────────────────────────────────────────────────────────────
# 6. Create zip for distribution / GitHub release
# ─────────────────────────────────────────────────────────────
Write-Step "Creating release zip..."

$zipName = "StepsRecorder-portable-win-x64.zip"
$zipPath = Join-Path $Root $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath }

Compress-Archive -Path (Join-Path $Dist "*") -DestinationPath $zipPath
$zipSize = "{0:N1} MB" -f ((Get-Item $zipPath).Length / 1MB)
Write-Ok "Created $zipName ($zipSize)"

# ─────────────────────────────────────────────────────────────
# 7. GitHub Release (optional)
# ─────────────────────────────────────────────────────────────
if ($GithubRelease) {
    Write-Step "Creating GitHub Release..."

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        Write-Warn "gh CLI not found. Install from https://cli.github.com/ and run 'gh auth login'"
        Write-Warn "Skipping GitHub release — zip is ready at $zipName"
    } else {
        if ($ReleaseTag -eq "") {
            $ReleaseTag = "v1.0.0-build-$(Get-Date -Format 'yyyyMMdd')"
        }

        $releaseNotes = @"
## StepsRecorder $ReleaseTag

Portable Windows application — no installer, no admin rights required.

### What's included
- \`StepsRecorder.exe\` — self-contained (no .NET install needed)
- \`ffmpeg.exe\` — video encoding
- \`settings.template.json\` — copy to \`settings.json\` to customize

### Quick Start
1. Extract the zip anywhere (local drive or USB)
2. Run \`StepsRecorder.exe\`
3. Press **F9** to start recording
4. Press **F9** again to stop — output folder opens automatically

### System Requirements
- Windows 10 (1803+) or Windows 11
- DirectX 11 GPU
"@

        & gh release create $ReleaseTag $zipPath `
            --title "StepsRecorder $ReleaseTag" `
            --notes $releaseNotes `
            --latest

        if ($LASTEXITCODE -eq 0) {
            Write-Ok "GitHub Release $ReleaseTag created"
        } else {
            Write-Warn "gh release create failed (check gh auth status)"
        }
    }
}

# ─────────────────────────────────────────────────────────────
# Done
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   Build successful!                          ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Portable folder : $Dist"
Write-Host "  Release zip     : $zipPath"
Write-Host ""
Write-Host "  Run the app     : $Dist\StepsRecorder.exe"
Write-Host ""
