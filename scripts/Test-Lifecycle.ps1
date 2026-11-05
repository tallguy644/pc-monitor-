param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$data = Join-Path ([IO.Path]::GetTempPath()) ('PcMonitor-smoke-' + [guid]::NewGuid())
try {
    & "$PSScriptRoot\Start-Agent.ps1" -Executable $Executable -DataDirectory $data -IntervalSeconds 1
    Start-Sleep -Seconds 3
    $snapshot = Get-Content -LiteralPath (Join-Path $data 'snapshot.json') -Raw | ConvertFrom-Json
    if ($null -eq $snapshot.cpuUsagePercent -or $snapshot.memory.totalBytes -le 0) { throw 'Invalid live snapshot' }
    $duplicate = Start-Process -FilePath $Executable -ArgumentList "--data-dir `"$data`"" -WindowStyle Hidden -Wait -PassThru
    if ($duplicate.ExitCode -ne 2) { throw 'Duplicate instance was not rejected' }
    & "$PSScriptRoot\Stop-Agent.ps1" -DataDirectory $data
    $before = (Get-Item -LiteralPath (Join-Path $data 'snapshot.json')).LastWriteTimeUtc
    Start-Sleep -Seconds 2
    if ((Get-Item -LiteralPath (Join-Path $data 'snapshot.json')).LastWriteTimeUtc -ne $before) { throw 'Agent continued after stop' }
    Write-Output 'PASS background start, sampling, duplicate prevention, graceful stop'
} finally {
    & "$PSScriptRoot\Stop-Agent.ps1" -DataDirectory $data
    # Unique test directory created above; leave logs available for diagnosis.
    Write-Output "Smoke-test data: $data"
}
