$ErrorActionPreference = 'Stop'

$packageName = 'kustoterminal'

# Remove the shim
Uninstall-BinFile -Name 'kustoterminal'
