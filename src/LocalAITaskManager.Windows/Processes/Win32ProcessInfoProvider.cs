using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Windows.Processes;

public sealed class Win32ProcessInfoProvider : IProcessInfoProvider
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, int flags, [Out] StringBuilder lpExeName, ref uint lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private sealed record CachedEntry(
        ProcessMetadata Metadata,
        DateTime? StartTime
    );

    private readonly Dictionary<int, CachedEntry> _cache = [];
    private readonly object _syncLock = new();

    public ProcessMetadata GetMetadata(int pid)
    {
        lock (_syncLock)
        {
            DateTime? startTime = TryGetStartTime(pid);

            if (_cache.TryGetValue(pid, out CachedEntry? existing))
            {
                // If start times match (or if both are unavailable), reuse cached metadata
                if (!startTime.HasValue || !existing.StartTime.HasValue || startTime.Value == existing.StartTime.Value)
                {
                    return existing.Metadata;
                }
            }

            string processName = TryGetProcessName(pid, out string? exePath);
            string? commandLine = TryGetCommandLineWmi(pid);

            var metadata = new ProcessMetadata(
                Pid: pid,
                ProcessName: processName,
                ExecutablePath: exePath,
                CommandLine: commandLine
            );

            _cache[pid] = new CachedEntry(metadata, startTime);
            return metadata;
        }
    }

    public void Cleanup(IReadOnlyCollection<int> currentPids)
    {
        lock (_syncLock)
        {
            var pidsToRemove = new List<int>();
            foreach (int pid in _cache.Keys)
            {
                if (!currentPids.Contains(pid))
                {
                    pidsToRemove.Add(pid);
                }
            }

            foreach (int pid in pidsToRemove)
            {
                _cache.Remove(pid);
            }
        }
    }

    private static DateTime? TryGetStartTime(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return proc.StartTime;
        }
        catch
        {
            return null;
        }
    }

    private static string TryGetProcessName(int pid, out string? executablePath)
    {
        executablePath = TryGetExecutablePath(pid);

        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                string fileName = Path.GetFileName(executablePath);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return fileName;
                }
            }
            catch
            {
                // Fallback to Process.ProcessName
            }
        }

        try
        {
            using var proc = Process.GetProcessById(pid);
            string name = proc.ProcessName;
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name += ".exe";
            }
            return name;
        }
        catch
        {
            return $"Process_{pid}";
        }
    }

    private static string? TryGetExecutablePath(int pid)
    {
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            uint size = (uint)buffer.Capacity;
            if (QueryFullProcessImageNameW(hProcess, 0, buffer, ref size))
            {
                return buffer.ToString();
            }
            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    private static string? TryGetCommandLineWmi(int pid)
    {
        try
        {
            string query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}";
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();

            foreach (ManagementBaseObject obj in results)
            {
                object? cmdObj = obj["CommandLine"];
                if (cmdObj is string cmd && !string.IsNullOrWhiteSpace(cmd))
                {
                    return cmd;
                }
            }
        }
        catch
        {
            // Best effort; Access Denied or WMI failure yields null
        }

        return null;
    }
}
