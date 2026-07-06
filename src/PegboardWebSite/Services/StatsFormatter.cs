using System.Globalization;

namespace PegboardWebSite.Services;

/// <summary>
/// Formats a raw aggregate count for the public "N+" social-proof display.
/// Rounds DOWN (never inflates above the real count) to two significant figures,
/// so a real 34,127 renders "34,000+" and 1,187 renders "1,100+".
/// </summary>
public static class StatsFormatter
{
    public static string HonestRound(long value)
    {
        if (value <= 0) return "0+";
        if (value < 100) return value.ToString(CultureInfo.InvariantCulture) + "+";

        // Round DOWN to two significant figures. The tiny epsilon guards against
        // Math.Log10 returning e.g. 2.9999999 for an exact power of ten.
        int digits = (int)Math.Floor(Math.Log10(value) + 1e-9) + 1;
        long factor = (long)Math.Pow(10, digits - 2);
        long floored = value / factor * factor;
        return floored.ToString("#,##0", CultureInfo.InvariantCulture) + "+";
    }
}
