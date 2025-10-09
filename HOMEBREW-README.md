# Homebrew Distribution Guide for KustoTerminal

This guide explains how to build, package, and distribute KustoTerminal via Homebrew for macOS.

## Prerequisites

1. **Homebrew** installed on your system:
   ```bash
   /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
   ```

2. **.NET 8.0 SDK** installed:
   ```bash
   brew install dotnet
   ```

3. **GitHub CLI** (optional but recommended):
   ```bash
   brew install gh
   gh auth login
   ```

## Project Structure

```
kusto-terminal/
├── homebrew/
│   └── kustoterminal.rb              # Homebrew formula
├── build-homebrew.sh                 # Build script
├── HOMEBREW-README.md                # This file
└── releases/                         # Generated release artifacts
    ├── KustoTerminal-osx-x64.tar.gz
    └── KustoTerminal-osx-arm64.tar.gz
```

## Building and Packaging

### Step 1: Build the Release

Run the build script to create macOS binaries:

```bash
./build-homebrew.sh
```

Or with a specific version:

```bash
./build-homebrew.sh 0.2.0
```

This script will:
1. Build self-contained binaries for macOS x64 and ARM64
2. Create tar.gz archives for each architecture
3. Calculate SHA256 checksums
4. Display the checksums and next steps

### Step 2: Create GitHub Release

Create a new release on GitHub with the built artifacts:

**Option A: Using GitHub CLI (Recommended)**

```bash
gh release create v0.1.0 \
  --title "v0.1.0" \
  --notes "Release notes for v0.1.0" \
  releases/KustoTerminal-osx-x64.tar.gz \
  releases/KustoTerminal-osx-arm64.tar.gz
```

**Option B: Using GitHub Web Interface**

1. Go to https://github.com/basaba/kusto-terminal/releases/new
2. Create tag: `v0.1.0`
3. Set release title: `v0.1.0`
4. Add release notes describing changes
5. Upload both `.tar.gz` files from the `releases/` directory
6. Publish release

### Step 3: Update the Formula

Update `homebrew/kustoterminal.rb` with the information from the build output:

1. Update the `version` field
2. Replace `CHECKSUM_X64_PLACEHOLDER` with the x64 checksum
3. Replace `CHECKSUM_ARM64_PLACEHOLDER` with the ARM64 checksum
4. Update the version in the download URLs

Example:
```ruby
version "0.1.0"

on_macos do
  if Hardware::CPU.arm?
    url "https://github.com/basaba/kusto-terminal/releases/download/v0.1.0/KustoTerminal-osx-arm64.tar.gz"
    sha256 "abc123..."  # Use actual checksum from build output
  else
    url "https://github.com/basaba/kusto-terminal/releases/download/v0.1.0/KustoTerminal-osx-x64.tar.gz"
    sha256 "def456..."  # Use actual checksum from build output
  end
end
```

## Testing Locally

Before publishing, test the formula locally:

```bash
# Install from local formula
brew install --build-from-source homebrew/kustoterminal.rb

# Test the application
kustoterminal --version
kustoterminal

# Uninstall
brew uninstall kustoterminal
```

## Publishing to Homebrew

There are two main ways to distribute your formula:

### Option 1: Homebrew Tap (Recommended for Personal/Organizational Use)

A tap is a third-party repository for Homebrew formulas.

**Step 1: Create a Tap Repository**

Create a new GitHub repository named `homebrew-kustoterminal` (must start with `homebrew-`):

```bash
gh repo create basaba/homebrew-kustoterminal --public --description "Homebrew tap for KustoTerminal"
```

**Step 2: Add the Formula**

```bash
cd /tmp
git clone https://github.com/basaba/homebrew-kustoterminal.git
cd homebrew-kustoterminal

# Copy your formula
cp /path/to/kusto-terminal/homebrew/kustoterminal.rb Formula/kustoterminal.rb

# Or create Formula directory and add formula
mkdir -p Formula
cp /path/to/kusto-terminal/homebrew/kustoterminal.rb Formula/

git add Formula/kustoterminal.rb
git commit -m "Add kustoterminal formula v0.1.0"
git push origin main
```

**Step 3: Users Can Install**

Users can now install your app with:

```bash
# Add your tap
brew tap basaba/kustoterminal

# Install
brew install kustoterminal
```

Or in one command:

```bash
brew install basaba/kustoterminal/kustoterminal
```

**Step 4: Updating the Formula**

For new releases:

```bash
cd homebrew-kustoterminal
# Edit Formula/kustoterminal.rb with new version and checksums
git add Formula/kustoterminal.rb
git commit -m "Update kustoterminal to v0.2.0"
git push origin main
```

Users can upgrade with:

```bash
brew update
brew upgrade kustoterminal
```

### Option 2: Official Homebrew Core (For Popular Open-Source Projects)

For widely-used projects, you can submit to Homebrew's official repository.

**Requirements:**
- Project must be stable and well-maintained
- Must have a reasonable number of users
- Must be open source
- Must have documentation and tests

**Process:**

1. **Fork homebrew-core:**
   ```bash
   gh repo fork Homebrew/homebrew-core --clone
   cd homebrew-core
   ```

2. **Create a new branch:**
   ```bash
   git checkout -b kustoterminal
   ```

3. **Add your formula:**
   ```bash
   cp /path/to/kusto-terminal/homebrew/kustoterminal.rb Formula/kustoterminal.rb
   ```

4. **Test thoroughly:**
   ```bash
   brew install --build-from-source Formula/kustoterminal.rb
   brew test kustoterminal
   brew audit --strict kustoterminal
   ```

5. **Submit a pull request:**
   ```bash
   git add Formula/kustoterminal.rb
   git commit -m "kustoterminal 0.1.0 (new formula)"
   git push origin kustoterminal
   gh pr create --repo Homebrew/homebrew-core
   ```

6. **Wait for review** - Homebrew maintainers will review and may request changes

## Maintenance

### Updating for New Releases

1. Build new version:
   ```bash
   ./build-homebrew.sh 0.2.0
   ```

2. Create GitHub release with new artifacts

3. Update formula with new version and checksums

4. If using a tap, commit and push changes:
   ```bash
   git add Formula/kustoterminal.rb
   git commit -m "Update kustoterminal to v0.2.0"
   git push
   ```

5. If in homebrew-core, submit a new PR

### Formula Best Practices

1. **Version Numbers**: Use semantic versioning (MAJOR.MINOR.PATCH)
2. **Testing**: Always include a `test do` block that verifies the installation
3. **Dependencies**: Declare all runtime dependencies (your formula depends on macOS only)
4. **Architecture**: Support both x64 and ARM64 (Apple Silicon)
5. **Documentation**: Keep release notes updated

## Troubleshooting

### Build Fails

```bash
# Ensure .NET SDK is installed and correct version
dotnet --version  # Should show 8.0.x

# Clean and rebuild
rm -rf build/ releases/
./build-homebrew.sh
```

### Checksum Mismatch

If users report checksum errors:
1. Re-download the release file
2. Verify checksum: `shasum -a 256 KustoTerminal-osx-x64.tar.gz`
3. Update formula with correct checksum
4. GitHub release artifacts should never change after publishing

### Installation Issues

```bash
# Verbose installation for debugging
brew install --verbose kustoterminal

# Check formula syntax
brew audit kustoterminal

# Uninstall and reinstall
brew uninstall kustoterminal
brew cleanup
brew install kustoterminal
```

### Formula Validation

```bash
# Run Homebrew's style checks
brew style kustoterminal

# Run audit
brew audit --strict kustoterminal

# Test installation
brew test kustoterminal
```

## Additional Resources

- **Homebrew Formula Cookbook**: https://docs.brew.sh/Formula-Cookbook
- **Homebrew Documentation**: https://docs.brew.sh/
- **Creating Taps**: https://docs.brew.sh/How-to-Create-and-Maintain-a-Tap
- **Acceptable Formulae**: https://docs.brew.sh/Acceptable-Formulae
- **Your GitHub Repository**: https://github.com/basaba/kusto-terminal

## Support

For issues with:
- **KustoTerminal**: https://github.com/basaba/kusto-terminal/issues
- **Homebrew Installation**: Create issue in your tap repository
- **Homebrew itself**: https://github.com/Homebrew/brew/issues

## Quick Reference

```bash
# Build
./build-homebrew.sh 0.1.0

# Create release
gh release create v0.1.0 releases/*.tar.gz

# Test locally
brew install --build-from-source homebrew/kustoterminal.rb

# Create tap (one-time)
gh repo create basaba/homebrew-kustoterminal --public

# Publish to tap
git add Formula/kustoterminal.rb
git commit -m "Update kustoterminal to v0.1.0"
git push

# Users install
brew install basaba/kustoterminal/kustoterminal
