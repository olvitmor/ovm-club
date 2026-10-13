using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using StackExchange.Redis;
using WebApi.RateLimiting;

namespace WebApi.Tests.RateLimiting;

[TestClass]
public class UserRateLimiterTests
{
    [TestMethod]
    public async Task BrokenRedisIsSkippedUntilBreakDurationPasses()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        var time = new FakeTimeProvider();
        using var limiter = new UserRateLimiter(
            redis.Object,
            time,
            Options.Create(new RateLimitOptions { PermitLimit = 60, WindowSeconds = 60, RedisBreakDurationSeconds = 30 }),
            NullLogger<UserRateLimiter>.Instance);

        Assert.IsNull(await limiter.TryAcquireAsync(1));
        Assert.IsNull(await limiter.TryAcquireAsync(1));
        time.Advance(TimeSpan.FromSeconds(29));
        Assert.IsNull(await limiter.TryAcquireAsync(1));

        VerifyRedisCalls(database, Times.Once());

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.IsNull(await limiter.TryAcquireAsync(1));

        VerifyRedisCalls(database, Times.Exactly(2));
    }

    private static void VerifyRedisCalls(Mock<IDatabase> database, Times times)
    {
        database.Verify(
            x => x.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()),
            times);
    }
}
