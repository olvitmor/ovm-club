using BusinessLogic.Booking;

namespace WebApi.Tests.Booking;

[TestClass]
public class WaitlistPolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);
    private static readonly TimeSpan CloseBefore = TimeSpan.FromHours(1);

    [TestMethod]
    public void WaitlistClosesExactlyCloseBeforeStart()
    {
        Assert.IsTrue(WaitlistPolicy.IsOpen(false, Now.Add(CloseBefore).AddTicks(10), Now, CloseBefore));
        Assert.IsFalse(WaitlistPolicy.IsOpen(false, Now.Add(CloseBefore), Now, CloseBefore));
    }

    [TestMethod]
    public void WaitlistOfCancelledClassIsClosed()
    {
        Assert.IsFalse(WaitlistPolicy.IsOpen(true, Now.AddDays(1), Now, CloseBefore));
    }

    [TestMethod]
    public void FirstFittingCandidateGetsSeatsAndFirstStaysFirst()
    {
        var promoted = WaitlistPolicy.SelectPromotions([Candidate(1, seats: 3), Candidate(2, seats: 2)], 2, Today);

        CollectionAssert.AreEqual(new long[] { 2 }, promoted.ToArray());
    }

    [TestMethod]
    public void FreeSeatsShrinkWithEachPromotion()
    {
        var promoted = WaitlistPolicy.SelectPromotions(
            [Candidate(1, seats: 2), Candidate(2, seats: 2), Candidate(3, seats: 1)],
            3,
            Today);

        CollectionAssert.AreEqual(new long[] { 1, 3 }, promoted.ToArray());
    }

    [TestMethod]
    public void CandidateWithBadPassIsSkipped()
    {
        var promoted = WaitlistPolicy.SelectPromotions(
            [
                Candidate(1, seats: 1, validUntil: Today.AddDays(-1)),
                Candidate(2, seats: 2, visits: 1),
                Candidate(3, seats: 1)
            ],
            1,
            Today);

        CollectionAssert.AreEqual(new long[] { 3 }, promoted.ToArray());
    }

    [TestMethod]
    public void PassValidTodayIsEnough()
    {
        var promoted = WaitlistPolicy.SelectPromotions([Candidate(1, seats: 1, validUntil: Today, visits: 1)], 1, Today);

        CollectionAssert.AreEqual(new long[] { 1 }, promoted.ToArray());
    }

    [TestMethod]
    public void NoFreeSeatsPromotesNobody()
    {
        Assert.AreEqual(0, WaitlistPolicy.SelectPromotions([Candidate(1, seats: 1)], 0, Today).Count);
    }

    private static WaitlistCandidate Candidate(long id, int seats, DateOnly? validUntil = null, int visits = 10)
    {
        return new WaitlistCandidate(id, seats, validUntil ?? Today.AddDays(30), visits);
    }
}
