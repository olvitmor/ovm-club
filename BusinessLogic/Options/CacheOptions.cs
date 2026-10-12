namespace BusinessLogic.Options;

public class CacheOptions
{
    public const string SectionName = "Cache";

    public int EnrollmentCardTtlSeconds { get; set; }

    public TimeSpan EnrollmentCardTtl => TimeSpan.FromSeconds(EnrollmentCardTtlSeconds);
}
