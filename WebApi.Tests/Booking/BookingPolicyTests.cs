using BusinessLogic.Booking;
using BusinessLogic.Results;

namespace WebApi.Tests.Booking;

[TestClass]
public class BookingPolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    [TestMethod]
    public void OpenClassPasses()
    {
        Assert.IsNull(BookingPolicy.CheckClassOpen(false, Now.AddTicks(10), Now));
    }

    [TestMethod]
    public void ClassStartingNowIsStarted()
    {
        Assert.AreEqual(ConflictReason.ClassStarted, BookingPolicy.CheckClassOpen(false, Now, Now));
        Assert.AreEqual(ConflictReason.ClassStarted, BookingPolicy.CheckClassOpen(false, Now.AddHours(-1), Now));
    }

    [TestMethod]
    public void CancellationWinsOverStart()
    {
        Assert.AreEqual(ConflictReason.ClassCancelled, BookingPolicy.CheckClassOpen(true, Now.AddHours(-1), Now));
        Assert.AreEqual(ConflictReason.ClassCancelled, BookingPolicy.CheckClassOpen(true, Now.AddHours(1), Now));
    }

    [TestMethod]
    public void PassValidThroughLastDayInclusive()
    {
        Assert.IsNull(BookingPolicy.CheckPassCovers(Today, 1, 1, Today));
        Assert.AreEqual(ConflictReason.PassExpired, BookingPolicy.CheckPassCovers(Today.AddDays(-1), 1, 1, Today));
    }

    [TestMethod]
    public void PassNeedsEnoughVisits()
    {
        Assert.IsNull(BookingPolicy.CheckPassCovers(Today, 3, 3, Today));
        Assert.AreEqual(ConflictReason.PassExhausted, BookingPolicy.CheckPassCovers(Today, 2, 3, Today));
        Assert.AreEqual(ConflictReason.PassExhausted, BookingPolicy.CheckPassCovers(Today, 0, 1, Today));
    }

    [TestMethod]
    public void ExpiryWinsOverExhaustion()
    {
        Assert.AreEqual(ConflictReason.PassExpired, BookingPolicy.CheckPassCovers(Today.AddDays(-1), 0, 1, Today));
    }

    [TestMethod]
    public void CapacityAllowsExactFill()
    {
        Assert.IsNull(BookingPolicy.CheckCapacity(10, 8, 2));
        Assert.AreEqual(ConflictReason.CapacityExceeded, BookingPolicy.CheckCapacity(10, 9, 2));
        Assert.AreEqual(ConflictReason.CapacityExceeded, BookingPolicy.CheckCapacity(10, 10, 1));
    }

    [TestMethod]
    public void CapacityDoesNotOverflow()
    {
        Assert.AreEqual(ConflictReason.CapacityExceeded, BookingPolicy.CheckCapacity(10, 5, int.MaxValue));
    }
}
