using System.Text.Json;
using WebApi.Time;

namespace WebApi.Tests.Time;

[TestClass]
public class UtcDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcDateTimeJsonConverter() } };

    [TestMethod]
    public void WritesUtcWithMicrosecondPrecision()
    {
        var value = new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc).AddTicks(1234567);

        Assert.AreEqual("\"2026-09-22T10:00:00.123456Z\"", JsonSerializer.Serialize(value, Options));
    }

    [TestMethod]
    public void WritesWholeSecondsWithoutFraction()
    {
        var value = new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual("\"2026-09-22T10:00:00Z\"", JsonSerializer.Serialize(value, Options));
    }

    [TestMethod]
    public void WritesUnspecifiedAsUtc()
    {
        var value = new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Unspecified);

        Assert.AreEqual("\"2026-09-22T10:00:00Z\"", JsonSerializer.Serialize(value, Options));
    }

    [TestMethod]
    public void ReadsOffsetAsUtc()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-09-22T10:00:00+03:00\"", Options);

        Assert.AreEqual(new DateTime(2026, 9, 22, 7, 0, 0, DateTimeKind.Utc), value);
        Assert.AreEqual(DateTimeKind.Utc, value.Kind);
    }

    [TestMethod]
    public void RejectsDateWithoutTime()
    {
        Assert.ThrowsException<JsonException>(() => JsonSerializer.Deserialize<DateTime>("\"2026-09-22\"", Options));
    }
}
