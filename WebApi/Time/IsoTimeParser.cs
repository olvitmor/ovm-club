using System.Globalization;

namespace WebApi.Time;

public static class IsoTimeParser
{
    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
    ];

    private const string DateFormat = "yyyy-MM-dd";

    public static bool TryParseUtcDateTime(string? value, out DateTime result)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && DateTimeOffset.TryParseExact(
                value,
                DateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            result = parsed.UtcDateTime;
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryParseDate(string? value, out DateOnly result)
    {
        return DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
