using System.Runtime.InteropServices;

namespace LocalAITaskManager.Windows.PerformanceCounters;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct PdhFmtCounterValueItem
{
    public IntPtr szName;
    public PdhFmtCounterValue FmtValue;
}

[StructLayout(LayoutKind.Explicit)]
public struct PdhFmtCounterValue
{
    [FieldOffset(0)]
    public uint CStatus;

    [FieldOffset(8)]
    public long largeValue;

    [FieldOffset(8)]
    public double doubleValue;

    [FieldOffset(8)]
    public int longValue;
}

public static class PdhNative
{
    public const uint ERROR_SUCCESS = 0x00000000;
    public const uint PDH_MORE_DATA = 0x800007D2;
    public const uint PDH_NO_DATA = 0x800007D5;
    public const uint PDH_CSTATUS_NO_INSTANCE = 0x800007D1;
    public const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
    public const uint PDH_CSTATUS_NEW_DATA = 0x00000001;

    public const uint PDH_FMT_LONG = 0x00000100;
    public const uint PDH_FMT_DOUBLE = 0x00000200;
    public const uint PDH_FMT_LARGE = 0x00000400;
    public const uint PDH_FMT_NOSCALE = 0x00001000;

    [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint PdhOpenQueryW(string? szDataSource, UIntPtr dwUserData, out IntPtr phQuery);

    [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, UIntPtr dwUserData, out IntPtr phCounter);

    [DllImport("pdh.dll", SetLastError = true)]
    public static extern uint PdhCollectQueryData(IntPtr hQuery);

    [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint PdhGetFormattedCounterArrayW(
        IntPtr hCounter,
        uint dwFormat,
        ref uint lpdwBufferSize,
        ref uint lpdwItemCount,
        IntPtr lpItemBuffer);

    [DllImport("pdh.dll", SetLastError = true)]
    public static extern uint PdhCloseQuery(IntPtr hQuery);
}
