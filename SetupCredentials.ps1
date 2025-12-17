# ============================================================
# MonBureau Secure Credentials Setup Script (Production)
# Reads Firebase key from a JSON file directly
# ============================================================

$ErrorActionPreference = "Stop"

function Write-Header { param([string]$Text)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success { param([string]$Text) Write-Host "  V $Text" -ForegroundColor Green }
function Write-Error { param([string]$Text) Write-Host "  ? $Text" -ForegroundColor Red }
function Write-Warning { param([string]$Text) Write-Host "  ! $Text" -ForegroundColor Yellow }

function Store-SecureCredential {
    param (
        [Parameter(Mandatory)] [string]$Key,
        [Parameter(Mandatory)] [string]$Value
    )

    $targetName = "MonBureau_$Key"

    try {
        cmdkey /delete:$targetName 2>$null | Out-Null

        $tempFile = [System.IO.Path]::GetTempFileName()
        try {
            $Value | Out-File -FilePath $tempFile -Encoding ASCII -NoNewline
            cmdkey /generic:$targetName /user:MonBureau /pass:"$(Get-Content $tempFile -Raw)" | Out-Null
            Write-Success "Stored $Key"
            return $true
        } finally {
            if (Test-Path $tempFile) { Remove-Item $tempFile -Force }
        }
    } catch {
        Write-Error "Failed to store $Key - $($_.Exception.Message)"
        return $false
    }
}

# ============================================================
# MAIN SCRIPT
# ============================================================

Write-Header "MonBureau Secure Credentials Setup"

# Admin check
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "Running without administrator privileges"
    Write-Warning "Credentials will be stored for current user only"
    $confirm = Read-Host "Continue? (y/n)"
    if ($confirm -ne "y") { Write-Host "Setup cancelled"; exit 1 }
}

# ============================================================
# Firebase Configuration
# ============================================================

$firebaseKeyFile = "D:\majey\monbureau-licenses-firebase-adminsdk-fbsvc-fa47f004e8.json"

if (-not (Test-Path $firebaseKeyFile)) {
    Write-Error "Firebase key file not found at $firebaseKeyFile"
    exit 1
}

try {
    $firebaseJson = Get-Content $firebaseKeyFile -Raw
    $firebaseData = $firebaseJson | ConvertFrom-Json
} catch {
    Write-Error "Failed to read or parse Firebase key JSON: $($_.Exception.Message)"
    exit 1
}

$projectId = $firebaseData.project_id
$clientEmail = $firebaseData.client_email
$privateKey = $firebaseData.private_key -replace "\\n", "`n"  # Preserve newlines for Firebase SDK

if (-not $projectId -or -not $clientEmail -or -not $privateKey) {
    Write-Error "Firebase JSON missing required fields (project_id, client_email, private_key)"
    exit 1
}

# ============================================================
# Store Credentials Securely
# ============================================================

Write-Host ""
Write-Host "Storing credentials securely..." -ForegroundColor Yellow

$allSuccess = $true
$allSuccess = $allSuccess -and (Store-SecureCredential "Firebase_ProjectId" $projectId)
$allSuccess = $allSuccess -and (Store-SecureCredential "Firebase_ClientEmail" $clientEmail)
$allSuccess = $allSuccess -and (Store-SecureCredential "Firebase_PrivateKey" $privateKey)

# ============================================================
# Database Encryption Key
# ============================================================

Write-Host ""
Write-Host "Generating database encryption key..." -ForegroundColor Yellow
$keyBytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($keyBytes)
$dbKey = [Convert]::ToBase64String($keyBytes)
$allSuccess = $allSuccess -and (Store-SecureCredential "Database_EncryptionKey" $dbKey)

# ============================================================
# Environment Variables (Non-Sensitive)
# ============================================================

Write-Host ""
Write-Host "Setting environment variables..." -ForegroundColor Yellow

try {
    $scope = if ($isAdmin) { "Machine" } else { "User" }
    [Environment]::SetEnvironmentVariable("MONBUREAU_FIREBASE_PROJECT_ID", $projectId, $scope)
    [Environment]::SetEnvironmentVariable("MONBUREAU_FIREBASE_CLIENT_EMAIL", $clientEmail, $scope)
    Write-Success "Environment variables set ($scope scope)"
} catch {
    Write-Warning "Failed to set environment variables (non-critical)"
    Write-Host "    $($_.Exception.Message)" -ForegroundColor Gray
}

# ============================================================
# Summary
# ============================================================

Write-Host ""
Write-Header "Setup Complete"

if ($allSuccess) {
    Write-Success "All credentials stored securely"
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Restart MonBureau application"
    Write-Host "  2. Test license activation"
    Write-Host "  3. Verify Firebase connectivity"
    Write-Host ""
    exit 0
} else {
    Write-Host ""
    Write-Error "Setup completed with errors"
    exit 1
}
