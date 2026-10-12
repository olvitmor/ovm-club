namespace WebApi.RateLimiting;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public int RedisBreakDurationSeconds { get; set; }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

    public TimeSpan RedisBreakDuration => TimeSpan.FromSeconds(RedisBreakDurationSeconds);
}
