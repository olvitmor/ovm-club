using BusinessLogic.Options;
using WebApi.RateLimiting;

namespace WebApi.Configuration;

public static class OptionsRegistration
{
    public static IServiceCollection AddApplicationOptions(this IServiceCollection services)
    {
        services
            .AddOptions<BookingOptions>()
            .BindConfiguration(BookingOptions.SectionName)
            .Validate(x => x.RefundCutoffSeconds > 0, "Booking:RefundCutoffSeconds must be positive")
            .Validate(x => x.WaitlistCloseBeforeSeconds > 0, "Booking:WaitlistCloseBeforeSeconds must be positive")
            .ValidateOnStart();

        services
            .AddOptions<ScheduleOptions>()
            .BindConfiguration(ScheduleOptions.SectionName)
            .Validate(x => x.MaxLimit > 0, "Schedule:MaxLimit must be positive")
            .Validate(
                x => x.DefaultLimit > 0 && x.DefaultLimit <= x.MaxLimit,
                "Schedule:DefaultLimit must be between 1 and Schedule:MaxLimit")
            .ValidateOnStart();

        services
            .AddOptions<IdempotencyOptions>()
            .BindConfiguration(IdempotencyOptions.SectionName)
            .Validate(x => x.RetentionSeconds > 0, "Idempotency:RetentionSeconds must be positive")
            .Validate(x => x.CleanupIntervalSeconds > 0, "Idempotency:CleanupIntervalSeconds must be positive")
            .Validate(x => x.CleanupBatchSize > 0, "Idempotency:CleanupBatchSize must be positive")
            .ValidateOnStart();

        services
            .AddOptions<CacheOptions>()
            .BindConfiguration(CacheOptions.SectionName)
            .Validate(x => x.EnrollmentCardTtlSeconds > 0, "Cache:EnrollmentCardTtlSeconds must be positive")
            .ValidateOnStart();

        services
            .AddOptions<RateLimitOptions>()
            .BindConfiguration(RateLimitOptions.SectionName)
            .Validate(x => x.PermitLimit > 0, "RateLimit:PermitLimit must be positive")
            .Validate(x => x.WindowSeconds > 0, "RateLimit:WindowSeconds must be positive")
            .Validate(x => x.RedisBreakDurationSeconds > 0, "RateLimit:RedisBreakDurationSeconds must be positive")
            .ValidateOnStart();

        return services;
    }
}
