using DataAccessLayer.Entities;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Repositories;

[TestClass]
public class WaitlistRepositoryTests : DatabaseTestBase
{
    [TestMethod]
    public async Task WaitingAreOrderedByCreationAndFilteredBySeats()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var otherClass = await Given.Class(TimeSpan.FromHours(3));
        var time = DateTime.UtcNow.AddMinutes(-10);
        var late = await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 1, createdAt: time.AddMinutes(2));
        var sameTimeFirst = await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 2, createdAt: time);
        var sameTimeSecond = await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 1, createdAt: time);
        await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 3, createdAt: time);
        await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 1, status: WaitlistStatus.Cancelled);
        await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 1, status: WaitlistStatus.Promoted);
        await Given.WaitlistEntry(await Given.User(), otherClass, seats: 1);

        var waiting = await InUnitOfWork(unitOfWork => unitOfWork.Waitlist.GetWaitingAsync(scheduledClass.Id, 2));

        CollectionAssert.AreEqual(
            new[] { sameTimeFirst.Id, sameTimeSecond.Id, late.Id },
            waiting.Select(x => x.Id).ToArray());
    }

    [TestMethod]
    public async Task PositionCountsOnlyWaitingAhead()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var time = DateTime.UtcNow.AddMinutes(-10);
        await Given.WaitlistEntry(await Given.User(), scheduledClass, createdAt: time);
        await Given.WaitlistEntry(await Given.User(), scheduledClass, status: WaitlistStatus.Cancelled, createdAt: time);
        var sameTime = await Given.WaitlistEntry(await Given.User(), scheduledClass, createdAt: time);
        var last = await Given.WaitlistEntry(await Given.User(), scheduledClass, createdAt: time.AddMinutes(1));

        Assert.AreEqual(2, await InUnitOfWork(unitOfWork => unitOfWork.Waitlist.GetPositionAsync(sameTime)));
        Assert.AreEqual(3, await InUnitOfWork(unitOfWork => unitOfWork.Waitlist.GetPositionAsync(last)));
    }

    [TestMethod]
    public async Task IsWaitingIgnoresLeftAndPromotedEntries()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        await Given.WaitlistEntry(user, scheduledClass, status: WaitlistStatus.Cancelled);
        await Given.WaitlistEntry(user, scheduledClass, status: WaitlistStatus.Promoted);

        Assert.IsFalse(await InUnitOfWork(unitOfWork => unitOfWork.Waitlist.IsWaitingAsync(user.Id, scheduledClass.Id)));

        await Given.WaitlistEntry(user, scheduledClass);

        Assert.IsTrue(await InUnitOfWork(unitOfWork => unitOfWork.Waitlist.IsWaitingAsync(user.Id, scheduledClass.Id)));
    }
}
