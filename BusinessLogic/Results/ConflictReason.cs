using System.Text.Json.Serialization;

namespace BusinessLogic.Results;

public enum ConflictReason
{
    [JsonStringEnumMemberName("capacity-exceeded")]
    CapacityExceeded,

    [JsonStringEnumMemberName("pass-exhausted")]
    PassExhausted,

    [JsonStringEnumMemberName("pass-expired")]
    PassExpired,

    [JsonStringEnumMemberName("class-started")]
    ClassStarted,

    [JsonStringEnumMemberName("class-cancelled")]
    ClassCancelled,

    [JsonStringEnumMemberName("enrollment-cancelled")]
    EnrollmentCancelled,

    [JsonStringEnumMemberName("seats-available")]
    SeatsAvailable,

    [JsonStringEnumMemberName("already-waiting")]
    AlreadyWaiting,

    [JsonStringEnumMemberName("waitlist-closed")]
    WaitlistClosed,

    [JsonStringEnumMemberName("already-promoted")]
    AlreadyPromoted,

    [JsonStringEnumMemberName("idempotency-key-reused")]
    IdempotencyKeyReused
}
