using System.Net;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class JoinWaitlistTests
{
    private static ApiApplicationFactory _factory = null!;

    private static Given Given => _factory.Given;

    private static DateOnly Today => DateOnly.FromDateTime(_factory.Time.GetUtcNow().UtcDateTime);

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _factory = new ApiApplicationFactory();
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task JoinReturnsEntryWithPosition()
    {
        var scheduledClass = await FullClass(capacity: 2);
        var first = await Given.User();
        var second = await Given.User();

        var firstResponse = await Join(first, scheduledClass.Id, 1);
        var secondResponse = await Join(second, scheduledClass.Id, 2);

        Assert.AreEqual(HttpStatusCode.Created, firstResponse.StatusCode);
        var body = await JsonAssert.ReadAsync(secondResponse);
        JsonAssert.HasExactlyProperties(body, "id", "classId", "seats", "position", "createdAt");
        Assert.AreEqual(scheduledClass.Id, body.GetProperty("classId").GetInt64());
        Assert.AreEqual(2, body.GetProperty("seats").GetInt32());
        Assert.AreEqual(2, body.GetProperty("position").GetInt32());
        Assert.AreEqual(1, (await JsonAssert.ReadAsync(firstResponse)).GetProperty("position").GetInt32());

        var entry = await StoredEntry(body.GetProperty("id").GetInt64());
        Assert.AreEqual(WaitlistStatus.Waiting, entry.Status);
        Assert.AreEqual(second.Id, entry.UserId);
    }

    [TestMethod]
    public async Task JoinDoesNotSpendOrCheckPass()
    {
        var user = await Given.User(visits: 0, validUntil: Today.AddDays(-1));
        var scheduledClass = await FullClass(capacity: 1);

        Assert.AreEqual(HttpStatusCode.Created, (await Join(user, scheduledClass.Id, 3)).StatusCode);
        Assert.AreEqual(0, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task EnoughFreeSeatsIsConflict()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 3);
        await Given.Enrollment(await Given.User(), scheduledClass, seats: 1);

        await AssertConflict(await Given.User(), scheduledClass.Id, 2, "seats-available");
        Assert.AreEqual(HttpStatusCode.Created, (await Join(await Given.User(), scheduledClass.Id, 3)).StatusCode);
    }

    [TestMethod]
    public async Task SecondEntryOfSameUserIsConflict()
    {
        var user = await Given.User();
        var scheduledClass = await FullClass(capacity: 1);

        Assert.AreEqual(HttpStatusCode.Created, (await Join(user, scheduledClass.Id, 1)).StatusCode);
        await AssertConflict(user, scheduledClass.Id, 2, "already-waiting");
    }

    [TestMethod]
    public async Task UserWithEnrollmentCanWaitForMore()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 2);
        await Given.Enrollment(user, scheduledClass, seats: 2);

        Assert.AreEqual(HttpStatusCode.Created, (await Join(user, scheduledClass.Id, 1)).StatusCode);
    }

    [TestMethod]
    public async Task UserCanWaitAgainAfterLeaving()
    {
        var user = await Given.User();
        var scheduledClass = await FullClass(capacity: 1);
        await Given.WaitlistEntry(user, scheduledClass, status: WaitlistStatus.Cancelled);

        Assert.AreEqual(HttpStatusCode.Created, (await Join(user, scheduledClass.Id, 1)).StatusCode);
    }

    [TestMethod]
    public async Task WaitlistClosesExactlyHourBeforeStart()
    {
        var open = await FullClass(capacity: 1, startsIn: TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        var closed = await FullClass(capacity: 1, startsIn: TimeSpan.FromHours(1));

        Assert.AreEqual(HttpStatusCode.Created, (await Join(await Given.User(), open.Id, 1)).StatusCode);
        await AssertConflict(await Given.User(), closed.Id, 1, "waitlist-closed");
    }

    [TestMethod]
    public async Task ReasonsFollowOrder()
    {
        var user = await Given.User();
        var cancelledStarted = await Given.Class(TimeSpan.FromHours(-1), cancelled: true);
        var started = await Given.Class(TimeSpan.Zero, capacity: 5);
        var closed = await Given.Class(TimeSpan.FromMinutes(30), capacity: 5);
        var waitingWithFreeSeats = await Given.Class(TimeSpan.FromHours(3), capacity: 5);
        await Given.WaitlistEntry(user, cancelledStarted);
        await Given.WaitlistEntry(user, started);
        await Given.WaitlistEntry(user, closed);
        await Given.WaitlistEntry(user, waitingWithFreeSeats);

        await AssertConflict(user, cancelledStarted.Id, 1, "class-cancelled");
        await AssertConflict(user, started.Id, 1, "class-started");
        await AssertConflict(user, closed.Id, 1, "waitlist-closed");
        await AssertConflict(user, waitingWithFreeSeats.Id, 1, "already-waiting");
    }

    [TestMethod]
    public async Task UnknownClassIsNotFound()
    {
        Assert.AreEqual(HttpStatusCode.NotFound, (await Join(await Given.User(), long.MaxValue, 1)).StatusCode);
    }

    [TestMethod]
    [DataRow("""{ "classId": 1, "seats": 0 }""")]
    [DataRow("""{ "classId": 1 }""")]
    [DataRow("""{ "seats": 1 }""")]
    [DataRow("""{ "classId": "1", "seats": 1 }""")]
    [DataRow("""broken""")]
    public async Task InvalidBodyIsBadRequest(string json)
    {
        using var client = _factory.CreateClientFor(await Given.User());

        var response = await client.SendAsync(ApiClient.Post("/enrollments/waitlist", json, ApiClient.NewKey()));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task MissingIdempotencyKeyIsBadRequest()
    {
        var scheduledClass = await FullClass(capacity: 1);
        using var client = _factory.CreateClientFor(await Given.User());

        var response = await client.PostJsonAsync("/enrollments/waitlist", new { classId = scheduledClass.Id, seats = 1 });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, await WaitingCount(scheduledClass.Id));
    }

    [TestMethod]
    public async Task ParallelJoinsOfSameUserCreateOneEntry()
    {
        var user = await Given.User();
        var scheduledClass = await FullClass(capacity: 1);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Join(user, scheduledClass.Id, 1)));

        Assert.AreEqual(1, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.AreEqual(4, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        Assert.AreEqual(1, await WaitingCount(scheduledClass.Id));
    }

    private static async Task<Class> FullClass(int capacity, TimeSpan? startsIn = null)
    {
        var scheduledClass = await Given.Class(startsIn ?? TimeSpan.FromHours(3), capacity: capacity);
        await Given.Enrollment(await Given.User(), scheduledClass, seats: capacity);

        return scheduledClass;
    }

    private static async Task AssertConflict(TestUser user, long classId, int seats, string reason)
    {
        var waitingBefore = await WaitingCount(classId);

        var response = await Join(user, classId, seats);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual(reason, await response.ReadReasonAsync());
        Assert.AreEqual(waitingBefore, await WaitingCount(classId));
    }

    private static Task<WaitlistEntry> StoredEntry(long id)
    {
        return _factory.QueryAsync(db => db.WaitlistEntries.AsNoTracking().SingleAsync(x => x.Id == id));
    }

    private static Task<int> WaitingCount(long classId)
    {
        return _factory.QueryAsync(db => db.WaitlistEntries
            .CountAsync(x => x.ClassId == classId && x.Status == WaitlistStatus.Waiting));
    }

    private static Task<int> RemainingVisits(TestUser user)
    {
        return _factory.QueryAsync(db => db.Passes.Where(x => x.UserId == user.Id).Select(x => x.RemainingVisits).SingleAsync());
    }

    private static async Task<HttpResponseMessage> Join(TestUser user, long classId, int seats)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.PostJsonAsync("/enrollments/waitlist", new { classId, seats }, ApiClient.NewKey());
    }
}
