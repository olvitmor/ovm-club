using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessLogic.Time;

namespace WebApi.Time;

public class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return IsoTimeParser.TryParseUtcDateTime(reader.GetString(), out var value)
            ? value
            : throw new JsonException("Expected an ISO 8601 date and time");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.TruncateToMicroseconds());
    }
}
