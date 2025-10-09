# Build and Package Script for Chocolatey
# This script builds the KustoTerminal application and creates a Chocolatey package

param(
    [string]$Version = "0.1.0",
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = 'Stop'

Write-Host "Building KustoTerminal v$Version for Chocolatey" -ForegroundColor Green

# Define paths
$projectPath = "src\KustoTerminal.CLI\KustoTerminal.CLI.csproj"
$publishDir = "publish\win-x64"
$chocoDir = "choco"
$releaseZip = "KustoTerminal-win-x64.zip"

# Step 1: Build the application
if (-not $SkipBuild) {
    Write-Host "`nStep 1: Building application..." -ForegroundColor Cyan
    
    # Clean previous builds
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }
    
    # Publish as self-contained for win-x64
    dotnet publish $projectPath `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $publishDir `
        /p:PublishSingleFile=false `
        /p:PublishTrimmed=false
    
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

# Step 4: Update nuspec version
Write-Host "`nStep 4: Updating nuspec version..." -ForegroundColor Cyan

$nuspecPath = Join-Path $chocoDir "kustoterminal.nuspec"
$nuspecContent = Get-Content $nuspecPath -Raw
$nuspecContent = $nuspecContent -replace '<version>[\d\.]+</version>', "<version>$Version</version>"
Set-Content -Path $nuspecPath -Value $nuspecContent

Write-Host "Updated nuspec to version $Version" -ForegroundColor Green

# Step 5: Pack Chocolatey package
Write-Host "`nStep 5: Creating Chocolatey package..." -ForegroundColor Cyan

Push-Location $chocoDir
try {
    choco pack
    if ($LASTEXITCODE -ne 0) {
        throw "Chocolatey pack failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Upload $releaseZip to GitHub Releases (tag: v$Version)" -ForegroundColor White
Write-Host "2. Update choco/tools/chocolateyinstall.ps1 with the actual download URL" -ForegroundColor White
Write-Host "3. Replace 'CHECKSUM_PLACEHOLDER' with: $checksum" -ForegroundColor Yellow
Write-Host "4. Test locally: choco install kustoterminal -s choco -y" -ForegroundColor White
Write-Host "5. Push to Chocolatey: choco push choco\kustoterminal.$Version.nupkg --source https://push.chocolatey.org/" -ForegroundColor White
Write-Host "`nChecksum for reference:" -ForegroundColor Cyan
Write-Host $checksum -ForegroundColor Yellow
