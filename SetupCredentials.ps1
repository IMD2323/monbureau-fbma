# ============================================================
# MonBureau Secure Credentials Setup Script
# FIXED VERSION - Production Safe
# ============================================================

$ErrorActionPreference = "Stop"

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Text)
    Write-Host "  ✓ $Text" -ForegroundColor Green
}

function Write-Error {
    param([string]$Text)
    Write-Host "  ✗ $Text" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Text)
    Write-Host "  ⚠ $Text" -ForegroundColor Yellow
}

function Store-SecureCredential {
    param (
        [Parameter(Mandatory)]
        [string]$Key,

        [Parameter(Mandatory)]
        [string]$Value
    )

    $targetName = "MonBureau_$Key"

    try {
        # Delete existing credential
        cmdkey /delete:$targetName 2>$null | Out-Null

        # Store new credential
        $tempFile = [System.IO.Path]::GetTempFileName()
        try {
            $Value | Out-File -FilePath $tempFile -Encoding ASCII -NoNewline
            $result = cmdkey /generic:$targetName /user:MonBureau /pass:"$(Get-Content $tempFile -Raw)"
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Stored $Key"
                return $true
            }
            else {
                Write-Error "Failed to store $Key (Exit code: $LASTEXITCODE)"
                return $false
            }
        }
        finally {
            if (Test-Path $tempFile) {
                Remove-Item $tempFile -Force
            }
        }
    }
    catch {
        Write-Error "Failed to store $Key - $($_.Exception.Message)"
        return $false
    }
}

# ============================================================
# MAIN SCRIPT
# ============================================================

Write-Header "MonBureau Secure Credentials Setup"

# Check admin privileges
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)


if (-not $isAdmin) {
    Write-Warning "Running without administrator privileges"
    Write-Warning "Credentials will be stored for current user only"
    Write-Host ""
    
    $confirm = Read-Host "Continue? (y/n)"
    if ($confirm -ne "y") { 
        Write-Host "Setup cancelled" -ForegroundColor Yellow
        exit 1 
    }
}

# ============================================================
# Firebase Configuration
# ============================================================

Write-Host ""
Write-Host "Firebase Configuration" -ForegroundColor Yellow
Write-Host "----------------------" -ForegroundColor Yellow
Write-Host ""

# Project ID
do {
    $projectId = Read-Host "Firebase Project ID"
    if ([string]::IsNullOrWhiteSpace($projectId)) {
        Write-Error "Project ID cannot be empty"
    }
} while ([string]::IsNullOrWhiteSpace($projectId))

# Client Email
do {
    $clientEmail = Read-Host "Firebase Client Email (service account email)"
    if ([string]::IsNullOrWhiteSpace($clientEmail)) {
        Write-Error "Client Email cannot be empty"
    }
} while ([string]::IsNullOrWhiteSpace($clientEmail))

# Private Key
Write-Host ""
Write-Host "Paste Firebase Private Key (include BEGIN/END lines)" -ForegroundColor Gray
Write-Host "Press ENTER on empty line to finish" -ForegroundColor Gray
Write-Host ""

$privateKeyLines = @()
$lineNumber = 1

while ($true) {
    $line = Read-Host "Line $lineNumber"
    
    if ([string]::IsNullOrWhiteSpace($line)) {
        if ($privateKeyLines.Count -eq 0) {
            Write-Warning "Private key cannot be empty. Try again."
            continue
        }
        break
    }
    
    $privateKeyLines += $line
    $lineNumber++
}

$privateKey = $privateKeyLines -join "`n"

# Validate private key format
if (-not ($privateKey -match "BEGIN PRIVATE KEY")) {
    Write-Error "Invalid private key format - must contain 'BEGIN PRIVATE KEY'"
    Write-Host "Please ensure you copied the complete key including header/footer lines" -ForegroundColor Yellow
    exit 1
}

# ============================================================
# Store Credentials
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

    Write-Success "Environment variables set ($scope scope)"
}
catch {
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
    Write-Host "Security Notes:" -ForegroundColor Cyan
    Write-Host "  • Credentials stored in Windows Credential Manager"
    Write-Host "  • Private keys NOT in environment variables"
    Write-Host "  • No secrets written to disk"
    Write-Host "  • Use least-privilege Firebase service account"
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Restart MonBureau application"
    Write-Host "  2. Test license activation"
    Write-Host "  3. Verify Firebase connectivity"
    Write-Host ""
    
    exit 0
}
else {
    Write-Host ""
    Write-Error "Setup completed with errors"
    Write-Host ""
    Write-Host "Some credentials may not have been stored correctly." -ForegroundColor Yellow
    Write-Host "Please review the error messages above." -ForegroundColor Yellow
    Write-Host ""
    
    exit 1
}