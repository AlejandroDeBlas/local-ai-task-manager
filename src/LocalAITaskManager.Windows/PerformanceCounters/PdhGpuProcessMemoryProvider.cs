using System.Runtime.InteropServices;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Windows.PerformanceCounters;

public sealed class PdhGpuProcessMemoryProvider : IGpuProcessMemoryProvider, IDisposable
{
    private const string LocalCounterPath = @"\GPU Process Memory(*)\Local Usage";
    private const string NonLocalCounterPath = @"\GPU Process Memory(*)\Non Local Usage";
    private const string TotalCommittedCounterPath = @"\GPU Process Memory(*)\Total Committed";
    private const string DedicatedCounterPath = @"\GPU Process Memory(*)\Dedicated Usage";
    private const string SharedCounterPath = @"\GPU Process Memory(*)\Shared Usage";

    private readonly object _syncLock = new();
    private IntPtr _hQuery = IntPtr.Zero;
    private IntPtr _hLocalCounter = IntPtr.Zero;
    private IntPtr _hNonLocalCounter = IntPtr.Zero;
    private IntPtr _hTotalCommittedCounter = IntPtr.Zero;
    private IntPtr _hDedicatedCounter = IntPtr.Zero;
    private IntPtr _hSharedCounter = IntPtr.Zero;
    private bool _initialized;
    private bool _disposed;
    private string? _lastError;

    public string? LastError => _lastError;

    public bool IsAvailable => _initialized && _hQuery != IntPtr.Zero;

    private void EnsureInitialized()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdhGpuProcessMemoryProvider));
        }

        if (_initialized && _hQuery != IntPtr.Zero)
        {
            return;
        }

        CleanupQuery();

        uint status = PdhNative.PdhOpenQueryW(null, UIntPtr.Zero, out _hQuery);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhOpenQueryW failed with error 0x{status:X8}.";
            _hQuery = IntPtr.Zero;
            throw new PdhTelemetryException("PdhOpenQueryW failed", status);
        }

        status = PdhNative.PdhAddEnglishCounterW(_hQuery, LocalCounterPath, UIntPtr.Zero, out _hLocalCounter);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhAddEnglishCounterW failed for Local Usage (0x{status:X8}).";
            CleanupQuery();
            throw new PdhTelemetryException("PdhAddEnglishCounterW failed for Local Usage", status);
        }

        status = PdhNative.PdhAddEnglishCounterW(_hQuery, NonLocalCounterPath, UIntPtr.Zero, out _hNonLocalCounter);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhAddEnglishCounterW failed for Non Local Usage (0x{status:X8}).";
            CleanupQuery();
            throw new PdhTelemetryException("PdhAddEnglishCounterW failed for Non Local Usage", status);
        }

        status = PdhNative.PdhAddEnglishCounterW(_hQuery, TotalCommittedCounterPath, UIntPtr.Zero, out _hTotalCommittedCounter);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhAddEnglishCounterW failed for Total Committed (0x{status:X8}).";
            CleanupQuery();
            throw new PdhTelemetryException("PdhAddEnglishCounterW failed for Total Committed", status);
        }

        status = PdhNative.PdhAddEnglishCounterW(_hQuery, DedicatedCounterPath, UIntPtr.Zero, out _hDedicatedCounter);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhAddEnglishCounterW failed for Dedicated Usage (0x{status:X8}).";
            CleanupQuery();
            throw new PdhTelemetryException("PdhAddEnglishCounterW failed for Dedicated Usage", status);
        }

        status = PdhNative.PdhAddEnglishCounterW(_hQuery, SharedCounterPath, UIntPtr.Zero, out _hSharedCounter);
        if (status != PdhNative.ERROR_SUCCESS)
        {
            _lastError = $"PdhAddEnglishCounterW failed for Shared Usage (0x{status:X8}).";
            CleanupQuery();
            throw new PdhTelemetryException("PdhAddEnglishCounterW failed for Shared Usage", status);
        }

        // Prime the query with an initial collection
        PdhNative.PdhCollectQueryData(_hQuery);

        _initialized = true;
        _lastError = null;
    }

    public Task<IReadOnlyList<GpuProcessMemorySample>> GetProcessMemoryAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            if (_disposed)
            {
                return Task.FromResult<IReadOnlyList<GpuProcessMemorySample>>([]);
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                EnsureInitialized();
            }
            catch (PdhTelemetryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                throw new PdhTelemetryException($"Initialization error: {ex.Message}", 0xFFFFFFFF);
            }

            cancellationToken.ThrowIfCancellationRequested();

            uint status = PdhNative.PdhCollectQueryData(_hQuery);
            if (status != PdhNative.ERROR_SUCCESS)
            {
                _lastError = $"PdhCollectQueryData failed with error 0x{status:X8}.";
                CleanupQuery();
                throw new PdhTelemetryException("PdhCollectQueryData failed", status);
            }

            var localMap = CollectCounterMap(_hLocalCounter, "Local Usage");
            var nonLocalMap = CollectCounterMap(_hNonLocalCounter, "Non Local Usage");
            var totalCommittedMap = CollectCounterMap(_hTotalCommittedCounter, "Total Committed");
            var dedicatedMap = CollectCounterMap(_hDedicatedCounter, "Dedicated Usage");
            var sharedMap = CollectCounterMap(_hSharedCounter, "Shared Usage");

            var allInstances = new HashSet<string>(localMap.Keys, StringComparer.OrdinalIgnoreCase);
            allInstances.UnionWith(nonLocalMap.Keys);
            allInstances.UnionWith(totalCommittedMap.Keys);
            allInstances.UnionWith(dedicatedMap.Keys);
            allInstances.UnionWith(sharedMap.Keys);

            var samples = new List<GpuProcessMemorySample>(allInstances.Count);

            foreach (string instance in allInstances)
            {
                if (GpuProcessCounterInstanceParser.TryParse(instance, out ParsedInstanceInfo? info) && info is not null)
                {
                    localMap.TryGetValue(instance, out ulong? local);
                    nonLocalMap.TryGetValue(instance, out ulong? nonLocal);
                    totalCommittedMap.TryGetValue(instance, out ulong? totalCommitted);
                    dedicatedMap.TryGetValue(instance, out ulong? dedicated);
                    sharedMap.TryGetValue(instance, out ulong? shared);

                    samples.Add(new GpuProcessMemorySample(
                        Pid: info.Pid,
                        LocalBytes: local,
                        NonLocalBytes: nonLocal,
                        TotalCommittedBytes: totalCommitted,
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

    private static Dictionary<string, ulong?> CollectCounterMap(IntPtr hCounter, string counterName)
    {
        var map = new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
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

        // Valid empty cases
        if (status is PdhNative.PDH_CSTATUS_NO_INSTANCE or PdhNative.PDH_NO_DATA || bufferSize == 0 || itemCount == 0)
        {
            return map;
        }

        if (status != PdhNative.PDH_MORE_DATA)
        {
            throw new PdhTelemetryException($"PdhGetFormattedCounterArrayW buffer sizing failed for {counterName}", status);
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
                throw new PdhTelemetryException($"PdhGetFormattedCounterArrayW collection failed for {counterName}", status);
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
                        ulong? val = PdhCounterFormatter.ExtractItemValue(item.FmtValue);
                        map[instanceName] = val;
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
        _hLocalCounter = IntPtr.Zero;
        _hNonLocalCounter = IntPtr.Zero;
        _hTotalCommittedCounter = IntPtr.Zero;
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
