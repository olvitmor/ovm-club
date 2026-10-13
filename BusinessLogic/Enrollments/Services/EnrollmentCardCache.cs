using BusinessLogic.Options;
using Common.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLogic.Enrollments;

public sealed class EnrollmentCardCache
{
    private readonly ICacheService _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<EnrollmentCardCache> _logger;

    public EnrollmentCardCache(ICacheService cache, IOptions<CacheOptions> options, ILogger<EnrollmentCardCache> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EnrollmentCard?> GetOrLoadAsync(
        long userId,
        long enrollmentId,
        Func<Task<EnrollmentCard?>> load,
        CancellationToken ct)
    {
        string key;

        try
        {
            key = CardKey(userId, await _cache.GetAsync<long>(VersionKey(userId), ct), enrollmentId);

            var cached = await _cache.GetAsync<EnrollmentCard>(key, ct);

            if (cached != null)
            {
                return cached;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Enrollment card cache is unavailable, reading from database");

            return await load();
        }

        var card = await load();

        if (card != null)
        {
            try
            {
                await _cache.SetAsync(key, card, _options.EnrollmentCardTtl, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to cache enrollment card {EnrollmentId}", enrollmentId);
            }
        }

        return card;
    }

    public async Task InvalidateAsync(IEnumerable<long> userIds, CancellationToken ct)
    {
        foreach (var userId in userIds.Distinct())
        {
            try
            {
                await _cache.IncrementAsync(VersionKey(userId), ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to invalidate enrollment cards of user {UserId}", userId);
            }
        }
    }

    private static string VersionKey(long userId)
    {
        return $"card-ver:{userId}";
    }

    private static string CardKey(long userId, long version, long enrollmentId)
    {
        return $"card:{userId}:{version}:{enrollmentId}";
    }
}
