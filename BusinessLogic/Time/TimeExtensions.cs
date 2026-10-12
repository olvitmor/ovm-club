namespace BusinessLogic.Time;

public static class TimeExtensions
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    public static DateTime TruncateToMicroseconds(this DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return new DateTime(utc.Ticks - utc.Ticks % TicksPerMicrosecond, DateTimeKind.Utc);
    }

    public static DateTime GetUtcNowTruncated(this TimeProvider timeProvider)
    {
        return timeProvider.GetUtcNow().UtcDateTime.TruncateToMicroseconds();
    }

    public static DateOnly GetUtcToday(this TimeProvider timeProvider)
    {
        return DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    }
}
