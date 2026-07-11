# PowerShell script to build and publish PhotoSorter as single-file EXE

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish",
    [string]$Runtime = "win-x64",
    [switch]$SignExe,
    [string]$CertPath = "",
    [string]$CertPassword = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

Write-Host "=== PhotoSorter Build & Publish ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Runtime: $Runtime"
Write-Host "Output: $OutputDir"

# Clean previous build
if (Test-Path $OutputDir) {
    Write-Host "Cleaning previous output..." -ForegroundColor Yellow
    Remove-Item $OutputDir -Recurse -Force
}

# Restore
Write-Host "Restoring packages..." -ForegroundColor Cyan
dotnet restore

# Build
Write-Host "Building..." -ForegroundColor Cyan
dotnet build -c $Configuration --no-restore

# Publish single-file
Write-Host "Publishing single-file EXE..." -ForegroundColor Cyan
dotnet publish -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:PublishTrimmed=false `
    -o $OutputDir

$exePath = Join-Path $OutputDir "PhotoSorter.exe"

if (Test-Path $exePath) {
    $sizeMB = (Get-Item $exePath).Length / 1MB
    Write-Host "✅ Build successful!" -ForegroundColor Green
    Write-Host "EXE: $exePath ($([math]::Round($sizeMB, 1)) MB)" -ForegroundColor Green
    
    # Sign if requested
    if ($SignExe -and $CertPath) {
        Write-Host "Signing EXE..." -ForegroundColor Cyan
        
        $signTool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
        if (-not (Test-Path $signTool)) {
            $signTool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "signtool.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
        }
        
        if (Test-Path $signTool) {
            $signArgs = @(
                "sign",
                "/f", $CertPath,
                "/p", $CertPassword,
                "/tr", $TimestampUrl,
                "/td", "sha256",
                "/fd", "sha256",
                "/v", $exePath
            )
            
            & $signTool @signArgs
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Signing successful!" -ForegroundColor Green
                
                # Verify signature
                & $signTool verify /v /pa $exePath
            } else {
                Write-Host "❌ Signing failed!" -ForegroundColor Red
            }
        } else {
            Write-Host "⚠️ signtool.exe not found. Install Windows 11 SDK." -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "❌ Build failed - EXE not found" -ForegroundColor Red
    exit 1
}

Write-Host "`nDone!" -ForegroundColor Cyan