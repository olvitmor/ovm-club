using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessLogic.Schedule.GetSchedule;

namespace WebApi.Contracts.Schedule.GetSchedule;

public static class ScheduleToken
{
    public static string Encode(ScheduleCursor cursor, DateTime from, DateTime to, long? roomId)
    {
        var payload = new Payload(cursor.StartsAt, cursor.Id, FiltersHash(from, to, roomId));

        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
    }

    public static bool TryDecode(string token, DateTime from, DateTime to, long? roomId, out ScheduleCursor? cursor)
    {
        cursor = null;

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(Base64Url.DecodeFromChars(token));

            if (payload == null || payload.StartsAt.Kind != DateTimeKind.Utc || payload.Filters != FiltersHash(from, to, roomId))
            {
                return false;
            }

            cursor = new ScheduleCursor(payload.StartsAt, payload.Id);

            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }

    private static string FiltersHash(DateTime from, DateTime to, long? roomId)
    {
        var filters = $"{from.Ticks}|{to.Ticks}|{roomId}";

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(filters)))[..16];
    }

    private sealed record Payload(
        [property: JsonPropertyName("s")] DateTime StartsAt,
        [property: JsonPropertyName("i")] long Id,
        [property: JsonPropertyName("f")] string Filters);
}
