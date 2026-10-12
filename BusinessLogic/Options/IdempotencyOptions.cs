namespace BusinessLogic.Options;

public class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public int CleanupBatchSize { get; set; }

    public int RetentionSeconds { get; set; }

    public int CleanupIntervalSeconds { get; set; }

    public TimeSpan Retention => TimeSpan.FromSeconds(RetentionSeconds);

    public TimeSpan CleanupInterval => TimeSpan.FromSeconds(CleanupIntervalSeconds);
}
