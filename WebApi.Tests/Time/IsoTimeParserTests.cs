using WebApi.Time;

namespace WebApi.Tests.Time;

[TestClass]
public class IsoTimeParserTests
{
    [TestMethod]
    [DataRow("2026-09-22T10:00:00", "2026-09-22T10:00:00.0000000Z")]
    [DataRow("2026-09-22T10:00", "2026-09-22T10:00:00.0000000Z")]
    [DataRow("2026-09-22T10:00:00Z", "2026-09-22T10:00:00.0000000Z")]
    [DataRow("2026-09-22T10:00:00+03:00", "2026-09-22T07:00:00.0000000Z")]
    [DataRow("2026-09-22T01:00:00+03:00", "2026-09-21T22:00:00.0000000Z")]
    [DataRow("2026-09-22T10:00:00-05:30", "2026-09-22T15:30:00.0000000Z")]
    [DataRow("2026-09-22T10:00:00.1234567Z", "2026-09-22T10:00:00.1234567Z")]
    [DataRow("2026-09-22T10:00:00.5", "2026-09-22T10:00:00.5000000Z")]
    public void ParsesDateTimeToUtc(string input, string expected)
    {
        Assert.IsTrue(IsoTimeParser.TryParseUtcDateTime(input, out var result));
        Assert.AreEqual(DateTimeKind.Utc, result.Kind);
        Assert.AreEqual(expected, result.ToString("O"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("2026-09-22")]
    [DataRow("2026-09-22T")]
    [DataRow("2026-09-22 10:00:00")]
    [DataRow("22.09.2026 10:00")]
    [DataRow("09/22/2026 10:00")]
    [DataRow("2026-13-01T10:00:00")]
    [DataRow("2026-09-22T25:00:00")]
    [DataRow("2026-09-22T10:00:00 03:00")]
    [DataRow("tomorrow")]
    public void RejectsInvalidDateTime(string? input)
    {
        Assert.IsFalse(IsoTimeParser.TryParseUtcDateTime(input, out _));
    }

    [TestMethod]
    public void ParsesDate()
    {
        Assert.IsTrue(IsoTimeParser.TryParseDate("2026-09-22", out var result));
        Assert.AreEqual(new DateOnly(2026, 9, 22), result);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("2026-09-22T00:00:00")]
    [DataRow("2026-09-22T00:00:00Z")]
    [DataRow("2026-09-22 00:00")]
    [DataRow("22.09.2026")]
    [DataRow("2026-9-22")]
    [DataRow("2026-02-30")]
    public void RejectsInvalidDate(string? input)
    {
        Assert.IsFalse(IsoTimeParser.TryParseDate(input, out _));
    }
}
