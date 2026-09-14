namespace LocalAITaskManager.Windows.PerformanceCounters;

public static class PdhCounterFormatter
{
    public static ulong? ExtractItemValue(PdhFmtCounterValue fmtValue)
    {
        if (fmtValue.CStatus is not (PdhNative.PDH_CSTATUS_VALID_DATA or PdhNative.PDH_CSTATUS_NEW_DATA))
        {
            return null;
        }

        long rawVal = fmtValue.largeValue;
        return rawVal >= 0 ? (ulong)rawVal : null;
    }
}
