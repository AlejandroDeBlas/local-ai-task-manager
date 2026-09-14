using System.Runtime.InteropServices;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Windows.PerformanceCounters;

public sealed class PdhGpuProcessMemoryProvider : IGpuProcessMemoryProvider, IDisposable
{
    private const string DedicatedCounterPath = @"\GPU Process Memory(*)\Dedicated Usage";
    private const string SharedCounterPath = @"\GPU Process Memory(*)\Shared Usage";

    private readonly object _syncLock = new();
    private IntPtr _hQuery = IntPtr.Zero;
    private IntPtr _hDedicatedCounter = IntPtr.Zero;
    private IntPtr _hSharedCounter = IntPtr.Zero;
    private bool _initialized;
    private bool _disposed;
    private string? _lastError;

    public string? LastError => _lastError;

    public bool IsAvailable => _initialized && _hQuery != IntPtr.Zero;

    private bool EnsureInitialized()
    {
        if (_disposed)
        {
            return false;
        }

        if (_initialized && _hQuery != IntPtr.Zero)
        {
            return true;
        }

        CleanupQuery();

        uint status = PdhNative.PdhOpenQueryW(null, UIntPtr.Zero, out _hQuery);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhOpenQueryW failed with error 0x{status:X8}.";
            _hQuery = IntPtr.Zero;
            return false;
        }

        status = PdhNative.PdhAddEnglishCounterW(_hQuery, DedicatedCounterPath, UIntPtr.Zero, out _hDedicatedCounter);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhAddEnglishCounterW failed for Dedicated Usage with error 0x{status:X8}.";
            CleanupQuery();
            return false;
        }

        status = PdhNative.PdhAddEnglishCounterW(_hQuery, SharedCounterPath, UIntPtr.Zero, out _hSharedCounter);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhAddEnglishCounterW failed for Shared Usage with error 0x{status:X8}.";
            CleanupQuery();
            return false;
        }

        // Prime the query with an initial collection
        PdhNative.PdhCollectQueryData(_hQuery);

        _initialized = true;
        _lastError = null;
        return true;
    }

    public Task<IReadOnlyList<GpuProcessMemorySample>> GetProcessMemoryAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            if (_disposed)
            {
                return Task.FromResult<IReadOnlyList<GpuProcessMemorySample>>([]);
            }

            if (!EnsureInitialized())
            {
                return Task.FromResult<IReadOnlyList<GpuProcessMemorySample>>([]);
            }

            cancellationToken.ThrowIfCancellationRequested();

            uint status = PdhNative.PdhCollectQueryData(_hQuery);
            if (status != PdhNative.ERROR_SUCCESS)
            {
                _lastError = $"PdhCollectQueryData failed with error 0x{status:X8}.";
                // Invalidate query to reinitialize on next attempt
                CleanupQuery();
                return Task.FromResult<IReadOnlyList<GpuProcessMemorySample>>([]);
            }

            var dedicatedMap = CollectCounterMap(_hDedicatedCounter);
            var sharedMap = CollectCounterMap(_hSharedCounter);

            var allInstances = new HashSet<string>(dedicatedMap.Keys, StringComparer.OrdinalIgnoreCase);
            allInstances.UnionWith(sharedMap.Keys);

            var samples = new List<GpuProcessMemorySample>(allInstances.Count);

            foreach (string instance in allInstances)
            {
                if (GpuProcessCounterInstanceParser.TryParse(instance, out ParsedInstanceInfo? info) && info is not null)
                {
                    dedicatedMap.TryGetValue(instance, out ulong dedicated);
                    sharedMap.TryGetValue(instance, out ulong shared);

                    samples.Add(new GpuProcessMemorySample(
                        Pid: info.Pid,
                        DedicatedBytes: dedicated,
                        SharedBytes: shared,
                        PhysicalAdapterIndex: info.PhysicalAdapterIndex,
                        Luid: info.Luid
                    ));
                }
            }

            return Task.FromResult<IReadOnlyList<GpuProcessMemorySample>>(samples);
        }
    }

    private static Dictionary<string, ulong> CollectCounterMap(IntPtr hCounter)
    {
        var map = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        if (hCounter == IntPtr.Zero)
        {
            return map;
        }

        uint bufferSize = 0;
        uint itemCount = 0;

        uint status = PdhNative.PdhGetFormattedCounterArrayW(
            hCounter,
            PdhNative.PDH_FMT_LARGE | PdhNative.PDH_FMT_NOSCALE,
            ref bufferSize,
            ref itemCount,
            IntPtr.Zero
        );

        if (status != PdhNative.PDH_MORE_DATA || bufferSize == 0 || itemCount == 0)
        {
            return map;
        }

        IntPtr pBuffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            status = PdhNative.PdhGetFormattedCounterArrayW(
                hCounter,
                PdhNative.PDH_FMT_LARGE | PdhNative.PDH_FMT_NOSCALE,
                ref bufferSize,
                ref itemCount,
                pBuffer
            );

            if (status != PdhNative.ERROR_SUCCESS)
            {
                return map;
            }

            int itemSize = Marshal.SizeOf<PdhFmtCounterValueItem>();
            for (int i = 0; i < itemCount; i++)
            {
                IntPtr itemPtr = IntPtr.Add(pBuffer, i * itemSize);
                PdhFmtCounterValueItem item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(itemPtr);

                if (item.szName != IntPtr.Zero)
                {
                    string? instanceName = Marshal.PtrToStringUni(item.szName);
                    if (!string.IsNullOrWhiteSpace(instanceName))
                    {
                        ulong value = 0;
                        if (item.FmtValue.CStatus is PdhNative.PDH_CSTATUS_VALID_DATA or PdhNative.PDH_CSTATUS_NEW_DATA)
                        {
                            long rawVal = item.FmtValue.largeValue;
                            if (rawVal > 0)
                            {
                                value = (ulong)rawVal;
                            }
                        }

                        map[instanceName] = value;
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pBuffer);
        }

        return map;
    }

    private void CleanupQuery()
    {
        _initialized = false;
        _hDedicatedCounter = IntPtr.Zero;
        _hSharedCounter = IntPtr.Zero;

        if (_hQuery != IntPtr.Zero)
        {
            PdhNative.PdhCloseQuery(_hQuery);
            _hQuery = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CleanupQuery();
        }
    }
}
