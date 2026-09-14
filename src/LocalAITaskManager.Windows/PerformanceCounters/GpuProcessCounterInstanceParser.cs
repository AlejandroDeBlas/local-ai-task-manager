using System.Globalization;
using System.Text.RegularExpressions;

namespace LocalAITaskManager.Windows.PerformanceCounters;

public sealed record ParsedInstanceInfo(
    int Pid,
    long? LuidHigh,
    long? LuidLow,
    long? Luid,
    int? PhysicalAdapterIndex
);

public static partial class GpuProcessCounterInstanceParser
{
    private static readonly Regex InstanceRegex = new(
        @"^pid_(?<pid>\d+)(?:_luid_(?<luidHigh>0x[0-9a-fA-F]+|\d+)_(?<luidLow>0x[0-9a-fA-F]+|\d+))?(?:_phys_(?<phys>\d+))?(?:#\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    public static bool TryParse(string? instanceName, out ParsedInstanceInfo? info)
    {
        info = null;

        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return false;
        }

        Match match = InstanceRegex.Match(instanceName.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["pid"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid))
        {
            return false;
        }

        long? luidHigh = null;
        long? luidLow = null;
        long? fullLuid = null;

        if (match.Groups["luidHigh"].Success && match.Groups["luidLow"].Success)
        {
            string highStr = match.Groups["luidHigh"].Value;
            string lowStr = match.Groups["luidLow"].Value;

            if (TryParseHexOrDec(highStr, out long highVal) && TryParseHexOrDec(lowStr, out long lowVal))
            {
                luidHigh = highVal;
                luidLow = lowVal;
                fullLuid = (highVal << 32) | (lowVal & 0xFFFFFFFFL);
            }
        }

        int? physIndex = null;
        if (match.Groups["phys"].Success && int.TryParse(match.Groups["phys"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int phys))
        {
            physIndex = phys;
        }

        info = new ParsedInstanceInfo(pid, luidHigh, luidLow, fullLuid, physIndex);
        return true;
    }

    private static bool TryParseHexOrDec(string text, out long value)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
