# MonBureau Secure Credentials Setup Script
# PRODUCTION-SAFE VERSION

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MonBureau Secure Credentials Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal]
    [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "Running without administrator privileges." -ForegroundColor Yellow
    Write-Host "Credentials will be stored for the current user only." -ForegroundColor Yellow
    Write-Host ""
    $confirm = Read-Host "Continue? (y/n)"
    if ($confirm -ne "y") { exit 1 }
}

# Secure credential storage
function Store-Credential {
    param (
        [Parameter(Mandatory)]
        [string]$Key,

        [Parameter(Mandatory)]
        [string]$Value
    )

    $target = "MonBureau_$Key"

    try {
        cmdkey /delete:$target 2>$null | Out-Null
        cmdkey /generic:$target /user:MonBureau /pass:$Value | Out-Null

        Write-Host "  ✓ Stored $Key" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "  ✗ Failed to store $Key" -ForegroundColor Red
        Write-Host $_ -ForegroundColor Red
        return $false
    }
}

# ---------------------------
# Firebase configuration
# ---------------------------

Write-Host ""
Write-Host "Firebase Configuration" -ForegroundColor Yellow
Write-Host "----------------------" -ForegroundColor Yellow
Write-Host ""

$projectId = Read-Host "Firebase Project ID"
if ([string]::IsNullOrWhiteSpace($projectId)) {
    Write-Host "Project ID cannot be empty." -ForegroundColor Red
    exit 1
}

$clientEmail = Read-Host "Firebase Client Email (service account)"
if ([string]::IsNullOrWhiteSpace($clientEmail)) {
    Write-Host "Client Email cannot be empty." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Paste Firebase Private Key (BEGIN/END lines included)." -ForegroundColor Gray
Write-Host "Press ENTER twice to finish." -ForegroundColor Gray

$privateKeyLines = @()
while ($true) {
    $line = Read-Host
    if ([string]::IsNullOrWhiteSpace($line)) { break }
    $privateKeyLines += $line
}

$privateKey = $privateKeyLines -join "`n"

if (-not ($privateKey -match "BEGIN PRIVATE KEY")) {
    Write-Host "Invalid private key format." -ForegroundColor Red
    exit 1
}

# ---------------------------
# Store secrets securely
# ---------------------------

Write-Host ""
Write-Host "Storing credentials securely..." -ForegroundColor Yellow

$ok = $true
$ok = $ok -and (Store-Credential "Firebase_ProjectId"  $projectId)
$ok = $ok -and (Store-Credential "Firebase_ClientEmail" $clientEmail)
$ok = $ok -and (Store-Credential "Firebase_PrivateKey"  $privateKey)

# ---------------------------
# Database encryption key
# ---------------------------

Write-Host ""
Write-Host "Generating database encryption key..." -ForegroundColor Yellow

$keyBytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($keyBytes)
$dbKey = [Convert]::ToBase64String($keyBytes)

$ok = $ok -and (Store-Credential "Database_EncryptionKey" $dbKey)

# ---------------------------
# Non-sensitive environment variables
# ---------------------------

try {
    $scope = if ($isAdmin) { "Machine" } else { "User" }

    [Environment]::SetEnvironmentVariable(
        "MONBUREAU_FIREBASE_PROJECT_ID",
        $projectId,
        $scope
    )

    [Environment]::SetEnvironmentVariable(
        "MONBUREAU_FIREBASE_CLIENT_EMAIL",
        $clientEmail,
        $scope
    )

    Write-Host "  ✓ Non-sensitive environment variables set ($scope)" -ForegroundColor Green
}
catch {
    Write-Host "  ⚠ Failed to set environment variables (non-critical)" -ForegroundColor Yellow
}

# ---------------------------
# Final summary
# ---------------------------

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Setup Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($ok) {
    Write-Host "Credentials stored securely in Windows Credential Manager." -ForegroundColor Green
    Write-Host ""
    Write-Host "Security Notes:" -ForegroundColor Yellow
    Write-Host "  • Private keys are NOT stored in environment variables"
    Write-Host "  • No secrets written to disk"
    Write-Host "  • Least-privilege Firebase account strongly recommended"
    Write-Host ""
    Write-Host "Restart the application to ensure credentials are available." -ForegroundColor Gray
    exit 0
}
else {
    Write-Host "Setup completed with errors. Review messages above." -ForegroundColor Red
    exit 1
}
