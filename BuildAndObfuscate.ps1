# ============================================================
# MonBureau - Build & Obfuscation Pipeline
# Production-ready PowerShell script
# ============================================================

param(
    [string]$Configuration = "Release",
    [string]$Platform = "Any CPU",
    [switch]$SkipBuild = $false,
    [switch]$SkipObfuscation = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

# ------------------------------------------------------------
# CONFIGURATION (CHANGE ONLY HERE)
# ------------------------------------------------------------
$AppName        = "MonBureau"
$SolutionFile   = "MonBureau.sln"
$AppExeName     = "MonBureau.exe"

$ConfuserExPath = "tools\ConfuserEx\Confuser.CLI.exe"
$ConfuserConfig = "confuser.crproj"

$OutputDir      = "bin\$Configuration"
$ObfuscatedDir  = "bin\Obfuscated"
$ReleaseDir     = "Release"

$BuildInfoFile  = "build-info.txt"
# ------------------------------------------------------------

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  $AppName Build & Obfuscation Pipeline" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Platform: $Platform" -ForegroundColor Yellow
Write-Host ""

# ------------------------------------------------------------
# PREREQUISITES
# ------------------------------------------------------------
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

if (-not (Test-Path $SolutionFile)) {
    throw "Solution file not found: $SolutionFile"
}

if (-not $SkipObfuscation) {
    if (-not (Test-Path $ConfuserExPath)) {
        throw "ConfuserEx not found at $ConfuserExPath. Run setup-tools.ps1 first."
    }

    if (-not (Test-Path $ConfuserConfig)) {
        throw "ConfuserEx config not found: $ConfuserConfig"
    }
}

# ------------------------------------------------------------
# MSBUILD DISCOVERY
# ------------------------------------------------------------
try {
    $msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
        -latest -requires Microsoft.Component.MSBuild `
        -find MSBuild\**\Bin\MSBuild.exe |
        Select-Object -First 1

    if (-not $msbuildPath) {
        throw "MSBuild not found"
    }

    Write-Host "  ✓ MSBuild found" -ForegroundColor Green
}
catch {
    throw "MSBuild not found. Install Visual Studio Build Tools."
}

# ------------------------------------------------------------
# STEP 1 – CLEAN
# ------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Step 1: Cleaning solution..." -ForegroundColor Yellow

    & $msbuildPath $SolutionFile /t:Clean `
        /p:Configuration=$Configuration `
        /p:Platform="$Platform" `
        /v:minimal

    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }

    Write-Host "  ✓ Clean completed" -ForegroundColor Green
}

# ------------------------------------------------------------
# STEP 2 – RESTORE
# ------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Step 2: Restoring NuGet packages..." -ForegroundColor Yellow

    & $msbuildPath $SolutionFile /t:Restore /v:minimal

    if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed" }

    Write-Host "  ✓ Restore completed" -ForegroundColor Green
}

# ------------------------------------------------------------
# STEP 3 – BUILD
# ------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Step 3: Building solution..." -ForegroundColor Yellow

    $verbosity = if ($Verbose) { "detailed" } else { "minimal" }

    & $msbuildPath $SolutionFile /t:Build `
        /p:Configuration=$Configuration `
        /p:Platform="$Platform" `
        /p:Optimize=true `
        /p:DebugSymbols=false `
        /p:DebugType=None `
        /v:$verbosity

    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    Write-Host "  ✓ Build completed" -ForegroundColor Green
}

# ------------------------------------------------------------
# STEP 4 – OBFUSCATION
# ------------------------------------------------------------
if (-not $SkipObfuscation) {
    Write-Host ""
    Write-Host "Step 4: Obfuscating assemblies..." -ForegroundColor Yellow

    if (Test-Path $ObfuscatedDir) {
        Remove-Item $ObfuscatedDir -Recurse -Force
    }

    $args = @("-n", $ConfuserConfig)
    if ($Verbose) { $args += "-debug" }

    & $ConfuserExPath $args
    if ($LASTEXITCODE -ne 0) { throw "Obfuscation failed" }

    Write-Host "  ✓ Obfuscation completed" -ForegroundColor Green

    # Verification
    $originalExe  = Join-Path $OutputDir $AppExeName
    $obfuscatedExe = Join-Path $ObfuscatedDir $AppExeName

    if (-not (Test-Path $obfuscatedExe)) {
        throw "Obfuscated executable not found"
    }

    $origSize = (Get-Item $originalExe).Length
    $obfSize  = (Get-Item $obfuscatedExe).Length
    $ratio    = [Math]::Round($obfSize / $origSize, 2)

    Write-Host "  Size ratio: $ratio" -ForegroundColor Gray

    if ($ratio -lt 0.5 -or $ratio -gt 2.0) {
        Write-Host "  ⚠ Unusual obfuscation size ratio" -ForegroundColor Yellow
    }

    Write-Host "  ✓ Verification passed" -ForegroundColor Green
}

# ------------------------------------------------------------
# STEP 5 – RELEASE PACKAGE
# ------------------------------------------------------------
Write-Host ""
Write-Host "Step 5: Creating release package..." -ForegroundColor Yellow

if (Test-Path $ReleaseDir) {
    Remove-Item $ReleaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null

$sourceDir = if ($SkipObfuscation) { $OutputDir } else { $ObfuscatedDir }

Copy-Item "$sourceDir\*" $ReleaseDir -Recurse -Force

# Copy NON-BINARY assets only
$assetPatterns = @("*.json", "*.config", "Assets\*", "Resources\*")

foreach ($pattern in $assetPatterns) {
    Get-ChildItem $OutputDir -Filter $pattern -Recurse -ErrorAction SilentlyContinue |
        Copy-Item -Destination $ReleaseDir -Recurse -Force
}

Write-Host "  ✓ Release files copied" -ForegroundColor Green

# ------------------------------------------------------------
# STEP 6 – BUILD INFO
# ------------------------------------------------------------
$buildInfo = @"
Application : $AppName
Configuration: $Configuration
Build Date  : $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Machine     : $env:COMPUTERNAME
"@

$buildInfo | Out-File (Join-Path $ReleaseDir $BuildInfoFile) -Encoding UTF8

# ------------------------------------------------------------
# STEP 7 – CHECKSUMS
# ------------------------------------------------------------
Write-Host ""
Write-Host "Step 6: Generating checksums..." -ForegroundColor Yellow

$checksumFile = Join-Path $ReleaseDir "checksums.txt"
Get-ChildItem $ReleaseDir -File -Recurse |
    ForEach-Object {
        $hash = Get-FileHash $_.FullName -Algorithm SHA256
        "$($hash.Hash)  $($_.FullName.Replace("$ReleaseDir\", ""))"
    } | Out-File $checksumFile -Encoding UTF8

Write-Host "  ✓ Checksums generated" -ForegroundColor Green

# ------------------------------------------------------------
# SUMMARY
# ------------------------------------------------------------
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  BUILD COMPLETE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output: $ReleaseDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next mandatory steps:" -ForegroundColor Yellow
Write-Host "  • Test obfuscated build" -ForegroundColor Gray
Write-Host "  • Code sign executables" -ForegroundColor Gray
Write-Host "  • Build installer (MSI/EXE)" -ForegroundColor Gray
Write-Host ""

exit 0
