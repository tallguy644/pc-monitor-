$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Stop-Agent.ps1')
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'PcMonitor' -ErrorAction SilentlyContinue
$root = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PcMonitor'))
$target = [IO.Path]::GetFullPath((Join-Path $root 'bin'))
if ((Split-Path $target -Parent) -ne $root -or (Split-Path $target -Leaf) -ne 'bin') { throw 'Unexpected uninstall path.' }
if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
Write-Output 'Uninstalled. Snapshots and logs are preserved in LOCALAPPDATA\PcMonitor\data.'
