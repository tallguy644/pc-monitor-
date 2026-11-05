using PcMonitor;
using System.Text.Json;

int passed = 0;
void Test(string name, Action test) { test(); Console.WriteLine($"PASS {name}"); passed++; }
void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Invalid(string[] args) { try { Options.Parse(args); } catch (ArgumentException) { return; } catch (FormatException) { return; } throw new Exception("Accepted invalid options"); }
Test("CPU subtracts idle from kernel plus user", () => Assert(CpuUsage.Calculate(new(10, 20, 10), new(30, 60, 50)) == 75));
Test("CPU zero delta and counter reset are unknown", () => { Assert(CpuUsage.Calculate(new(1, 2, 3), new(1, 2, 3)) is null); Assert(CpuUsage.Calculate(new(1, 2, 3), new(0, 3, 4)) is null); });
Test("CPU clamps inconsistent counters", () => Assert(CpuUsage.Calculate(new(0, 0, 0), new(100, 1, 1)) == 0));
Test("Options defaults and explicit values", () => { Assert(Options.Parse([]).IntervalSeconds == 5); var o = Options.Parse(["--interval", "2.5", "--data-dir", ".", "--once"]); Assert(o.IntervalSeconds == 2.5 && o.Once && Path.IsPathFullyQualified(o.DataDirectory)); });
Test("Options reject unsafe intervals and unknown arguments", () => { foreach (var value in new[] { "0", "-1", "NaN", "Infinity", "86401", "no" }) Invalid(["--interval", value]); Invalid(["--interval"]); Invalid(["--unknown"]); });
Test("RAM used bytes", () => Assert(new MemorySnapshot(100, 25).UsedBytes == 75));
string temp = Path.Combine(Path.GetTempPath(), "PcMonitor-tests-" + Guid.NewGuid());
Directory.CreateDirectory(temp);
try
{
    Test("Snapshot replacement is valid JSON with null first CPU", () =>
    {
        var snapshot = new Snapshot(1, DateTimeOffset.UtcNow, null, 4, new(100, 40), 1000, []);
        SnapshotWriter.Write(temp, snapshot);
        SnapshotWriter.Write(temp, snapshot with { UptimeMilliseconds = 2000 });
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(temp, "snapshot.json")));
        Assert(json.RootElement.GetProperty("uptimeMilliseconds").GetUInt64() == 2000);
        Assert(json.RootElement.GetProperty("cpuUsagePercent").ValueKind == JsonValueKind.Null);
        Assert(!File.Exists(Path.Combine(temp, "snapshot.json.tmp")));
    });
    Test("Logging rotates at one MiB", () => { File.WriteAllText(Path.Combine(temp, "agent.log"), new string('x', 1024 * 1024)); new AgentLog(temp).Write("test"); Assert(File.Exists(Path.Combine(temp, "agent.log.1"))); Assert(new FileInfo(Path.Combine(temp, "agent.log")).Length < 1024); });
    if (OperatingSystem.IsWindows()) Test("Real Windows API collection", () => { var c = new WindowsCollector(); var first = c.Collect(); Thread.Sleep(100); var second = c.Collect(); Assert(first.CpuUsagePercent is null && second.CpuUsagePercent is >= 0 and <= 100); Assert(second.Memory.TotalBytes > 0 && second.UptimeMilliseconds > 0); Assert(second.Disks.Count > 0); });
}
finally { Directory.Delete(temp, true); }
Console.WriteLine($"{passed} tests passed");
