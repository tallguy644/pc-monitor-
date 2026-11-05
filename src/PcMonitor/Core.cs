using System.Globalization;
using System.Text.Json;

namespace PcMonitor;

public sealed record Options(double IntervalSeconds, string DataDirectory, bool Once, bool Stop)
{
    public static Options Parse(string[] args)
    {
        double interval = 5;
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcMonitor");
        bool once = false, stop = false;
        for (int i = 0; i < args.Length; i++)
            switch (args[i])
            {
                case "--interval" when i + 1 < args.Length:
                    interval = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--data-dir" when i + 1 < args.Length:
                    directory = args[++i]; break;
                case "--once": once = true; break;
                case "--stop": stop = true; break;
                default: throw new ArgumentException($"Unknown or incomplete option: {args[i]}");
            }
        if (!double.IsFinite(interval) || interval < 1 || interval > 86400)
            throw new ArgumentException("Interval must be between 1 and 86400 seconds.");
        return new(interval, Path.GetFullPath(directory), once, stop);
    }
}

public readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
public static class CpuUsage
{
    public static double? Calculate(CpuTimes previous, CpuTimes current)
    {
        if (current.Idle < previous.Idle || current.Kernel < previous.Kernel || current.User < previous.User) return null;
        double total = (double)(current.Kernel - previous.Kernel) + (current.User - previous.User);
        return total <= 0 ? null : Math.Clamp(100 * (1 - (current.Idle - previous.Idle) / total), 0, 100);
    }
}

public sealed record MemorySnapshot(ulong TotalBytes, ulong AvailableBytes)
{
    public ulong UsedBytes => TotalBytes - Math.Min(TotalBytes, AvailableBytes);
}
public sealed record DiskSnapshot(string Name, long? TotalBytes, long? FreeBytes, string? Error);
public sealed record Snapshot(int SchemaVersion, DateTimeOffset TimestampUtc, double? CpuUsagePercent,
    int LogicalProcessorCount, MemorySnapshot Memory, ulong UptimeMilliseconds, IReadOnlyList<DiskSnapshot> Disks);

public static class SnapshotWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static void Write(string directory, Snapshot snapshot)
    {
        string target = Path.Combine(directory, "snapshot.json");
        string temporary = target + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, JsonOptions));
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class AgentLog(string directory)
{
    public void Write(string message)
    {
        try
        {
            string path = Path.Combine(directory, "agent.log");
            if (File.Exists(path) && new FileInfo(path).Length >= 1024 * 1024)
                File.Move(path, path + ".1", true);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
