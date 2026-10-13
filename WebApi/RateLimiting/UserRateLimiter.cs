using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace WebApi.RateLimiting;

public sealed class UserRateLimiter : IDisposable
{
    private const string SlidingWindowScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, now - ARGV[1])
        if redis.call('ZCARD', KEYS[1]) >= tonumber(ARGV[2]) then
          local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
          return oldest[2] + ARGV[1] - now
        end
        redis.call('ZADD', KEYS[1], now, ARGV[3])
        redis.call('PEXPIRE', KEYS[1], ARGV[1])
        return 0
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeProvider _timeProvider;
    private readonly RateLimitOptions _options;
    private readonly ILogger<UserRateLimiter> _logger;
    private readonly PartitionedRateLimiter<long> _fallback;
    private readonly Lock _breakerLock = new();
    private DateTimeOffset _redisBrokenUntil = DateTimeOffset.MinValue;

    public UserRateLimiter(
        IConnectionMultiplexer redis,
        TimeProvider timeProvider,
        IOptions<RateLimitOptions> options,
        ILogger<UserRateLimiter> logger)
    {
        _redis = redis;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
        _fallback = PartitionedRateLimiter.Create<long, long>(userId => RateLimitPartition.GetSlidingWindowLimiter(
            userId,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = _options.PermitLimit,
                Window = _options.Window,
                SegmentsPerWindow = _options.WindowSeconds,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public async Task<TimeSpan?> TryAcquireAsync(long userId)
    {
        if (!IsRedisBroken())
        {
            try
            {
                var retryAfterMs = (long)await _redis.GetDatabase().ScriptEvaluateAsync(
                    SlidingWindowScript,
                    [new RedisKey($"ratelimit:{userId}")],
                    [(long)_options.Window.TotalMilliseconds, _options.PermitLimit, Guid.NewGuid().ToString("N")]);

                return retryAfterMs > 0 ? TimeSpan.FromMilliseconds(retryAfterMs) : null;
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                _logger.LogWarning(
                    exception,
                    "Rate limit storage is unavailable, using in-memory limit for {BreakDuration}",
                    _options.RedisBreakDuration);

                BreakRedis();
            }
        }

        using var lease = _fallback.AttemptAcquire(userId);

        if (lease.IsAcquired)
        {
            return null;
        }

        return lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : _options.Window;
    }

    public void Dispose()
    {
        _fallback.Dispose();
    }

    private bool IsRedisBroken()
    {
        lock (_breakerLock)
        {
            return _timeProvider.GetUtcNow() < _redisBrokenUntil;
        }
    }

    private void BreakRedis()
    {
        lock (_breakerLock)
        {
            _redisBrokenUntil = _timeProvider.GetUtcNow() + _options.RedisBreakDuration;
        }
    }
}
