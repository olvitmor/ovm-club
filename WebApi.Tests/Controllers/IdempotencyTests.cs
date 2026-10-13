using System.Net;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Background;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class IdempotencyTests
{
    private static ApiApplicationFactory _factory = null!;

    private static Given Given => _factory.Given;

    private static DateTime Now => _factory.Time.GetUtcNow().UtcDateTime;

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _factory = new ApiApplicationFactory(new Dictionary<string, string?> { ["Idempotency:CleanupBatchSize"] = "2" });
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task RepeatReturnsFirstBodyWithoutSecondEnrollment()
    {
        var user = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var key = ApiClient.NewKey();

        var first = await Enroll(user, scheduledClass.Id, 2, key);
        var second = await Enroll(user, scheduledClass.Id, 2, key);

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, second.StatusCode);
        Assert.AreEqual(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.AreEqual(1, await EnrollmentCount(user));
        Assert.AreEqual(3, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task RepeatReturnsFirstBodyAfterStateChanged()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var key = ApiClient.NewKey();
        var first = await JsonAssert.ReadAsync(await Enroll(user, scheduledClass.Id, 1, key));

        using (var client = _factory.CreateClientFor(user))
        {
            await client.SendAsync(ApiClient.Post($"/enrollments/{first.GetProperty("id").GetInt64()}/cancel", null));
        }

        var repeat = await Enroll(user, scheduledClass.Id, 1, key);

        Assert.AreEqual(HttpStatusCode.Created, repeat.StatusCode);
        Assert.AreEqual("active", (await JsonAssert.ReadAsync(repeat)).GetProperty("status").GetString());
        Assert.AreEqual(1, await EnrollmentCount(user));
    }

    [TestMethod]
    public async Task SameKeyWithOtherBodyIsConflict()
    {
        var user = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var key = ApiClient.NewKey();
        await Enroll(user, scheduledClass.Id, 1, key);

        var response = await Enroll(user, scheduledClass.Id, 2, key);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual("idempotency-key-reused", await response.ReadReasonAsync());
        Assert.AreEqual(1, await EnrollmentCount(user));
        Assert.AreEqual(4, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task RefusalDoesNotStoreKey()
    {
        var user = await Given.User();
        var other = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 1);
        var blocking = await Given.Enrollment(other, scheduledClass, seats: 1);
        var key = ApiClient.NewKey();

        var refused = await Enroll(user, scheduledClass.Id, 1, key);
        Assert.AreEqual("capacity-exceeded", await refused.ReadReasonAsync());
        Assert.AreEqual(0, await KeyCount(user));

        using (var client = _factory.CreateClientFor(other))
        {
            await client.SendAsync(ApiClient.Post($"/enrollments/{blocking.Id}/cancel", null));
        }

        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 1, key)).StatusCode);
        Assert.AreEqual(1, await KeyCount(user));
    }

    [TestMethod]
    public async Task SameKeyOfOtherUserOrEndpointDoesNotConflict()
    {
        var first = await Given.User();
        var second = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 2);
        var key = ApiClient.NewKey();

        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(first, scheduledClass.Id, 1, key)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(second, scheduledClass.Id, 1, key)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, (await JoinWaitlist(first, scheduledClass.Id, 1, key)).StatusCode);
    }

    [TestMethod]
    public async Task VeryLongKeyWorks()
    {
        var user = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var key = new string('k', 100_000);

        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 1, key)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 1, key)).StatusCode);
        Assert.AreEqual(1, await EnrollmentCount(user));
    }

    [TestMethod]
    public async Task WaitlistRepeatReturnsFirstBody()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 1);
        await Given.Enrollment(await Given.User(), scheduledClass, seats: 1);
        var key = ApiClient.NewKey();

        var first = await JoinWaitlist(user, scheduledClass.Id, 1, key);
        await Given.WaitlistEntry(await Given.User(), scheduledClass, createdAt: Now.AddMinutes(-5));
        var second = await JoinWaitlist(user, scheduledClass.Id, 1, key);

        Assert.AreEqual(HttpStatusCode.Created, second.StatusCode);
        Assert.AreEqual(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.AreEqual(1, await _factory.QueryAsync(db => db.WaitlistEntries.CountAsync(x => x.UserId == user.Id)));
    }

    [TestMethod]
    public async Task ParallelRequestsWithSameKeyCreateOneEnrollment()
    {
        var user = await Given.User(visits: 10);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var key = ApiClient.NewKey();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Enroll(user, scheduledClass.Id, 1, key)));

        Assert.IsTrue(responses.All(x => x.StatusCode == HttpStatusCode.Created));
        Assert.AreEqual(1, (await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()))).Distinct().Count());
        Assert.AreEqual(1, await EnrollmentCount(user));
        Assert.AreEqual(9, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task CleanupDeletesOnlyExpiredKeysInBatches()
    {
        var user = await Given.User();
        var retention = TimeSpan.FromDays(1);
        var expired = Enumerable.Range(0, 5)
            .Select(i => NewKey(user, Now - retention - TimeSpan.FromSeconds(1 + i)))
            .ToList();
        var fresh = NewKey(user, Now - retention + TimeSpan.FromSeconds(1));
        await _factory.QueryAsync(async db =>
        {
            db.AddRange(expired);
            db.Add(fresh);
            return await db.SaveChangesAsync();
        });

        var deleted = await _factory.Services.GetRequiredService<IdempotencyKeyCleanupService>().CleanupAsync(default);

        Assert.IsTrue(deleted >= 5);
        Assert.AreEqual(1, await KeyCount(user));
    }

    [TestMethod]
    public async Task ExpiredKeyIsNew()
    {
        var user = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromDays(3));
        var key = ApiClient.NewKey();
        var first = await JsonAssert.ReadAsync(await Enroll(user, scheduledClass.Id, 1, key));

        _factory.Time.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));
        var second = await Enroll(user, scheduledClass.Id, 1, key);

        Assert.AreEqual(HttpStatusCode.Created, second.StatusCode);
        Assert.AreNotEqual(first.GetProperty("id").GetInt64(), (await JsonAssert.ReadAsync(second)).GetProperty("id").GetInt64());
        Assert.AreEqual(2, await EnrollmentCount(user));
    }

    private static IdempotencyKey NewKey(TestUser user, DateTime createdAt)
    {
        return new IdempotencyKey
        {
            UserId = user.Id,
            Operation = "create-enrollment",
            Key = Guid.NewGuid().ToString("N"),
            RequestHash = "hash",
            ResponseBody = "{}",
            CreatedAt = createdAt
        };
    }

    private static Task<int> KeyCount(TestUser user)
    {
        return _factory.QueryAsync(db => db.IdempotencyKeys.CountAsync(x => x.UserId == user.Id));
    }

    private static Task<int> EnrollmentCount(TestUser user)
    {
        return _factory.QueryAsync(db => db.Enrollments.CountAsync(x => x.UserId == user.Id));
    }

    private static Task<int> RemainingVisits(TestUser user)
    {
        return _factory.QueryAsync(db => db.Passes.Where(x => x.UserId == user.Id).Select(x => x.RemainingVisits).SingleAsync());
    }

    private static async Task<HttpResponseMessage> Enroll(TestUser user, long classId, int seats, string key)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.PostJsonAsync("/enrollments", new { classId, seats }, key);
    }

    private static async Task<HttpResponseMessage> JoinWaitlist(TestUser user, long classId, int seats, string key)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.PostJsonAsync("/enrollments/waitlist", new { classId, seats }, key);
    }
}
