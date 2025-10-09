# Chocolatey Distribution Guide for KustoTerminal

This guide explains how to build, package, and distribute KustoTerminal via Chocolatey.

## Prerequisites

1. **Chocolatey** installed on your system:
   ```powershell
   Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
   ```

2. **.NET 8.0 SDK** installed:
   ```powershell
   choco install dotnet-sdk -y
   ```

3. **Chocolatey API Key** (for publishing):
   - Get your API key from: https://community.chocolatey.org/account
   - Set it: `choco apikey --key YOUR-API-KEY --source https://push.chocolatey.org/`

## Project Structure

```
kusto-terminal/
├── choco/
│   ├── kustoterminal.nuspec          # Package metadata
│   └── tools/
│       ├── chocolateyinstall.ps1     # Installation script
│       ├── chocolateyuninstall.ps1   # Uninstallation script
│       └── VERIFICATION.txt          # Verification info
├── build-choco.ps1                   # Build and package script
└── CHOCOLATEY-README.md              # This file
```

## Building and Packaging

### Step 1: Build the Release

Run the build script to create the package:

```powershell
.\build-choco.ps1
```

Or with a specific version:

```powershell
.\build-choco.ps1 -Version "0.2.0"
```

This script will:
1. Build the application for win-x64
2. Create a release ZIP file
3. Calculate the SHA256 checksum
4. Update the nuspec version
5. Create the Chocolatey package (.nupkg)

### Step 2: Create GitHub Release

1. Go to your GitHub repository: https://github.com/basaba/kusto-terminal
2. Create a new release with tag `v0.1.0`
3. Upload the `KustoTerminal-win-x64.zip` file
4. Note the download URL (it will be: `https://github.com/basaba/kusto-terminal/releases/download/v0.1.0/KustoTerminal-win-x64.zip`)

### Step 3: Update Installation Script

Update `choco/tools/chocolateyinstall.ps1`:

1. Replace the `$url64` with your actual GitHub release URL
2. Replace `CHECKSUM_PLACEHOLDER` with the SHA256 checksum from the build script output

Example:
```powershell
$url64 = 'https://github.com/basaba/kusto-terminal/releases/download/v0.1.0/KustoTerminal-win-x64.zip'
checksum64    = 'abc123...'  # Use the actual checksum from build output
```

### Step 4: Rebuild the Package

After updating the installation script, rebuild the package:

```powershell
cd choco
choco pack
```

## Testing Locally

Test the package locally before publishing:

```powershell
# Install from local source
choco install kustoterminal -s choco -y

# Test the application
kustoterminal

# Uninstall
choco uninstall kustoterminal -y
```

## Publishing to Chocolatey

### First-time Publishing

1. **Create a Chocolatey account**: https://community.chocolatey.org/account/Register

2. **Get your API key**: https://community.chocolatey.org/account

3. **Set the API key**:
   ```powershell
   choco apikey --key YOUR-API-KEY --source https://push.chocolatey.org/
   ```

4. **Push the package**:
   ```powershell
   choco push choco\kustoterminal.0.1.0.nupkg --source https://push.chocolatey.org/
   ```

### Package Review Process

- Your package will be reviewed by Chocolatey moderators
- Initial review typically takes 1-3 days
- You'll receive feedback via email
- After approval, updates are usually faster (automated if tests pass)

### Updating Existing Package

For subsequent releases:

1. Update version in `choco/kustoterminal.nuspec`
2. Run `build-choco.ps1 -Version "X.Y.Z"`
3. Create GitHub release with new version
4. Update checksums in `chocolateyinstall.ps1`
5. Rebuild and push: `choco push choco\kustoterminal.X.Y.Z.nupkg --source https://push.chocolatey.org/`

## Package Metadata

Key fields in `kustoterminal.nuspec`:

- **id**: `kustoterminal` (package identifier)
- **version**: Must follow semantic versioning (e.g., 0.1.0)
- **tags**: Space-separated keywords for discovery
- **dependencies**: Automatically installs .NET 8.0 runtime if needed

## Installation for End Users

Once published, users can install with:

```powershell
choco install kustoterminal -y
```

Or upgrade:

```powershell
choco upgrade kustoterminal -y
```

## Troubleshooting

### Build Fails

- Ensure .NET 8.0 SDK is installed: `dotnet --version`
- Check project builds normally: `dotnet build src/KustoTerminal.CLI/KustoTerminal.CLI.csproj`

### Package Validation Errors

- Run `choco pack --trace` for detailed output
- Ensure all required files exist in `choco/tools/`
- Verify nuspec XML is valid

### Installation Issues

- Check if .NET 8.0 runtime dependency is properly declared
- Verify the download URL is accessible
- Confirm checksum matches the ZIP file

## Best Practices

1. **Version Numbers**: Use semantic versioning (MAJOR.MINOR.PATCH)
2. **Release Notes**: Update for each version in GitHub releases
3. **Testing**: Always test locally before pushing to Chocolatey
4. **Checksums**: Never skip checksum verification
5. **Dependencies**: Keep runtime dependencies up to date
6. **Documentation**: Update README and release notes with each version

## Useful Links

- **Chocolatey Docs**: https://docs.chocolatey.org/en-us/create/create-packages
- **Package Guidelines**: https://docs.chocolatey.org/en-us/create/create-packages#package-guidelines
- **Your Package**: https://community.chocolatey.org/packages/kustoterminal (after publishing)
- **Validation**: https://docs.chocolatey.org/en-us/community-repository/moderation/package-validator

## Support

For issues with:
- **KustoTerminal**: https://github.com/basaba/kusto-terminal/issues
- **Chocolatey Package**: https://community.chocolatey.org/packages/kustoterminal/ContactOwners
- **Chocolatey itself**: https://github.com/chocolatey/choco/issues
