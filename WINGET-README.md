# Publishing to WinGet

This guide explains how to publish KustoTerminal to the Windows Package Manager (WinGet).

## Overview

WinGet is Microsoft's official package manager for Windows. Publishing to WinGet allows users to install your application using:

```powershell
winget install basaba.KustoTerminal
```

## Prerequisites

1. **WinGet CLI** - Install from the Microsoft Store or https://aka.ms/getwinget
2. **GitHub Account** - Required for submitting packages
3. **Release Assets** - Your app must be available as a GitHub release

## Project Structure

```
winget/
├── basaba.KustoTerminal.yaml              # Version manifest
├── basaba.KustoTerminal.installer.yaml    # Installer configuration
└── basaba.KustoTerminal.locale.en-US.yaml # Package metadata
```

## Build and Prepare Manifests

Run the build script to generate the release package and update manifests:

```powershell
# Build and prepare manifests for version 0.1.0
.\build-winget.ps1 -Version "0.1.0"

# Skip build if you already have the release ZIP
.\build-winget.ps1 -Version "0.1.0" -SkipBuild
```

The script will:
1. Build the application (if not skipped)
2. Create the release ZIP file
3. Calculate SHA256 checksum
4. Update all manifest files with the correct version and checksum
5. Validate manifests (if WinGet CLI is available)

## Publishing Process

### 1. Create GitHub Release

1. Go to your GitHub repository releases page
2. Click "Draft a new release"
3. Create a new tag: `v0.1.0` (or your version)
4. Upload `KustoTerminal-win-x64.zip` as a release asset
5. Publish the release

### 2. Test Locally

Before submitting, test the installation locally:

```powershell
# Test installation from local manifests
winget install --manifest winget

# If successful, uninstall
winget uninstall basaba.KustoTerminal
```

### 3. Submit to WinGet

#### Option A: Using WinGet CLI (Recommended)

If you have the WinGet CLI with the `submit` command:

```powershell
winget submit --manifest winget --token <YOUR_GITHUB_TOKEN>
```

#### Option B: Manual Submission

1. **Fork the winget-pkgs repository**
   ```
   https://github.com/microsoft/winget-pkgs
   ```

2. **Clone your fork**
   ```powershell
   git clone https://github.com/YOUR_USERNAME/winget-pkgs.git
   cd winget-pkgs
   ```

3. **Create a branch for your package**
   ```powershell
   git checkout -b basaba.KustoTerminal-0.1.0
   ```

4. **Copy manifest files**
   ```powershell
   # Create directory structure (first letter/publisher/package/version)
   mkdir -p manifests\b\basaba\KustoTerminal\0.1.0
   
   # Copy all manifest files
   copy ..\kusto-terminal\winget\*.yaml manifests\b\basaba\KustoTerminal\0.1.0\
   ```

5. **Commit and push**
   ```powershell
   git add .
   git commit -m "New Package: basaba.KustoTerminal version 0.1.0"
   git push origin basaba.KustoTerminal-0.1.0
   ```

6. **Create Pull Request**
   - Go to the winget-pkgs repository on GitHub
   - Create a Pull Request from your branch
   - Wait for automated validation to complete
   - Address any review comments from maintainers

## Updating to a New Version

When releasing a new version:

1. **Update the version** in the build script call:
   ```powershell
   .\build-winget.ps1 -Version "0.2.0"
   ```

2. **Create a new GitHub release** with the new tag (e.g., `v0.2.0`)

3. **Submit to WinGet** following the same process:
   - Create a new directory: `manifests\b\basaba\KustoTerminal\0.2.0\`
   - Copy the updated manifest files
   - Submit PR: "Update basaba.KustoTerminal to 0.2.0"

## Manifest File Details

### basaba.KustoTerminal.yaml (Version Manifest)
- Specifies the package version
- Links to the default locale
- Required by WinGet's multi-file manifest format

### basaba.KustoTerminal.installer.yaml (Installer Manifest)
- Defines installation parameters
- Specifies the download URL and checksum
- Configures the installer type (ZIP with portable executable)
- Sets the portable command alias (`kustoterminal`)

### basaba.KustoTerminal.locale.en-US.yaml (Locale Manifest)
- Contains package metadata (name, description, tags)
- Publisher information
- License and documentation URLs

## Validation

The WinGet team validates all submissions:
- Manifest format and schema compliance
- URL accessibility
- SHA256 checksum verification
- Installer functionality
- Package metadata accuracy

## Common Issues

### Checksum Mismatch
**Problem**: The SHA256 checksum in the manifest doesn't match the file.
**Solution**: Run `build-winget.ps1` again to recalculate the checksum.

### URL Not Accessible
**Problem**: The installer URL returns 404.
**Solution**: Ensure the GitHub release is published (not draft) and the asset is uploaded.

### Validation Errors
**Problem**: Automated tests fail on the PR.
**Solution**: Check the CI logs and run local validation:
```powershell
winget validate winget
```

## Resources

- [WinGet Package Repository](https://github.com/microsoft/winget-pkgs)
- [WinGet Manifest Schema](https://aka.ms/winget-manifest-schema)
- [WinGet Documentation](https://docs.microsoft.com/windows/package-manager/)
- [WinGet CLI](https://github.com/microsoft/winget-cli)

## Support

For issues with:
- **KustoTerminal package**: Open an issue in this repository
- **WinGet submission**: Comment on your Pull Request in winget-pkgs
- **WinGet CLI**: Open an issue in the [winget-cli repository](https://github.com/microsoft/winget-cli/issues)
