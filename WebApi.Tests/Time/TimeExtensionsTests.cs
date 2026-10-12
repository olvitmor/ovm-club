using BusinessLogic.Time;
using Microsoft.Extensions.Time.Testing;

namespace WebApi.Tests.Time;

[TestClass]
public class TimeExtensionsTests
{
    [TestMethod]
    public void TruncatesToMicroseconds()
    {
        var value = new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc).AddTicks(1234567);

        var truncated = value.TruncateToMicroseconds();

        Assert.AreEqual(new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc).AddTicks(1234560), truncated);
        Assert.AreEqual(DateTimeKind.Utc, truncated.Kind);
    }

    [TestMethod]
    public void TreatsUnspecifiedAsUtc()
    {
        var value = new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Unspecified);

        var truncated = value.TruncateToMicroseconds();

        Assert.AreEqual(DateTimeKind.Utc, truncated.Kind);
        Assert.AreEqual(value.Ticks, truncated.Ticks);
    }

    [TestMethod]
    public void ConvertsLocalToUtc()
    {
        var utc = new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual(utc, utc.ToLocalTime().TruncateToMicroseconds());
    }

    [TestMethod]
    public void UtcNowIsTruncated()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 22, 23, 59, 59, TimeSpan.Zero).AddTicks(9999999));

        var now = time.GetUtcNowTruncated();

        Assert.AreEqual(0, now.Ticks % 10);
        Assert.AreEqual(DateTimeKind.Utc, now.Kind);
    }

    [TestMethod]
    public void UtcTodayIgnoresOffset()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.FromHours(3)));

        Assert.AreEqual(new DateOnly(2026, 9, 22), time.GetUtcToday());
    }
}
