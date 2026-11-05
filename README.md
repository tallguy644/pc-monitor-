# PC Monitor agent

A headless Windows hardware monitor. The `WinExe` executable creates no window, tray icon, web server, or outbound connection. No WMI, performance-counter polling, drivers, NuGet packages, or third-party runtime libraries are used. The standalone CI package includes the .NET runtime.

## Run

Download and extract **PcMonitor-win-x64** from a successful [Windows build](https://github.com/tallguy644/pc-monitor-/actions/workflows/windows.yml). Windows 10/11 x64 is the initial target. Run these commands from the extracted directory in PowerShell:

```powershell
# Start quietly; no administrator rights needed.
.\scripts\Start-Agent.ps1 -Executable "$pwd\publish\PcMonitor.exe" -IntervalSeconds 5

# Gracefully stop the instance using the default data directory.
.\scripts\Stop-Agent.ps1

# Copy the package into LOCALAPPDATA and start at each sign-in.
.\scripts\Install-Agent.ps1 -PackageDirectory "$pwd\publish" -IntervalSeconds 10

# Stop, remove sign-in startup and installed binaries; retain monitoring data.
.\scripts\Uninstall-Agent.ps1
```

Installation is **per user**, using `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\PcMonitor`. It starts at sign-in, not before sign-in, and is not a Windows service. Re-running installation stops the existing instance and updates files and interval. Keep the extracted scripts for later control/removal. If your PowerShell policy blocks scripts, follow your organization's policy or unblock the downloaded files where permitted.

Direct command-line options:

```powershell
# Start-Process -Wait is needed to obtain a WinExe's exit code reliably.
Start-Process .\publish\PcMonitor.exe -ArgumentList '--interval 2.5 --data-dir "C:\monitor data"' -WindowStyle Hidden
Start-Process .\publish\PcMonitor.exe -ArgumentList '--stop --data-dir "C:\monitor data"' -WindowStyle Hidden -Wait
Start-Process .\publish\PcMonitor.exe -ArgumentList '--once --data-dir "C:\monitor data"' -WindowStyle Hidden -Wait
```

`--interval` accepts invariant-culture seconds from 1 through 86400 (default 5). Change it by stopping and restarting, or reinstalling. `--once` writes one snapshot and exits; its CPU usage is null because no prior sample exists. `--data-dir` defaults to `%LOCALAPPDATA%\PcMonitor` for direct invocation; the scripts consistently use `%LOCALAPPDATA%\PcMonitor\data`. Always pass the same directory to start and stop. One instance per data directory is enforced by an exclusive file handle, released by Windows even after a crash. The remaining `agent.lock` file is harmless. Exit codes: 0 success, 1 invalid options/failure, 2 another instance holds the directory.

## Local data

`snapshot.json` contains the latest sample only, replaced through a temporary file in the same directory so readers do not receive partially written JSON. Readers should open/read/close promptly and retry sharing violations. If writing fails, the previous snapshot remains; consumers must check `timestampUtc` for freshness.

```json
{
  "schemaVersion": 1,
  "timestampUtc": "2026-11-05T12:00:00+00:00",
  "cpuUsagePercent": 12.5,
  "logicalProcessorCount": 16,
  "memory": { "totalBytes": 34359738368, "availableBytes": 17179869184, "usedBytes": 17179869184 },
  "uptimeMilliseconds": 1234567,
  "disks": [{ "name": "C:\\", "totalBytes": 1000000000000, "freeBytes": 500000000000, "error": null }]
}
```

CPU is the busy percentage over the previous sampling period, calculated from [`GetSystemTimes`](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes); kernel time includes idle time. Initial/invalid counter deltas produce null. Systems with more than 64 logical processors have processor-group limitations in this API. `logicalProcessorCount` is the count available to the process. RAM uses `GlobalMemoryStatusEx`. Uptime uses [`GetTickCount64`](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64), including sleep/hibernation time; Fast Startup can preserve the kernel session across shutdowns. Fixed local drives report total capacity and total free space, not disk activity. Network and removable drives are skipped. An inaccessible disk has null sizes and an error. There is no GPU, temperature, fan, remote access, or historical time series support in v0.1.

`agent.log` records start, stop and collection/write failures, rotating at 1 MiB into one backup (`agent.log.1`). Successful samples are not logged. Fatal startup/argument errors also go to `%TEMP%\PcMonitor-startup.log`. Stop requests are checked at most every second while waiting; a blocked OS/storage call can delay shutdown, and the stop script reports a timeout rather than killing an unrelated process. Startup entries do not restart a crashed process until the next sign-in or manual start.

The agent sleeps between sequential samples and never launches a process per sample. Sampling work adds to the configured delay. It wakes up to once a second for shutdown checks and writes one small snapshot per interval. Actual resource usage depends on storage and sampling frequency; no performance budget has yet been established. Data is local under the user's normal filesystem permissions. Do not use an untrusted/shared data directory.

## Build and verify

Install the .NET 8 SDK. The projects have no package dependencies; `NuGet.Config` disables package feeds for normal builds.

```powershell
dotnet build src/PcMonitor -c Release
dotnet run --project tests/PcMonitor.Tests -c Release
dotnet publish src/PcMonitor -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish --source https://api.nuget.org/v3/index.json
.\scripts\Test-Lifecycle.ps1 -Executable "$pwd\artifacts\publish\PcMonitor.exe"
```

The publish step downloads Microsoft's runtime packs at build time; the published agent makes no network requests. Framework-dependent builds require the .NET 8 runtime. The dependency-free test executable exits nonzero on failure and covers CPU math, argument validation, memory arithmetic, JSON replacement, log rotation, and real Windows API collection. The lifecycle smoke test covers hidden start, repeated samples, duplicate prevention, and graceful stop. CI builds, tests, publishes, smoke-tests, and uploads a standalone x64 artifact on pushes and pull requests. Install/uninstall registry persistence, reboot/sign-in startup, ARM64, multi-group CPUs, and prolonged resource usage require separate Windows acceptance testing.
