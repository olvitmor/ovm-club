using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Tests.Repositories;

[TestClass]
public class ClassRepositoryTests : RepositoryTestBase
{
    [TestMethod]
    public async Task LockFindsClass()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 7);

        var locked = await LockInTransaction(scheduledClass.Id);

        Assert.IsNotNull(locked);
        Assert.AreEqual(7, locked.Capacity);
    }

    [TestMethod]
    public async Task LockFindsClassCancelledByClub()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), cancelled: true);

        var locked = await LockInTransaction(scheduledClass.Id);

        Assert.IsNotNull(locked);
        Assert.IsTrue(locked.IsCancelled);
    }

    [TestMethod]
    public async Task LockReturnsNullForUnknownClass()
    {
        Assert.IsNull(await LockInTransaction(long.MaxValue));
    }

    [TestMethod]
    public async Task LockedClassIsTracked()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 5);

        await InUnitOfWork(async unitOfWork =>
        {
            await using var transaction = await unitOfWork.DbContext.Database.BeginTransactionAsync();
            var locked = await unitOfWork.Class.LockAsync(scheduledClass.Id);

            locked!.Capacity = 6;
            await unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        });

        var stored = await Factory.QueryAsync(db => db.Classes.IgnoreQueryFilters().SingleAsync(x => x.Id == scheduledClass.Id));
        Assert.AreEqual(6, stored.Capacity);
    }

    [TestMethod]
    public async Task SecondLockOfSameClassWaits()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        var waited = await SecondTransactionWaits(
            unitOfWork => unitOfWork.Class.LockAsync(scheduledClass.Id),
            unitOfWork => unitOfWork.Class.LockAsync(scheduledClass.Id));

        Assert.IsTrue(waited);
    }

    [TestMethod]
    public async Task LockOfOtherClassDoesNotWait()
    {
        var first = await Given.Class(TimeSpan.FromHours(3));
        var second = await Given.Class(TimeSpan.FromHours(3));

        var waited = await SecondTransactionWaits(
            unitOfWork => unitOfWork.Class.LockAsync(first.Id),
            unitOfWork => unitOfWork.Class.LockAsync(second.Id));

        Assert.IsFalse(waited);
    }

    private static Task<Class?> LockInTransaction(long id)
    {
        return InUnitOfWork(async unitOfWork =>
        {
            await using var transaction = await unitOfWork.DbContext.Database.BeginTransactionAsync();

            return await unitOfWork.Class.LockAsync(id);
        });
    }
}
