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

    public static string FormatRatio(ulong? usedBytes, ulong? totalBytes, string unavailableFallback = "—")
    {
        if (!usedBytes.HasValue || !totalBytes.HasValue)
        {
            return unavailableFallback;
        }

        double usedGb = (double)usedBytes.Value / OneGb;
        double totalGb = (double)totalBytes.Value / OneGb;

        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} / {1:0.0} GB", usedGb, totalGb);
    }

    public static string FormatTokens(int? tokens)
    {
        if (!tokens.HasValue || tokens.Value <= 0)
        {
            return "—";
        }

        int val = tokens.Value;
        if (val >= 1_000_000)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.#}M", val / 1_000_000.0);
        }
        if (val >= 1024 && val % 1024 == 0)
        {
            return $"{val / 1024}K";
        }
        if (val >= 1000)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.#}K", val / 1000.0);
        }
        return val.ToString(CultureInfo.InvariantCulture);
    }
}
