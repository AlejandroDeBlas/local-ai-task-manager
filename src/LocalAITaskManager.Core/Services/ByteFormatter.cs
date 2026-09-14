using System.Globalization;

namespace LocalAITaskManager.Core.Services;

public static class ByteFormatter
{
    private const ulong OneKb = 1024UL;
    private const ulong OneMb = 1024UL * 1024UL;
    private const ulong OneGb = 1024UL * 1024UL * 1024UL;
    private const ulong OneTb = 1024UL * 1024UL * 1024UL * 1024UL;

    public static string Format(ulong? bytes, string unavailableFallback = "Unavailable")
    {
        if (!bytes.HasValue)
        {
            return unavailableFallback;
        }

        ulong value = bytes.Value;

        if (value >= OneTb)
        {
            double tb = (double)value / OneTb;
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} TB", tb);
        }

        if (value >= OneGb)
        {
            double gb = (double)value / OneGb;
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} GB", gb);
        }

        if (value >= OneMb)
        {
            double mb = (double)value / OneMb;
            return string.Format(CultureInfo.InvariantCulture, "{0:0} MB", mb);
        }

        if (value >= OneKb)
        {
            double kb = (double)value / OneKb;
            return string.Format(CultureInfo.InvariantCulture, "{0:0} KB", kb);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} B", value);
    }

    public static string FormatRatio(ulong usedBytes, ulong totalBytes)
    {
        double usedGb = (double)usedBytes / OneGb;
        double totalGb = (double)totalBytes / OneGb;

        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} / {1:0.0} GB", usedGb, totalGb);
    }
}
