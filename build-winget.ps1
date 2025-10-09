# Build and Package Script for WinGet
# This script builds the KustoTerminal application and prepares WinGet manifests

param(
    [string]$Version = "0.1.0",
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = 'Stop'

Write-Host "Building KustoTerminal v$Version for WinGet" -ForegroundColor Green

# Define paths
$projectPath = "src\KustoTerminal.CLI\KustoTerminal.CLI.csproj"
$publishDir = "publish\win-x64"
$wingetDir = "winget"
$releaseZip = "KustoTerminal-win-x64.zip"

# Step 1: Build the application
if (-not $SkipBuild) {
    Write-Host "`nStep 1: Building application..." -ForegroundColor Cyan
    
    # Clean previous builds
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }
    
    # Publish as single-file executable for win-x64
    # This creates a single EXE that's compatible with WinGet's portable installer
    Write-Host "Building as single-file executable for better WinGet compatibility..." -ForegroundColor Yellow
    dotnet publish $projectPath `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $publishDir `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:EnableCompressionInSingleFile=true
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "Build completed successfully!" -ForegroundColor Green
} else {
    Write-Host "`nStep 1: Skipping build (using existing files)" -ForegroundColor Yellow
}

# Step 2: Create release ZIP
Write-Host "`nStep 2: Creating release ZIP..." -ForegroundColor Cyan

if (Test-Path $releaseZip) {
    Remove-Item $releaseZip -Force
}

Compress-Archive -Path "$publishDir\*" -DestinationPath $releaseZip -CompressionLevel Optimal

Write-Host "Release ZIP created: $releaseZip" -ForegroundColor Green

# Step 3: Calculate checksum
Write-Host "`nStep 3: Calculating SHA256 checksum..." -ForegroundColor Cyan

$checksum = (Get-FileHash -Path $releaseZip -Algorithm SHA256).Hash
Write-Host "SHA256: $checksum" -ForegroundColor Yellow

# Step 4: Update WinGet manifests with version and checksum
Write-Host "`nStep 4: Updating WinGet manifest files..." -ForegroundColor Cyan

# Update version manifest
$versionManifest = Join-Path $wingetDir "basaba.KustoTerminal.yaml"
$content = Get-Content $versionManifest -Raw
$content = $content -replace 'PackageVersion: [\d\.]+', "PackageVersion: $Version"
Set-Content -Path $versionManifest -Value $content

# Update installer manifest
$installerManifest = Join-Path $wingetDir "basaba.KustoTerminal.installer.yaml"
$content = Get-Content $installerManifest -Raw
$content = $content -replace 'PackageVersion: [\d\.]+', "PackageVersion: $Version"
$content = $content -replace 'v[\d\.]+/', "v$Version/"
$content = $content -replace 'InstallerSha256: .+', "InstallerSha256: $checksum"
Set-Content -Path $installerManifest -Value $content

# Update locale manifest
$localeManifest = Join-Path $wingetDir "basaba.KustoTerminal.locale.en-US.yaml"
$content = Get-Content $localeManifest -Raw
$content = $content -replace 'PackageVersion: [\d\.]+', "PackageVersion: $Version"
Set-Content -Path $localeManifest -Value $content

Write-Host "Updated WinGet manifests to version $Version" -ForegroundColor Green

# Step 5: Validate manifests (if winget is installed)
Write-Host "`nStep 5: Validating WinGet manifests..." -ForegroundColor Cyan

$wingetCmd = Get-Command winget -ErrorAction SilentlyContinue
if ($wingetCmd) {
    try {
        winget validate $wingetDir
        Write-Host "WinGet manifests validated successfully!" -ForegroundColor Green
    } catch {
        Write-Host "Warning: Manifest validation failed. Please check the manifests manually." -ForegroundColor Yellow
    }
} else {
    Write-Host "WinGet CLI not found. Skipping validation. Install from: https://aka.ms/getwinget" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Upload $releaseZip to GitHub Releases (tag: v$Version)" -ForegroundColor White
Write-Host "2. Test installation locally:" -ForegroundColor White
Write-Host "   winget install --manifest $wingetDir" -ForegroundColor Gray
Write-Host "3. Fork the winget-pkgs repository:" -ForegroundColor White
Write-Host "   https://github.com/microsoft/winget-pkgs" -ForegroundColor Gray
Write-Host "4. Copy the manifest files to:" -ForegroundColor White
Write-Host "   manifests/b/basaba/KustoTerminal/$Version/" -ForegroundColor Gray
Write-Host "5. Submit a Pull Request to winget-pkgs" -ForegroundColor White
Write-Host "`nManifest files are ready in: $wingetDir" -ForegroundColor Cyan
Write-Host "Checksum for reference:" -ForegroundColor Cyan
Write-Host $checksum -ForegroundColor Yellow
