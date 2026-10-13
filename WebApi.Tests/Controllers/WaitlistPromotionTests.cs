using System.Net;
using System.Text.Json;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class WaitlistPromotionTests
{
    private static ApiApplicationFactory _factory = null!;

    private static Given Given => _factory.Given;

    private static DateOnly Today => DateOnly.FromDateTime(_factory.Time.GetUtcNow().UtcDateTime);

    private static DateTime Earlier(int minutes) => _factory.Time.GetUtcNow().UtcDateTime.AddMinutes(-minutes);

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
    public async Task CancelPromotesWaitingIntoEnrollment()
    {
        var owner = await Given.User();
        var waiter = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 2);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 2);
        var entry = await Given.WaitlistEntry(waiter, scheduledClass, seats: 2);

        Assert.AreEqual(HttpStatusCode.OK, (await Cancel(owner, enrollment.Id)).StatusCode);

        var stored = await StoredEntry(entry.Id);
        Assert.AreEqual(WaitlistStatus.Promoted, stored.Status);
        var promoted = await StoredEnrollment(stored.EnrollmentId ?? throw new AssertFailedException("No enrollment"));
        Assert.AreEqual(waiter.Id, promoted.UserId);
        Assert.AreEqual(scheduledClass.Id, promoted.ClassId);
        Assert.AreEqual(2, promoted.Seats);
        Assert.AreEqual(EnrollmentStatus.Active, promoted.Status);
        Assert.AreEqual(3, await RemainingVisits(waiter));
        Assert.AreEqual(2, await ActiveSeats(scheduledClass.Id));
    }

    [TestMethod]
    public async Task FirstFittingGetsSeatsAndFirstKeepsWaiting()
    {
        var owner = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 3);
        await Given.Enrollment(await Given.User(), scheduledClass, seats: 2);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 1);
        var big = await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 2, createdAt: Earlier(3));
        var small = await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 1, createdAt: Earlier(2));

        await Cancel(owner, enrollment.Id);

        Assert.AreEqual(WaitlistStatus.Waiting, (await StoredEntry(big.Id)).Status);
        Assert.AreEqual(WaitlistStatus.Promoted, (await StoredEntry(small.Id)).Status);
    }

    [TestMethod]
    public async Task WaiterWithBadPassIsSkippedAndKeepsWaiting()
    {
        var owner = await Given.User();
        var expired = await Given.User(visits: 5, validUntil: Today.AddDays(-1));
        var empty = await Given.User(visits: 1);
        var good = await Given.User(visits: 2);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 2);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 2);
        var expiredEntry = await Given.WaitlistEntry(expired, scheduledClass, seats: 2, createdAt: Earlier(3));
        var emptyEntry = await Given.WaitlistEntry(empty, scheduledClass, seats: 2, createdAt: Earlier(2));
        var goodEntry = await Given.WaitlistEntry(good, scheduledClass, seats: 2, createdAt: Earlier(1));

        await Cancel(owner, enrollment.Id);

        Assert.AreEqual(WaitlistStatus.Waiting, (await StoredEntry(expiredEntry.Id)).Status);
        Assert.AreEqual(WaitlistStatus.Waiting, (await StoredEntry(emptyEntry.Id)).Status);
        Assert.AreEqual(WaitlistStatus.Promoted, (await StoredEntry(goodEntry.Id)).Status);
        Assert.AreEqual(5, await RemainingVisits(expired));
        Assert.AreEqual(1, await RemainingVisits(empty));
        Assert.AreEqual(0, await RemainingVisits(good));
    }

    [TestMethod]
    public async Task AllFreeSeatsGoToQueueNotOnlyReleased()
    {
        var owner = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 5);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 1);
        var entry = await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 4);

        await Cancel(owner, enrollment.Id);

        Assert.AreEqual(WaitlistStatus.Promoted, (await StoredEntry(entry.Id)).Status);
        Assert.AreEqual(4, await ActiveSeats(scheduledClass.Id));
    }

    [TestMethod]
    public async Task CancelHourAndHalfBeforeStartPromotes()
    {
        var (owner, enrollment, entry) = await FullClassWithWaiter(TimeSpan.FromMinutes(90));

        await Cancel(owner, enrollment.Id);

        Assert.AreEqual(WaitlistStatus.Promoted, (await StoredEntry(entry.Id)).Status);
    }

    [TestMethod]
    public async Task CancelAfterWaitlistClosedLeavesSeatsFree()
    {
        var (owner, enrollment, entry) = await FullClassWithWaiter(TimeSpan.FromMinutes(50));

        await Cancel(owner, enrollment.Id);

        Assert.AreEqual(WaitlistStatus.Waiting, (await StoredEntry(entry.Id)).Status);
        Assert.AreEqual(0, await ActiveSeats(enrollment.ClassId));
    }

    [TestMethod]
    public async Task ClubCancelledClassPromotesNobody()
    {
        var owner = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 1, cancelled: true);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 1);
        var entry = await Given.WaitlistEntry(await Given.User(), scheduledClass);

        await Cancel(owner, enrollment.Id);

        Assert.AreEqual(WaitlistStatus.Waiting, (await StoredEntry(entry.Id)).Status);
    }

    [TestMethod]
    public async Task LeftWaiterIsNotPromoted()
    {
        var owner = await Given.User();
        var waiter = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 1);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 1);
        var entry = await Given.WaitlistEntry(waiter, scheduledClass);
        using (var client = _factory.CreateClientFor(waiter))
        {
            await client.SendAsync(ApiClient.Post($"/enrollments/waitlist/{entry.Id}/cancel", null));
        }

        await Cancel(owner, enrollment.Id);

        Assert.AreEqual(WaitlistStatus.Cancelled, (await StoredEntry(entry.Id)).Status);
        Assert.AreEqual(0, await ActiveSeats(scheduledClass.Id));
    }

    [TestMethod]
    public async Task DecreasePromotesWaiting()
    {
        var owner = await Given.User(visits: 0);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 3);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 3);
        var entry = await Given.WaitlistEntry(await Given.User(), scheduledClass, seats: 2);

        var response = await ChangeSeats(owner, enrollment.Id, 1);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(2, (await JsonAssert.ReadAsync(response)).GetProperty("pass").GetProperty("remainingVisits").GetInt32());
        Assert.AreEqual(WaitlistStatus.Promoted, (await StoredEntry(entry.Id)).Status);
        Assert.AreEqual(3, await ActiveSeats(scheduledClass.Id));
    }

    [TestMethod]
    public async Task InitiatorWaitingOnSameClassGetsReleasedSeats()
    {
        var anna = await Given.User(visits: 0);
        var boris = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 2);
        var enrollment = await Given.Enrollment(anna, scheduledClass, seats: 2);
        var annaEntry = await Given.WaitlistEntry(anna, scheduledClass, seats: 2, createdAt: Earlier(2));
        var borisEntry = await Given.WaitlistEntry(boris, scheduledClass, seats: 2, createdAt: Earlier(1));

        var response = await Cancel(anna, enrollment.Id);

        Assert.AreEqual(2, (await JsonAssert.ReadAsync(response)).GetProperty("refundedVisits").GetInt32());
        Assert.AreEqual(WaitlistStatus.Promoted, (await StoredEntry(annaEntry.Id)).Status);
        Assert.AreEqual(WaitlistStatus.Waiting, (await StoredEntry(borisEntry.Id)).Status);
        Assert.AreEqual(0, await RemainingVisits(anna));
    }

    [TestMethod]
    public async Task ParallelCancelsKeepInvariants()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 4);
        var owners = new List<(TestUser User, Enrollment Enrollment)>();
        var waiters = new List<TestUser>();

        for (var i = 0; i < 4; i++)
        {
            var owner = await Given.User();
            owners.Add((owner, await Given.Enrollment(owner, scheduledClass, seats: 1)));
        }

        for (var i = 0; i < 6; i++)
        {
            var waiter = await Given.User(visits: 1);
            waiters.Add(waiter);
            await Given.WaitlistEntry(waiter, scheduledClass, seats: 1 + i % 2, createdAt: Earlier(10 - i));
        }

        var responses = await Task.WhenAll(owners.Select(x => Cancel(x.User, x.Enrollment.Id)));

        Assert.IsTrue(responses.All(x => x.StatusCode == HttpStatusCode.OK));
        Assert.IsTrue(await ActiveSeats(scheduledClass.Id) <= scheduledClass.Capacity);
        foreach (var waiter in waiters)
        {
            Assert.IsTrue(await RemainingVisits(waiter) >= 0);
        }

        var promotedSeats = await _factory.QueryAsync(db => db.WaitlistEntries
            .Where(x => x.ClassId == scheduledClass.Id && x.Status == WaitlistStatus.Promoted)
            .SumAsync(x => x.Seats));
        Assert.AreEqual(3, promotedSeats);
        Assert.AreEqual(3, await ActiveSeats(scheduledClass.Id));
    }

    private static async Task<(TestUser Owner, Enrollment Enrollment, WaitlistEntry Entry)> FullClassWithWaiter(TimeSpan startsIn)
    {
        var owner = await Given.User();
        var scheduledClass = await Given.Class(startsIn, capacity: 1);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 1);
        var entry = await Given.WaitlistEntry(await Given.User(), scheduledClass);

        return (owner, enrollment, entry);
    }

    private static Task<WaitlistEntry> StoredEntry(long id)
    {
        return _factory.QueryAsync(db => db.WaitlistEntries.AsNoTracking().SingleAsync(x => x.Id == id));
    }

    private static Task<Enrollment> StoredEnrollment(long id)
    {
        return _factory.QueryAsync(db => db.Enrollments.AsNoTracking().SingleAsync(x => x.Id == id));
    }

    private static Task<int> ActiveSeats(long classId)
    {
        return _factory.QueryAsync(db => db.Enrollments
            .Where(x => x.ClassId == classId && x.Status == EnrollmentStatus.Active)
            .SumAsync(x => x.Seats));
    }

    private static Task<int> RemainingVisits(TestUser user)
    {
        return _factory.QueryAsync(db => db.Passes.Where(x => x.UserId == user.Id).Select(x => x.RemainingVisits).SingleAsync());
    }

    private static async Task<HttpResponseMessage> Cancel(TestUser user, long enrollmentId)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.SendAsync(ApiClient.Post($"/enrollments/{enrollmentId}/cancel", null));
    }

    private static async Task<HttpResponseMessage> ChangeSeats(TestUser user, long enrollmentId, int seats)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.SendAsync(ApiClient.Post($"/enrollments/{enrollmentId}/seats", JsonSerializer.Serialize(new { seats })));
    }
}
