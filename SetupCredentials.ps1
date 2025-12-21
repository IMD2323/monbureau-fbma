# ============================================================================
# MonBureau Firebase Web SDK Credentials Setup
# ============================================================================
# 
# This script securely stores Firebase Web SDK credentials in Windows 
# Credential Manager.
#
# Required credentials:
# 1. Firebase API Key (from Firebase Console > Project Settings)
# 2. Firebase Project ID
# 3. (Optional) Firebase Database URL
#
# ============================================================================

param(
    [switch]$Verify,
    [switch]$Delete,
    [switch]$Show
)

# Require Administrator privileges
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "❌ This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    pause
    exit 1
}

# Credential keys
$CREDENTIALS = @{
    "Firebase_ApiKey" = "Firebase API Key"
    "Firebase_ProjectId" = "Firebase Project ID"
    "Firebase_DatabaseUrl" = "Firebase Database URL (optional)"
}

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor White
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Store-Credential {
    param(
        [string]$Key,
        [string]$Username,
        [string]$Password
    )
    
    try {
        $target = "MonBureau_$Key"
        
        # Use cmdkey to store credential
        $process = Start-Process -FilePath "cmdkey.exe" `
            -ArgumentList "/generic:$target /user:$Username /pass:`"$Password`"" `
            -Wait -PassThru -WindowStyle Hidden
        
        return $process.ExitCode -eq 0
    }
    catch {
        Write-Host "Error storing credential: $_" -ForegroundColor Red
        return $false
    }
}

function Get-Credential {
    param([string]$Key)
    
    try {
        $target = "MonBureau_$Key"
        
        Add-Type -AssemblyName System.Security
        
        $credPtr = [IntPtr]::Zero
        $success = [CredentialManager.CredRead]::CredRead($target, 1, 0, [ref]$credPtr)
        
        if ($success) {
            $cred = [System.Runtime.InteropServices.Marshal]::PtrToStructure($credPtr, [type][CredentialManager.Credential])
            $password = [System.Runtime.InteropServices.Marshal]::PtrToStringUni($cred.CredentialBlob, $cred.CredentialBlobSize / 2)
            [CredentialManager.CredFree]::CredFree($credPtr)
            return $password
        }
        
        return $null
    }
    catch {
        return $null
    }
}

function Test-Credential {
    param([string]$Key)
    
    try {
        $target = "MonBureau_$Key"
        $output = & cmdkey /list:$target 2>&1
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

function Remove-Credential {
    param([string]$Key)
    
    try {
        $target = "MonBureau_$Key"
        $process = Start-Process -FilePath "cmdkey.exe" `
            -ArgumentList "/delete:$target" `
            -Wait -PassThru -WindowStyle Hidden
        
        return $process.ExitCode -eq 0
    }
    catch {
        return $false
    }
}

# ============================================================================
# Main Operations
# ============================================================================

function Show-Credentials {
    Write-Header "Current Firebase Web SDK Credentials"
    
    $found = $false
    foreach ($key in $CREDENTIALS.Keys) {
        $exists = Test-Credential -Key $key
        $status = if ($exists) { "✓ Configured" } else { "✗ Not configured" }
        $color = if ($exists) { "Green" } else { "Yellow" }
        
        Write-Host "$($CREDENTIALS[$key]): " -NoNewline
        Write-Host $status -ForegroundColor $color
        
        if ($exists) { $found = $true }
    }
    
    if (-not $found) {
        Write-Host ""
        Write-Host "No credentials configured. Run without -Show to configure." -ForegroundColor Yellow
    }
    
    Write-Host ""
}

function Delete-Credentials {
    Write-Header "Delete Firebase Web SDK Credentials"
    
    Write-Host "⚠️  WARNING: This will delete all stored Firebase credentials!" -ForegroundColor Yellow
    Write-Host ""
    
    $confirm = Read-Host "Type 'DELETE' to confirm"
    
    if ($confirm -ne "DELETE") {
        Write-Host "Cancelled." -ForegroundColor Yellow
        return
    }
    
    Write-Host ""
    $deleted = 0
    
    foreach ($key in $CREDENTIALS.Keys) {
        if (Test-Credential -Key $key) {
            if (Remove-Credential -Key $key) {
                Write-Host "✓ Deleted: $($CREDENTIALS[$key])" -ForegroundColor Green
                $deleted++
            } else {
                Write-Host "✗ Failed to delete: $($CREDENTIALS[$key])" -ForegroundColor Red
            }
        }
    }
    
    Write-Host ""
    if ($deleted -gt 0) {
        Write-Host "✓ Deleted $deleted credential(s)" -ForegroundColor Green
    } else {
        Write-Host "No credentials to delete" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

function Verify-Credentials {
    Write-Header "Verify Firebase Web SDK Credentials"
    
    $allValid = $true
    $required = @("Firebase_ApiKey", "Firebase_ProjectId")
    
    foreach ($key in $required) {
        $exists = Test-Credential -Key $key
        
        if ($exists) {
            Write-Host "✓ $($CREDENTIALS[$key]): Configured" -ForegroundColor Green
        } else {
            Write-Host "✗ $($CREDENTIALS[$key]): Missing (REQUIRED)" -ForegroundColor Red
            $allValid = $false
        }
    }
    
    # Optional credential
    $dbUrlExists = Test-Credential -Key "Firebase_DatabaseUrl"
    if ($dbUrlExists) {
        Write-Host "✓ Firebase Database URL: Configured" -ForegroundColor Green
    } else {
        Write-Host "○ Firebase Database URL: Not configured (optional)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    
    if ($allValid) {
        Write-Host "✓ All required credentials are configured!" -ForegroundColor Green
        Write-Host ""
        Write-Host "You can now run MonBureau with Firebase support." -ForegroundColor Cyan
    } else {
        Write-Host "✗ Some required credentials are missing!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Run this script without -Verify to configure credentials." -ForegroundColor Yellow
    }
    
    Write-Host ""
}

function Setup-Credentials {
    Write-Header "Firebase Web SDK Credentials Setup"
    
    Write-Host "This script will securely store your Firebase Web SDK credentials" -ForegroundColor Cyan
    Write-Host "in Windows Credential Manager." -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To get your credentials:" -ForegroundColor Yellow
    Write-Host "1. Go to Firebase Console: https://console.firebase.google.com" -ForegroundColor White
    Write-Host "2. Select your project" -ForegroundColor White
    Write-Host "3. Go to Project Settings (gear icon)" -ForegroundColor White
    Write-Host "4. Scroll down to 'Your apps' section" -ForegroundColor White
    Write-Host "5. Find your Web App configuration" -ForegroundColor White
    Write-Host ""
    
    $continue = Read-Host "Continue? (Y/N)"
    if ($continue -ne "Y" -and $continue -ne "y") {
        Write-Host "Cancelled." -ForegroundColor Yellow
        return
    }
    
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host ""
    
    # Firebase API Key
    Write-Host "1. Firebase API Key" -ForegroundColor Yellow
    Write-Host "   (Example: AIzaSyBOTI5qB5xxxxxxxxxxxxxxxxxxxxxxxxx)" -ForegroundColor Gray
    Write-Host ""
    
    $apiKey = Read-Host "Enter Firebase API Key"
    
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        Write-Host "✗ API Key cannot be empty!" -ForegroundColor Red
        return
    }
    
    Write-Host ""
    
    # Firebase Project ID
    Write-Host "2. Firebase Project ID" -ForegroundColor Yellow
    Write-Host "   (Example: monbureau-licenses)" -ForegroundColor Gray
    Write-Host ""
    
    $projectId = Read-Host "Enter Firebase Project ID"
    
    if ([string]::IsNullOrWhiteSpace($projectId)) {
        Write-Host "✗ Project ID cannot be empty!" -ForegroundColor Red
        return
    }
    
    Write-Host ""
    
    # Firebase Database URL (Optional)
    Write-Host "3. Firebase Database URL (Optional)" -ForegroundColor Yellow
    Write-Host "   (Example: https://monbureau-licenses.firebaseio.com)" -ForegroundColor Gray
    Write-Host "   Press Enter to skip" -ForegroundColor Gray
    Write-Host ""
    
    $databaseUrl = Read-Host "Enter Firebase Database URL"
    
    # Default Database URL if not provided
    if ([string]::IsNullOrWhiteSpace($databaseUrl)) {
        $databaseUrl = "https://$projectId.firebaseio.com"
        Write-Host "   Using default: $databaseUrl" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Storing credentials..." -ForegroundColor Cyan
    Write-Host ""
    
    # Store API Key
    if (Store-Credential -Key "Firebase_ApiKey" -Username "FirebaseConfig" -Password $apiKey) {
        Write-Host "✓ Stored: Firebase API Key" -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to store API Key" -ForegroundColor Red
        return
    }
    
    # Store Project ID
    if (Store-Credential -Key "Firebase_ProjectId" -Username "FirebaseConfig" -Password $projectId) {
        Write-Host "✓ Stored: Firebase Project ID" -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to store Project ID" -ForegroundColor Red
        return
    }
    
    # Store Database URL
    if (Store-Credential -Key "Firebase_DatabaseUrl" -Username "FirebaseConfig" -Password $databaseUrl) {
        Write-Host "✓ Stored: Firebase Database URL" -ForegroundColor Green
    } else {
        Write-Host "⚠  Warning: Failed to store Database URL (optional)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "✓ Firebase Web SDK credentials configured successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Restart MonBureau application" -ForegroundColor White
    Write-Host "2. Firebase features will be automatically available" -ForegroundColor White
    Write-Host ""
    Write-Host "To verify: Run this script with -Verify flag" -ForegroundColor Cyan
    Write-Host ""
}

# ============================================================================
# Main Script Logic
# ============================================================================

# Add credential management types
Add-Type @"
using System;
using System.Runtime.InteropServices;

namespace CredentialManager {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct Credential {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }
    
    public class CredRead {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    }
    
    public class CredFree {
        [DllImport("advapi32.dll")]
        public static extern void CredFree(IntPtr cred);
    }
}
"@

# Execute based on flags
if ($Show) {
    Show-Credentials
}
elseif ($Delete) {
    Delete-Credentials
}
elseif ($Verify) {
    Verify-Credentials
}
else {
    Setup-Credentials
}

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")