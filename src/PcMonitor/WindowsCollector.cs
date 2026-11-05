using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PcMonitor;

public sealed class WindowsCollector
{
    private CpuTimes? previous;

    public Snapshot Collect()
    {
        if (!GetSystemTimes(out ulong idle, out ulong kernel, out ulong user)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var current = new CpuTimes(idle, kernel, user);
        double? usage = previous is { } prior ? CpuUsage.Calculate(prior, current) : null;
        previous = current;
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var disks = new List<DiskSnapshot>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            // Never probe mapped network or removable drives.
            if (drive.DriveType != DriveType.Fixed) continue;
            try { disks.Add(new(drive.Name, drive.TotalSize, drive.TotalFreeSpace, null)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { disks.Add(new(drive.Name, null, null, e.Message)); }
        }
        return new(1, DateTimeOffset.UtcNow, usage, Environment.ProcessorCount,
            new(memory.TotalPhysical, memory.AvailablePhysical), GetTickCount64(), disks);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
