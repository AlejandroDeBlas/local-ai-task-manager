using System.Runtime.InteropServices;
using System.Text;

namespace LocalAITaskManager.Windows.Nvidia;

public enum NvmlReturn : int
{
    Success = 0,
    Uninitialized = 1,
    InvalidArgument = 2,
    NotSupported = 3,
    NoPermission = 4,
    AlreadyInitialized = 5,
    NotFound = 6,
    InsufficientSize = 7,
    InsufficientPower = 8,
    DriverNotLoaded = 9,
    Timeout = 10,
    IrqIssue = 11,
    LibraryNotFound = 12,
    FunctionNotFound = 13,
    CorruptedInforom = 14,
    GpuIsLost = 15,
    ResetRequired = 16,
    OperatingSystem = 17,
    Unknown = 999
}

public enum NvmlTemperatureSensors : int
{
    Gpu = 0
}

[StructLayout(LayoutKind.Sequential)]
public struct NvmlMemory
{
    public ulong Total;
    public ulong Free;
    public ulong Used;
}

[StructLayout(LayoutKind.Sequential)]
public struct NvmlUtilization
{
    public uint Gpu;
    public uint Memory;
}

public sealed class NvmlNative : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlInitDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlShutdownDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlSystemGetDriverVersionDelegate(byte[] version, uint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlDeviceGetCountDelegate(out uint deviceCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlDeviceGetHandleByIndexDelegate(uint index, out IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlDeviceGetNameDelegate(IntPtr device, byte[] name, uint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlDeviceGetMemoryInfoDelegate(IntPtr device, out NvmlMemory memory);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlDeviceGetUtilizationRatesDelegate(IntPtr device, out NvmlUtilization utilization);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlDeviceGetTemperatureDelegate(IntPtr device, NvmlTemperatureSensors sensorType, out uint temp);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlDeviceGetPowerUsageDelegate(IntPtr device, out uint powerMilliWatts);

    private readonly IntPtr _libraryHandle;
    private readonly NvmlInitDelegate _init;
    private readonly NvmlShutdownDelegate _shutdown;
    private readonly NvmlSystemGetDriverVersionDelegate? _getDriverVersion;
    private readonly NvmlDeviceGetCountDelegate _getDeviceCount;
    private readonly NvmlDeviceGetHandleByIndexDelegate _getDeviceHandleByIndex;
    private readonly NvmlDeviceGetNameDelegate _getDeviceName;
    private readonly NvmlDeviceGetMemoryInfoDelegate _getDeviceMemoryInfo;
    private readonly NvmlDeviceGetUtilizationRatesDelegate? _getDeviceUtilizationRates;
    private readonly NvmlDeviceGetTemperatureDelegate? _getDeviceTemperature;
    private readonly NvmlDeviceGetPowerUsageDelegate? _getDevicePowerUsage;

    private bool _initialized;
    private bool _disposed;

    private NvmlNative(IntPtr libraryHandle)
    {
        _libraryHandle = libraryHandle;

        _init = GetExport<NvmlInitDelegate>(libraryHandle, "nvmlInit_v2", "nvmlInit")
            ?? throw new EntryPointNotFoundException("Failed to find nvmlInit export.");

        _shutdown = GetExport<NvmlShutdownDelegate>(libraryHandle, "nvmlShutdown")
            ?? throw new EntryPointNotFoundException("Failed to find nvmlShutdown export.");

        _getDriverVersion = GetExport<NvmlSystemGetDriverVersionDelegate>(libraryHandle, "nvmlSystemGetDriverVersion");

        _getDeviceCount = GetExport<NvmlDeviceGetCountDelegate>(libraryHandle, "nvmlDeviceGetCount_v2", "nvmlDeviceGetCount")
            ?? throw new EntryPointNotFoundException("Failed to find nvmlDeviceGetCount export.");

        _getDeviceHandleByIndex = GetExport<NvmlDeviceGetHandleByIndexDelegate>(libraryHandle, "nvmlDeviceGetHandleByIndex_v2", "nvmlDeviceGetHandleByIndex")
            ?? throw new EntryPointNotFoundException("Failed to find nvmlDeviceGetHandleByIndex export.");

        _getDeviceName = GetExport<NvmlDeviceGetNameDelegate>(libraryHandle, "nvmlDeviceGetName")
            ?? throw new EntryPointNotFoundException("Failed to find nvmlDeviceGetName export.");

        _getDeviceMemoryInfo = GetExport<NvmlDeviceGetMemoryInfoDelegate>(libraryHandle, "nvmlDeviceGetMemoryInfo")
            ?? throw new EntryPointNotFoundException("Failed to find nvmlDeviceGetMemoryInfo export.");

        _getDeviceUtilizationRates = GetExport<NvmlDeviceGetUtilizationRatesDelegate>(libraryHandle, "nvmlDeviceGetUtilizationRates");
        _getDeviceTemperature = GetExport<NvmlDeviceGetTemperatureDelegate>(libraryHandle, "nvmlDeviceGetTemperature");
        _getDevicePowerUsage = GetExport<NvmlDeviceGetPowerUsageDelegate>(libraryHandle, "nvmlDeviceGetPowerUsage");
    }

    public static bool TryLoad(out NvmlNative? instance, out string? errorMessage)
    {
        instance = null;
        errorMessage = null;

        string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string dchPath = Path.Combine(systemRoot, "System32", "nvml.dll");

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string standardPath = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvml.dll");

        string? resolvedPath = null;
        if (File.Exists(dchPath))
        {
            resolvedPath = dchPath;
        }
        else if (File.Exists(standardPath))
        {
            resolvedPath = standardPath;
        }

        if (resolvedPath is null)
        {
            errorMessage = "NVIDIA driver library (nvml.dll) was not found in trusted system directories.";
            return false;
        }

        IntPtr handle = IntPtr.Zero;
        try
        {
            if (!NativeLibrary.TryLoad(resolvedPath, out handle))
            {
                errorMessage = $"Failed to load NVIDIA driver library from {resolvedPath}.";
                return false;
            }

            instance = new NvmlNative(handle);
            return true;
        }
        catch (Exception ex)
        {
            if (handle != IntPtr.Zero)
            {
                NativeLibrary.Free(handle);
            }
            errorMessage = $"Error initializing NVML native wrapper: {ex.Message}";
            return false;
        }
    }

    public NvmlReturn Init()
    {
        if (_initialized)
        {
            return NvmlReturn.Success;
        }

        NvmlReturn ret = _init();
        if (ret is NvmlReturn.Success or NvmlReturn.AlreadyInitialized)
        {
            _initialized = true;
            return NvmlReturn.Success;
        }

        return ret;
    }

    public NvmlReturn GetDriverVersion(out string? driverVersion)
    {
        driverVersion = null;
        if (_getDriverVersion is null)
        {
            return NvmlReturn.NotSupported;
        }

        byte[] buffer = new byte[80];
        NvmlReturn ret = _getDriverVersion(buffer, (uint)buffer.Length);
        if (ret == NvmlReturn.Success)
        {
            driverVersion = DecodeUtf8CString(buffer);
        }

        return ret;
    }

    public NvmlReturn GetDeviceCount(out uint count) => _getDeviceCount(out count);

    public NvmlReturn GetDeviceHandleByIndex(uint index, out IntPtr device) => _getDeviceHandleByIndex(index, out device);

    public NvmlReturn GetDeviceName(IntPtr device, out string name)
    {
        byte[] buffer = new byte[96];
        NvmlReturn ret = _getDeviceName(device, buffer, (uint)buffer.Length);
        name = ret == NvmlReturn.Success ? DecodeUtf8CString(buffer) : string.Empty;
        return ret;
    }

    public NvmlReturn GetDeviceMemoryInfo(IntPtr device, out NvmlMemory memory) => _getDeviceMemoryInfo(device, out memory);

    public NvmlReturn GetDeviceUtilizationRates(IntPtr device, out NvmlUtilization utilization)
    {
        if (_getDeviceUtilizationRates is null)
        {
            utilization = default;
            return NvmlReturn.NotSupported;
        }

        return _getDeviceUtilizationRates(device, out utilization);
    }

    public NvmlReturn GetDeviceTemperature(IntPtr device, NvmlTemperatureSensors sensorType, out uint temp)
    {
        if (_getDeviceTemperature is null)
        {
            temp = 0;
            return NvmlReturn.NotSupported;
        }

        return _getDeviceTemperature(device, sensorType, out temp);
    }

    public NvmlReturn GetDevicePowerUsage(IntPtr device, out uint powerMilliWatts)
    {
        if (_getDevicePowerUsage is null)
        {
            powerMilliWatts = 0;
            return NvmlReturn.NotSupported;
        }

        return _getDevicePowerUsage(device, out powerMilliWatts);
    }

    private static T? GetExport<T>(IntPtr libraryHandle, params string[] names) where T : Delegate
    {
        foreach (string name in names)
        {
            if (NativeLibrary.TryGetExport(libraryHandle, name, out IntPtr address))
            {
                return Marshal.GetDelegateForFunctionPointer<T>(address);
            }
        }
        return null;
    }

    private static string DecodeUtf8CString(byte[] bytes)
    {
        int nullIdx = Array.IndexOf(bytes, (byte)0);
        int length = nullIdx >= 0 ? nullIdx : bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_initialized)
        {
            try
            {
                _shutdown();
            }
            catch
            {
                // Best effort shutdown
            }
            _initialized = false;
        }

        if (_libraryHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
        }
    }
}
