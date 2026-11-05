param(
    [string]$Executable = (Join-Path $env:LOCALAPPDATA 'PcMonitor\bin\PcMonitor.exe'),
    [string]$DataDirectory = (Join-Path $env:LOCALAPPDATA 'PcMonitor\data'),
    [ValidateRange(1,86400)][double]$IntervalSeconds = 5
)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$DataDirectory = [IO.Path]::GetFullPath($DataDirectory)
$interval = $IntervalSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
$process = Start-Process -FilePath $Executable -ArgumentList "--interval $interval --data-dir `"$DataDirectory`"" -WindowStyle Hidden -PassThru
Start-Sleep -Milliseconds 400
if ($process.HasExited) { throw "Agent exited with code $($process.ExitCode). Check agent.log or PcMonitor-startup.log in TEMP." }
Write-Output "Agent started (PID $($process.Id)). Data: $DataDirectory"
