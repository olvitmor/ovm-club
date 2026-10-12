namespace WebApi.Tests.Repositories;

[TestClass]
public class PassRepositoryTests : RepositoryTestBase
{
    [TestMethod]
    public async Task LockReturnsRequestedPassesOrderedById()
    {
        var first = await Given.User(visits: 1);
        var second = await Given.User(visits: 2);
        var third = await Given.User(visits: 3);

        var passes = await InUnitOfWork(async unitOfWork =>
        {
            await using var transaction = await unitOfWork.DbContext.Database.BeginTransactionAsync();

            return await unitOfWork.Pass.LockByUsersAsync([third.Id, first.Id, third.Id]);
        });

        CollectionAssert.AreEqual(new[] { first.PassId, third.PassId }, passes.Select(x => x.Id).ToArray());
        CollectionAssert.DoesNotContain(passes.Select(x => x.UserId).ToArray(), second.Id);
    }

    [TestMethod]
    public async Task LockOfNoUsersReturnsEmpty()
    {
        var passes = await InUnitOfWork(unitOfWork => unitOfWork.Pass.LockByUsersAsync([]));

        Assert.AreEqual(0, passes.Count);
    }

    [TestMethod]
    public async Task SecondLockOfSamePassWaits()
    {
        var user = await Given.User();
        var other = await Given.User();

        var waited = await SecondTransactionWaits(
            unitOfWork => unitOfWork.Pass.LockByUsersAsync([user.Id]),
            unitOfWork => unitOfWork.Pass.LockByUsersAsync([other.Id, user.Id]));

        Assert.IsTrue(waited);
    }

    [TestMethod]
    public async Task LockOfOtherPassDoesNotWait()
    {
        var user = await Given.User();
        var other = await Given.User();

        var waited = await SecondTransactionWaits(
            unitOfWork => unitOfWork.Pass.LockByUsersAsync([user.Id]),
            unitOfWork => unitOfWork.Pass.LockByUsersAsync([other.Id]));

        Assert.IsFalse(waited);
    }
}
