param(
    [string]$PackageDirectory = (Join-Path $PSScriptRoot '..\artifacts\publish'),
    [ValidateRange(1,86400)][double]$IntervalSeconds = 5
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $PackageDirectory).Path
if (!(Test-Path -LiteralPath (Join-Path $source 'PcMonitor.exe'))) { throw 'Package must contain PcMonitor.exe.' }
$root = Join-Path $env:LOCALAPPDATA 'PcMonitor'
& (Join-Path $PSScriptRoot 'Stop-Agent.ps1')
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
if ([IO.Path]::GetFullPath($source).TrimEnd('\') -ne [IO.Path]::GetFullPath($bin).TrimEnd('\')) {
    Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $bin -Force
}
$exe = Join-Path $bin 'PcMonitor.exe'
$data = Join-Path $root 'data'
$interval = $IntervalSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $run -Force | Out-Null
New-ItemProperty -Path $run -Name 'PcMonitor' -Value "`"$exe`" --interval $interval --data-dir `"$data`"" -PropertyType String -Force | Out-Null
& (Join-Path $PSScriptRoot 'Start-Agent.ps1') -IntervalSeconds $IntervalSeconds
