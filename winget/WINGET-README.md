# Winget Publishing Guide for Kusto Terminal

This guide explains how to publish Kusto Terminal to the Windows Package Manager (winget).

## Overview

Windows Package Manager (winget) is Microsoft's official package manager for Windows. Packages are published to the [winget-pkgs repository](https://github.com/microsoft/winget-pkgs) on GitHub.

## Prerequisites

1. **GitHub Account**: You need a GitHub account to submit packages
2. **Release Assets**: Windows release binaries (zip files) must be available on GitHub Releases
3. **SHA256 Hashes**: You need SHA256 checksums for your release artifacts

## Manifest Files

Winget uses three YAML manifest files (located in `winget/manifests/`):

1. **basaba.KustoTerminal.yaml** - Version manifest (links the other manifests)
2. **basaba.KustoTerminal.installer.yaml** - Installer details (URLs, checksums, architectures)
3. **basaba.KustoTerminal.locale.en-US.yaml** - Package metadata (description, tags, etc.)

## Publishing Process

### Step 1: Prepare Release Artifacts

Ensure your GitHub release includes:
- `KustoTerminal-win-x64.zip`
- `KustoTerminal-win-arm64.zip`

### Step 2: Update SHA256 Hashes

After creating a release, calculate SHA256 hashes for the Windows artifacts:

```bash
# Calculate SHA256 for x64
sha256sum KustoTerminal-win-x64.zip

# Calculate SHA256 for arm64
sha256sum KustoTerminal-win-arm64.zip
```

Or use the provided script:
```bash
./build-winget.sh
```

Update the `InstallerSha256` values in `basaba.KustoTerminal.installer.yaml`.

### Step 3: Update Version

When releasing a new version:

1. Update `PackageVersion` in all three manifest files
2. Update `ReleaseDate` in the installer manifest
3. Update `InstallerUrl` paths to point to the new version
4. Update `ReleaseNotesUrl` in the locale manifest

### Step 4: Validate Manifests

Install the winget client tools:

```powershell
# Windows PowerShell
winget install Microsoft.WingetCreate
```

Validate your manifests:

```powershell
winget validate --manifest winget/manifests/
```

### Step 5: Submit to winget-pkgs Repository

#### Option A: Using winget submit (Recommended)

```powershell
winget submit --token <GITHUB_PAT> winget/manifests/
```

#### Option B: Manual Pull Request

1. Fork the [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) repository

2. Create a new branch:
```bash
git checkout -b basaba.KustoTerminal-0.1.0
```

3. Copy your manifest files to the appropriate directory:
```bash
mkdir -p manifests/b/basaba/KustoTerminal/0.1.0/
cp winget/manifests/*.yaml manifests/b/basaba/KustoTerminal/0.1.0/
```

4. Commit and push:
```bash
git add manifests/b/basaba/KustoTerminal/0.1.0/
git commit -m "Add basaba.KustoTerminal version 0.1.0"
git push origin basaba.KustoTerminal-0.1.0
```

5. Create a Pull Request to the main winget-pkgs repository

6. Wait for automated validation and human review

## Testing Locally

Before submitting, test installation locally:

```powershell
# Install from local manifest
winget install --manifest winget/manifests/basaba.KustoTerminal.yaml
```

## After Publication

Once your PR is merged, users can install Kusto Terminal via:

```powershell
winget install basaba.KustoTerminal
```

Or search for it:

```powershell
winget search "Kusto Terminal"
```

## Updating Existing Package

For subsequent releases:

1. Update version numbers in all manifest files
2. Update SHA256 hashes for new release artifacts
3. Update release date and URLs
4. Follow the same submission process

The PR should target the same package directory structure but with the new version number.

## Resources

- [Windows Package Manager Documentation](https://docs.microsoft.com/windows/package-manager/)
- [Manifest Schema Reference](https://docs.microsoft.com/windows/package-manager/package/manifest)
- [Contributing Guide](https://github.com/microsoft/winget-pkgs/blob/master/CONTRIBUTING.md)
- [winget-pkgs Repository](https://github.com/microsoft/winget-pkgs)

## Troubleshooting

### Validation Errors

If validation fails:
- Ensure all URLs are accessible
- Verify SHA256 hashes match exactly
- Check YAML syntax (no tabs, proper indentation)
- Ensure all required fields are present

### PR Review Issues

Common issues that cause PR rejection:
- Incorrect SHA256 hashes
- URLs that return 404
- Missing or incorrect metadata
- YAML formatting problems

### Testing Installation

If local installation fails:
- Verify the installer URLs are correct
- Check that release artifacts are publicly accessible
- Ensure the ZIP structure is correct (binary should be at root or in predictable location)
