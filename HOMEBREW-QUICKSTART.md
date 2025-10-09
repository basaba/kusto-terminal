# Homebrew Quick Start Guide

This is a condensed guide to get KustoTerminal published on Homebrew quickly.

## Quick Setup (First Time)

### 1. Build the Release

```bash
./build-homebrew.sh 0.1.0
```

This creates:
- `releases/KustoTerminal-osx-x64.tar.gz`
- `releases/KustoTerminal-osx-arm64.tar.gz`
- Checksums for both files

### 2. Create GitHub Release

```bash
gh release create v0.1.0 \
  --title "v0.1.0" \
  --notes "Initial release for Homebrew" \
  releases/KustoTerminal-osx-x64.tar.gz \
  releases/KustoTerminal-osx-arm64.tar.gz
```

### 3. Update Formula

Edit `homebrew/kustoterminal.rb`:
- Change version to `0.1.0`
- Replace `CHECKSUM_X64_PLACEHOLDER` with the x64 checksum from step 1
- Replace `CHECKSUM_ARM64_PLACEHOLDER` with the ARM64 checksum from step 1

### 4. Create Homebrew Tap

```bash
# Create the tap repository
gh repo create basaba/homebrew-kustoterminal --public \
  --description "Homebrew tap for KustoTerminal"

# Clone and set up
cd /tmp
git clone https://github.com/basaba/homebrew-kustoterminal.git
cd homebrew-kustoterminal
mkdir -p Formula

# Copy your formula
cp /path/to/kusto-terminal/homebrew/kustoterminal.rb Formula/

# Commit and push
git add Formula/kustoterminal.rb
git commit -m "Add kustoterminal formula v0.1.0"
git push origin main
```

### 5. Test Installation

```bash
# Install your app
brew install basaba/kustoterminal/kustoterminal

# Test it works
kustoterminal --version

# Clean up
brew uninstall kustoterminal
```

## Done!

Users can now install with:

```bash
brew install basaba/kustoterminal/kustoterminal
```

---

## Updating to New Version

### 1. Build New Version

```bash
./build-homebrew.sh 0.2.0
```

### 2. Create GitHub Release

```bash
gh release create v0.2.0 \
  --title "v0.2.0" \
  --notes "Release notes here" \
  releases/KustoTerminal-osx-x64.tar.gz \
  releases/KustoTerminal-osx-arm64.tar.gz
```

### 3. Update Formula

Edit `homebrew/kustoterminal.rb` with new version and checksums.

### 4. Push to Tap

```bash
cd /path/to/homebrew-kustoterminal
cp /path/to/kusto-terminal/homebrew/kustoterminal.rb Formula/
git add Formula/kustoterminal.rb
git commit -m "Update kustoterminal to v0.2.0"
git push origin main
```

Users upgrade with:

```bash
brew update
brew upgrade kustoterminal
```

---

## Common Issues

**Build fails?**
```bash
dotnet --version  # Should be 8.0.x
rm -rf build/ releases/
./build-homebrew.sh
```

**Checksum error?**
```bash
shasum -a 256 releases/KustoTerminal-osx-x64.tar.gz
```

**Formula validation?**
```bash
brew install --build-from-source homebrew/kustoterminal.rb
brew audit kustoterminal
brew test kustoterminal
```

---

For detailed information, see [HOMEBREW-README.md](HOMEBREW-README.md)
