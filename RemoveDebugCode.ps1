# ============================================================
# MonBureau - Remove Debug Code for Production
# Removes all Debug.WriteLine statements and comments
# ============================================================

param(
    [string]$ProjectRoot = ".",
    [switch]$DryRun = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MonBureau Debug Code Cleanup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "⚠️  DRY RUN MODE - No files will be modified" -ForegroundColor Yellow
    Write-Host ""
}

# Patterns to remove
$patterns = @{
    # Debug.WriteLine statements
    "Debug.WriteLine" = @{
        Pattern = '^\s*System\.Diagnostics\.Debug\.WriteLine\([^)]*\);\s*$'
        Description = "Debug.WriteLine statements"
    }
    
    # Debug-only code blocks
    "DebugBlock" = @{
        Pattern = '(?s)#if DEBUG.*?#endif'
        Description = "DEBUG conditional blocks"
    }
    
    # Excessive comments (optional - review before enabling)
    # "Comments" = @{
    #     Pattern = '^\s*//.*DEBUG.*$'
    #     Description = "DEBUG comments"
    # }
}

# File extensions to process
$extensions = @("*.cs", "*.xaml.cs")

# Directories to exclude
$excludeDirs = @("bin", "obj", ".vs", "packages", "Tools")

# Statistics
$stats = @{
    FilesProcessed = 0
    FilesModified = 0
    LinesRemoved = 0
    BytesSaved = 0
}

function Remove-DebugCode {
    param(
        [string]$FilePath
    )
    
    $content = Get-Content -Path $FilePath -Raw -Encoding UTF8
    $originalSize = $content.Length
    $originalLines = ($content -split "`n").Count
    $modified = $false
    
    foreach ($patternName in $patterns.Keys) {
        $pattern = $patterns[$patternName].Pattern
        
        if ($content -match $pattern) {
            $matches = [regex]::Matches($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
            
            if ($matches.Count -gt 0) {
                Write-Host "  Found $($matches.Count) $($patterns[$patternName].Description) in:" -ForegroundColor Yellow
                Write-Host "    $($FilePath.Replace($ProjectRoot, ''))" -ForegroundColor Gray
                
                foreach ($match in $matches) {
                    if ($Verbose) {
                        $preview = $match.Value.Substring(0, [Math]::Min(80, $match.Value.Length))
                        Write-Host "      - $preview..." -ForegroundColor DarkGray
                    }
                }
                
                $content = $content -replace $pattern, ''
                $modified = $true
            }
        }
    }
    
    if ($modified) {
        # Remove consecutive empty lines (cleanup)
        $content = $content -replace '(\r?\n){3,}', "`n`n"
        
        $newSize = $content.Length
        $newLines = ($content -split "`n").Count
        
        $stats.FilesModified++
        $stats.LinesRemoved += ($originalLines - $newLines)
        $stats.BytesSaved += ($originalSize - $newSize)
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -Encoding UTF8 -NoNewline
            Write-Host "    ✓ Cleaned: $($originalLines - $newLines) lines removed, $([Math]::Round(($originalSize - $newSize) / 1024, 2)) KB saved" -ForegroundColor Green
        } else {
            Write-Host "    [DRY RUN] Would remove: $($originalLines - $newLines) lines, $([Math]::Round(($originalSize - $newSize) / 1024, 2)) KB" -ForegroundColor Yellow
        }
    }
}

# Find all C# files
Write-Host "Scanning project files..." -ForegroundColor Yellow
Write-Host ""

$files = Get-ChildItem -Path $ProjectRoot -Recurse -Include $extensions |
    Where-Object { 
        $exclude = $false
        foreach ($dir in $excludeDirs) {
            if ($_.FullName -like "*\$dir\*") {
                $exclude = $true
                break
            }
        }
        -not $exclude
    }

Write-Host "Found $($files.Count) files to process" -ForegroundColor Cyan
Write-Host ""

foreach ($file in $files) {
    $stats.FilesProcessed++
    Remove-DebugCode -FilePath $file.FullName
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Cleanup Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Files Processed:  $($stats.FilesProcessed)" -ForegroundColor White
Write-Host "Files Modified:   $($stats.FilesModified)" -ForegroundColor $(if ($stats.FilesModified -gt 0) { "Green" } else { "Gray" })
Write-Host "Lines Removed:    $($stats.LinesRemoved)" -ForegroundColor $(if ($stats.LinesRemoved -gt 0) { "Green" } else { "Gray" })
Write-Host "Space Saved:      $([Math]::Round($stats.BytesSaved / 1024, 2)) KB" -ForegroundColor $(if ($stats.BytesSaved -gt 0) { "Green" } else { "Gray" })
Write-Host ""

if ($DryRun) {
    Write-Host "This was a DRY RUN - no files were modified" -ForegroundColor Yellow
    Write-Host "Run without -DryRun to apply changes" -ForegroundColor Yellow
} else {
    Write-Host "✓ Cleanup complete!" -ForegroundColor Green
}

Write-Host ""

# Create backup recommendation
if (-not $DryRun -and $stats.FilesModified -gt 0) {
    Write-Host "⚠️  IMPORTANT: Test your application thoroughly after cleanup" -ForegroundColor Yellow
    Write-Host "   Some debug code may have been functional (error handling, etc.)" -ForegroundColor Yellow
    Write-Host ""
}

exit 0