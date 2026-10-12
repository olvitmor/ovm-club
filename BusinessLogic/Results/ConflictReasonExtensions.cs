using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json.Serialization;

namespace BusinessLogic.Results;

public static class ConflictReasonExtensions
{
    private static readonly FrozenDictionary<ConflictReason, string> Codes = Enum
        .GetValues<ConflictReason>()
        .ToFrozenDictionary(reason => reason, ReadCode);

    public static string ToCode(this ConflictReason reason)
    {
        return Codes.TryGetValue(reason, out var code)
            ? code
            : throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
    }

    private static string ReadCode(ConflictReason reason)
    {
        var attribute = typeof(ConflictReason)
            .GetField(reason.ToString())!
            .GetCustomAttribute<JsonStringEnumMemberNameAttribute>();

        return attribute?.Name
            ?? throw new InvalidOperationException(
                $"{nameof(ConflictReason)}.{reason} has no {nameof(JsonStringEnumMemberNameAttribute)}");
    }
}
