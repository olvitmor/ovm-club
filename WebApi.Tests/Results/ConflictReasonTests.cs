using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessLogic.Results;

namespace WebApi.Tests.Results;

[TestClass]
public class ConflictReasonTests
{
    private static readonly Dictionary<ConflictReason, string> ContractCodes = new()
    {
        [ConflictReason.CapacityExceeded] = "capacity-exceeded",
        [ConflictReason.PassExhausted] = "pass-exhausted",
        [ConflictReason.PassExpired] = "pass-expired",
        [ConflictReason.ClassStarted] = "class-started",
        [ConflictReason.ClassCancelled] = "class-cancelled",
        [ConflictReason.EnrollmentCancelled] = "enrollment-cancelled",
        [ConflictReason.SeatsAvailable] = "seats-available",
        [ConflictReason.AlreadyWaiting] = "already-waiting",
        [ConflictReason.WaitlistClosed] = "waitlist-closed",
        [ConflictReason.AlreadyPromoted] = "already-promoted",
        [ConflictReason.IdempotencyKeyReused] = "idempotency-key-reused"
    };

    [TestMethod]
    public void EveryReasonHasExpectedCode()
    {
        CollectionAssert.AreEquivalent(ContractCodes.Keys.ToList(), Enum.GetValues<ConflictReason>());

        foreach (var (reason, code) in ContractCodes)
        {
            Assert.AreEqual(code, reason.ToCode(), reason.ToString());
        }
    }

    [TestMethod]
    public void JsonSerializationMatchesCode()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };

        foreach (var reason in Enum.GetValues<ConflictReason>())
        {
            Assert.AreEqual($"\"{reason.ToCode()}\"", JsonSerializer.Serialize(reason, options));
        }
    }

    [TestMethod]
    public void UnknownValueThrows()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ((ConflictReason)999).ToCode());
    }
}
