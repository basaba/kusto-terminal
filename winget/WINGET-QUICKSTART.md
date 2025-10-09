# Winget Quick Start Guide

This is a quick reference for publishing Kusto Terminal to Windows Package Manager (winget).

## One-Time Setup

1. **Fork the winget-pkgs repository**:
   - Go to https://github.com/microsoft/winget-pkgs
   - Click "Fork" to create your own copy

2. **Create a GitHub Personal Access Token** (for automated submission):
   - Go to GitHub Settings → Developer settings → Personal access tokens
   - Generate new token with `public_repo` scope
   - Save the token securely

## For Each Release

### 1. Build and Test Locally

```bash
# Run the build script to calculate SHA256 hashes
./build-winget.sh

# This will:
# - Download Windows release files if needed
# - Calculate SHA256 hashes
# - Update the installer manifest
```

### 2. Update Version Information

Edit these files if releasing a new version:

- `winget/manifests/basaba.KustoTerminal.yaml` - Update `PackageVersion`
- `winget/manifests/basaba.KustoTerminal.installer.yaml` - Update `PackageVersion`, `ReleaseDate`, and URLs
- `winget/manifests/basaba.KustoTerminal.locale.en-US.yaml` - Update `PackageVersion` and release notes

### 3. Validate Manifests

On Windows with winget installed:

```powershell
winget validate --manifest winget/manifests/
```

### 4. Submit to Winget

#### Option A: Automated Submission (Recommended)

```powershell
winget submit --token YOUR_GITHUB_TOKEN winget/manifests/
```

#### Option B: Manual Pull Request

```bash
# In your fork of winget-pkgs
git checkout -b basaba.KustoTerminal-VERSION

# Copy manifests to the correct location
mkdir -p manifests/b/basaba/KustoTerminal/VERSION/
cp winget/manifests/*.yaml manifests/b/basaba/KustoTerminal/VERSION/

# Commit and push
git add manifests/b/basaba/KustoTerminal/VERSION/
git commit -m "Add basaba.KustoTerminal version VERSION"
git push origin basaba.KustoTerminal-VERSION

# Create PR on GitHub
```

### 5. Wait for Review

- Automated checks will run on your PR
- Microsoft reviewers will approve if all checks pass
- Usually takes 1-3 days

## Common Commands

```powershell
# Search for package
winget search "Kusto Terminal"

# Install package
winget install basaba.KustoTerminal

# Show package info
winget show basaba.KustoTerminal

# Upgrade package
winget upgrade basaba.KustoTerminal
```

## Checklist

Before submitting:

- [ ] Windows release files are published on GitHub
- [ ] SHA256 hashes are calculated and updated
- [ ] Version numbers match in all three manifest files
- [ ] URLs point to the correct release version
- [ ] Release date is updated
- [ ] Manifests validate without errors
- [ ] Test installation works locally (if possible)

## Need Help?

See the full guide in `winget/WINGET-README.md` for detailed information and troubleshooting.
