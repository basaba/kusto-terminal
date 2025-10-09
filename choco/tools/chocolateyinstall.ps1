$ErrorActionPreference = 'Stop'

$packageName = 'kustoterminal'
$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url64 = 'https://github.com/basaba/kusto-terminal/releases/download/v0.1.0/KustoTerminal-win-x64.zip'

$packageArgs = @{
  packageName   = $packageName
  unzipLocation = $toolsDir
  url64bit      = $url64
  checksum64    = 'CHECKSUM_PLACEHOLDER'
  checksumType64= 'sha256'
}

Install-ChocolateyZipPackage @packageArgs

# Create shim for the executable
$exePath = Join-Path $toolsDir 'KustoTerminal.exe'
Install-BinFile -Name 'kustoterminal' -Path $exePath
