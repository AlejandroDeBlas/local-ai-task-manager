using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Windows.Nvidia;

public sealed class NvmlGpuTelemetryProvider : IGpuTelemetryProvider, IDisposable
{
    private readonly NvmlNative? _nvml;
    private readonly string? _initError;
    private readonly bool _available;
    private readonly List<(uint Index, IntPtr Handle, string Name, string? DriverVersion)> _cachedDevices = [];
    private bool _devicesEnumerated;
    private bool _disposed;

    public NvmlGpuTelemetryProvider()
    {
        if (NvmlNative.TryLoad(out _nvml, out _initError))
        {
            NvmlReturn ret = _nvml!.Init();
            if (ret == NvmlReturn.Success)
            {
                _available = true;
            }
            else
            {
                _initError = $"nvmlInit returned {ret}";
                _nvml.Dispose();
                _nvml = null;
            }
        }
    }

    public string? InitializationError => _initError;

    public bool IsAvailable => _available;

    public Task<IReadOnlyList<GpuDeviceSnapshot>> GetGpusAsync(CancellationToken cancellationToken = default)
    {
        if (!_available || _nvml is null)
        {
            return Task.FromResult<IReadOnlyList<GpuDeviceSnapshot>>([]);
        }

        lock (_cachedDevices)
        {
            if (!_devicesEnumerated)
            {
                EnumerateDevices();
            }

            var snapshots = new List<GpuDeviceSnapshot>(_cachedDevices.Count);

            foreach (var (index, handle, name, driverVersion) in _cachedDevices)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ulong? totalVram = null;
                ulong? usedVram = null;

                NvmlReturn memRet = _nvml.GetDeviceMemoryInfo(handle, out NvmlMemory mem);
                if (memRet == NvmlReturn.Success)
                {
                    totalVram = mem.Total;
                    usedVram = mem.Used;
                }

                double? gpuUtil = null;
                NvmlReturn utilRet = _nvml.GetDeviceUtilizationRates(handle, out NvmlUtilization util);
                if (utilRet == NvmlReturn.Success)
                {
                    gpuUtil = util.Gpu;
                }

                double? temperature = null;
                NvmlReturn tempRet = _nvml.GetDeviceTemperature(handle, NvmlTemperatureSensors.Gpu, out uint temp);
                if (tempRet == NvmlReturn.Success)
                {
                    temperature = temp;
                }

                double? powerWatts = null;
                NvmlReturn pwrRet = _nvml.GetDevicePowerUsage(handle, out uint powerMilliWatts);
                if (pwrRet == NvmlReturn.Success)
                {
                    powerWatts = Math.Round(powerMilliWatts / 1000.0, 1);
                }

                string id = $"nvidia-gpu-{index}";
                snapshots.Add(new GpuDeviceSnapshot(
                    Id: id,
                    Index: (int)index,
                    Name: name,
                    TotalVramBytes: totalVram,
                    UsedVramBytes: usedVram,
                    GpuUtilizationPercent: gpuUtil,
                    TemperatureCelsius: temperature,
                    PowerWatts: powerWatts,
                    DriverVersion: driverVersion
                ));
            }

            return Task.FromResult<IReadOnlyList<GpuDeviceSnapshot>>(snapshots);
        }
    }

    private void EnumerateDevices()
    {
        if (_nvml is null)
        {
            return;
        }

        _cachedDevices.Clear();

        string? driverVersion = null;
        if (_nvml.GetDriverVersion(out string? dv) == NvmlReturn.Success)
        {
            driverVersion = dv;
        }

        if (_nvml.GetDeviceCount(out uint count) == NvmlReturn.Success)
        {
            for (uint i = 0; i < count; i++)
            {
                if (_nvml.GetDeviceHandleByIndex(i, out IntPtr handle) == NvmlReturn.Success)
                {
                    string name = $"NVIDIA GPU {i}";
                    if (_nvml.GetDeviceName(handle, out string devName) == NvmlReturn.Success && !string.IsNullOrWhiteSpace(devName))
                    {
                        name = devName;
                    }

                    _cachedDevices.Add((i, handle, name, driverVersion));
                }
            }
        }

        _devicesEnumerated = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cachedDevices.Clear();
        _nvml?.Dispose();
    }
}
