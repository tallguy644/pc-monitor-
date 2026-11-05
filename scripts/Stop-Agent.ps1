param([string]$DataDirectory = (Join-Path $env:LOCALAPPDATA 'PcMonitor\data'))
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $DataDirectory)) { return }
[IO.File]::WriteAllText((Join-Path $DataDirectory 'stop.request'), 'stop')
$lockPath = Join-Path $DataDirectory 'agent.lock'
$deadline = [DateTime]::UtcNow.AddSeconds(15)
do {
    try {
        $handle = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
        $handle.Dispose()
        return
    } catch [IO.IOException] { Start-Sleep -Milliseconds 200 }
} while ([DateTime]::UtcNow -lt $deadline)
throw 'Agent did not stop within 15 seconds. Inspect agent.log; no process was forcibly terminated.'
