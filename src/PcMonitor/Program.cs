namespace PcMonitor;

public static class Program
{
    public static int Main(string[] args)
    {
        AgentLog? log = null;
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows is required.");
            var options = Options.Parse(args);
            Directory.CreateDirectory(options.DataDirectory);
            log = new(options.DataDirectory);
            string lockPath = Path.Combine(options.DataDirectory, "agent.lock");
            string stopPath = Path.Combine(options.DataDirectory, "stop.request");
            if (options.Stop)
            {
                File.WriteAllText(stopPath, "stop");
                return 0;
            }
            FileStream instance;
            try { instance = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { return 2; }
            using (instance)
            {
                if (File.Exists(stopPath)) File.Delete(stopPath);
                using var cancelled = new ManualResetEventSlim();
                ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancelled.Set(); };
                Console.CancelKeyPress += handler;
                try
                {
                    log.Write($"Started; interval={options.IntervalSeconds}s");
                    var collector = new WindowsCollector();
                    do
                    {
                        try { SnapshotWriter.Write(options.DataDirectory, collector.Collect()); }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
                        {
                            log.Write($"Sample failed: {e.Message}");
                            if (options.Once) return 1;
                        }
                        if (options.Once) break;
                        // Chunk the wait so stop requests are handled within one second.
                        var deadline = System.Diagnostics.Stopwatch.StartNew();
                        while (deadline.Elapsed.TotalSeconds < options.IntervalSeconds && !cancelled.IsSet && !File.Exists(stopPath))
                        {
                            var remaining = TimeSpan.FromSeconds(options.IntervalSeconds) - deadline.Elapsed;
                            if (remaining > TimeSpan.Zero) cancelled.Wait(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1));
                        }
                    } while (!cancelled.IsSet && !File.Exists(stopPath));
                    log.Write("Stopped");
                    return 0;
                }
                finally { Console.CancelKeyPress -= handler; }
            }
        }
        catch (Exception e)
        {
            log?.Write($"Fatal: {e}");
            // WinExe has no console; exit status and the fallback log diagnose startup errors.
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "PcMonitor-startup.log"), $"{DateTimeOffset.UtcNow:O} {e}{Environment.NewLine}"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return 1;
        }
    }
}
