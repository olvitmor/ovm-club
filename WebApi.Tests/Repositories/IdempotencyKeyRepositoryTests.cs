using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Repositories;

[TestClass]
public class IdempotencyKeyRepositoryTests : DatabaseTestBase
{
    private static readonly DateTime Now = new(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExpiredBefore = Now.AddDays(-1);

    [TestMethod]
    public async Task AcquireInsertsNewKey()
    {
        var user = await Given.User();

        var acquired = await InUnitOfWork(unitOfWork =>
            unitOfWork.IdempotencyKey.TryAcquireAsync(Key(user, "k1", "h1", Now), ExpiredBefore));

        Assert.IsNotNull(acquired);
        Assert.IsTrue(acquired.Id > 0);
        Assert.AreEqual("h1", acquired.RequestHash);
        Assert.IsNull(acquired.ResponseBody);
        Assert.AreEqual(1, await KeyCount(user));
    }

    [TestMethod]
    public async Task AcquireOfFreshExistingKeyReturnsNullAndKeepsRow()
    {
        var user = await Given.User();
        await Store(Key(user, "k1", "h1", Now.AddHours(-1), "{\"id\":1}"));

        var acquired = await InUnitOfWork(unitOfWork =>
            unitOfWork.IdempotencyKey.TryAcquireAsync(Key(user, "k1", "h2", Now), ExpiredBefore));

        Assert.IsNull(acquired);
        var stored = await Get(user, "k1");
        Assert.AreEqual("h1", stored.RequestHash);
        Assert.AreEqual("{\"id\": 1}", stored.ResponseBody);
    }

    [TestMethod]
    public async Task AcquireOfExpiredKeyOverwritesIt()
    {
        var user = await Given.User();
        await Store(Key(user, "k1", "h1", ExpiredBefore.AddSeconds(-1), "{\"id\":1}"));

        var acquired = await InUnitOfWork(unitOfWork =>
            unitOfWork.IdempotencyKey.TryAcquireAsync(Key(user, "k1", "h2", Now), ExpiredBefore));

        Assert.IsNotNull(acquired);
        var stored = await Get(user, "k1");
        Assert.AreEqual("h2", stored.RequestHash);
        Assert.IsNull(stored.ResponseBody);
        Assert.AreEqual(Now, stored.CreatedAt);
        Assert.AreEqual(1, await KeyCount(user));
    }

    [TestMethod]
    public async Task SameKeyOfOtherUserOrOperationIsIndependent()
    {
        var user = await Given.User();
        var other = await Given.User();
        await Store(Key(user, "k1", "h1", Now));

        Assert.IsNotNull(await InUnitOfWork(unitOfWork =>
            unitOfWork.IdempotencyKey.TryAcquireAsync(Key(other, "k1", "h1", Now), ExpiredBefore)));
        Assert.IsNotNull(await InUnitOfWork(unitOfWork =>
            unitOfWork.IdempotencyKey.TryAcquireAsync(Key(user, "k1", "h1", Now, operation: "join-waitlist"), ExpiredBefore)));
    }

    [TestMethod]
    public async Task GetFindsKeyByUserOperationAndKey()
    {
        var user = await Given.User();
        await Store(Key(user, "k1", "h1", Now));

        Assert.IsNotNull(await InUnitOfWork(unitOfWork => unitOfWork.IdempotencyKey.GetAsync(user.Id, "create-enrollment", "k1")));
        Assert.IsNull(await InUnitOfWork(unitOfWork => unitOfWork.IdempotencyKey.GetAsync(user.Id, "join-waitlist", "k1")));
        Assert.IsNull(await InUnitOfWork(unitOfWork => unitOfWork.IdempotencyKey.GetAsync(user.Id, "create-enrollment", "k2")));
    }

    [TestMethod]
    public async Task DeleteExpiredRemovesOnlyOlderKeysInBatches()
    {
        var user = await Given.User();
        var cutoff = new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        await Store(
            Key(user, "old1", "h", cutoff.AddDays(-1)),
            Key(user, "old2", "h", cutoff.AddDays(-1)),
            Key(user, "old3", "h", cutoff.AddSeconds(-1)),
            Key(user, "edge", "h", cutoff));

        Assert.AreEqual(2, await InUnitOfWork(unitOfWork => unitOfWork.IdempotencyKey.DeleteExpiredAsync(cutoff, 2)));
        Assert.AreEqual(1, await InUnitOfWork(unitOfWork => unitOfWork.IdempotencyKey.DeleteExpiredAsync(cutoff, 2)));
        Assert.AreEqual(0, await InUnitOfWork(unitOfWork => unitOfWork.IdempotencyKey.DeleteExpiredAsync(cutoff, 2)));
        Assert.AreEqual("edge", (await Get(user, "edge")).Key);
    }

    private static IdempotencyKey Key(
        TestUser user,
        string key,
        string hash,
        DateTime createdAt,
        string? response = null,
        string operation = "create-enrollment")
    {
        return new IdempotencyKey
        {
            UserId = user.Id,
            Operation = operation,
            Key = key,
            RequestHash = hash,
            ResponseBody = response,
            CreatedAt = createdAt
        };
    }

    private static Task<int> Store(params IdempotencyKey[] keys)
    {
        return InUnitOfWork(async unitOfWork =>
        {
            foreach (var key in keys)
            {
                await unitOfWork.IdempotencyKey.CreateAsync(key);
            }

            await unitOfWork.SaveChangesAsync();

            return keys.Length;
        });
    }

    private static Task<IdempotencyKey> Get(TestUser user, string key)
    {
        return InUnitOfWork(unitOfWork => unitOfWork.IdempotencyKey.Query()
            .AsNoTracking()
            .SingleAsync(x => x.UserId == user.Id && x.Key == key));
    }

    private static Task<int> KeyCount(TestUser user)
    {
        return InUnitOfWork(unitOfWork => unitOfWork.IdempotencyKey.Query().CountAsync(x => x.UserId == user.Id));
    }
}
