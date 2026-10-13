using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Repositories;

[TestClass]
public class EnrollmentRepositoryTests : DatabaseTestBase
{
    [TestMethod]
    public async Task ActiveSeatsCountOnlyActiveEnrollmentsOfClass()
    {
        var user = await Given.User();
        var other = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var otherClass = await Given.Class(TimeSpan.FromHours(3));
        await Given.Enrollment(user, scheduledClass, seats: 2);
        await Given.Enrollment(other, scheduledClass, seats: 3);
        await Given.Enrollment(user, scheduledClass, seats: 4, cancelled: true);
        await Given.Enrollment(user, otherClass, seats: 5);

        var seats = await InUnitOfWork(unitOfWork => unitOfWork.Enrollment.GetActiveSeatsAsync(scheduledClass.Id));

        Assert.AreEqual(5, seats);
    }

    [TestMethod]
    public async Task ActiveSeatsOfClassWithoutEnrollmentsIsZero()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        await Given.Enrollment(user, scheduledClass, seats: 3, cancelled: true);

        var seats = await InUnitOfWork(unitOfWork => unitOfWork.Enrollment.GetActiveSeatsAsync(scheduledClass.Id));

        Assert.AreEqual(0, seats);
    }

    [TestMethod]
    public async Task ActiveSeatsCountEnrollmentsOfClassCancelledByClub()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), cancelled: true);
        await Given.Enrollment(user, scheduledClass, seats: 2);

        var seats = await InUnitOfWork(unitOfWork => unitOfWork.Enrollment.GetActiveSeatsAsync(scheduledClass.Id));

        Assert.AreEqual(2, seats);
    }

    [TestMethod]
    public async Task ActiveSeatsForSeveralClassesIncludeEveryRequestedClass()
    {
        var user = await Given.User();
        var busy = await Given.Class(TimeSpan.FromHours(3));
        var empty = await Given.Class(TimeSpan.FromHours(3));
        await Given.Enrollment(user, busy, seats: 2);
        await Given.Enrollment(user, busy, seats: 1);

        var seats = await InUnitOfWork(unitOfWork =>
            unitOfWork.Enrollment.GetActiveSeatsAsync([busy.Id, empty.Id, busy.Id]));

        Assert.AreEqual(2, seats.Count);
        Assert.AreEqual(3, seats[busy.Id]);
        Assert.AreEqual(0, seats[empty.Id]);
    }
}
