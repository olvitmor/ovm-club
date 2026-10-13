using System.Net;
using System.Text.Json;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class ChangeEnrollmentSeatsTests
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
    public async Task IncreaseSpendsDifferenceAndReturnsFullCard()
    {
        var user = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 10);
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 1);

        var response = await ChangeSeats(user, enrollment.Id, 3);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var card = await JsonAssert.ReadAsync(response);
        JsonAssert.HasExactlyProperties(card, "id", "status", "seats", "createdAt", "class", "pass");
        JsonAssert.HasExactlyProperties(
            card.GetProperty("class"),
            "id", "title", "startsAt", "durationMinutes", "isCancelled", "room");
        JsonAssert.HasExactlyProperties(card.GetProperty("pass"), "remainingVisits", "validUntil");
        Assert.AreEqual(enrollment.Id, card.GetProperty("id").GetInt64());
        Assert.AreEqual(3, card.GetProperty("seats").GetInt32());
        Assert.AreEqual(3, card.GetProperty("pass").GetProperty("remainingVisits").GetInt32());

        Assert.AreEqual(3, await StoredSeats(enrollment.Id));
        Assert.AreEqual(3, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task DecreaseRefundsDifference()
    {
        var user = await Given.User(visits: 2);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 4);

        var response = await ChangeSeats(user, enrollment.Id, 1);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, (await JsonAssert.ReadAsync(response)).GetProperty("seats").GetInt32());
        Assert.AreEqual(1, await StoredSeats(enrollment.Id));
        Assert.AreEqual(5, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task DecreaseWorksWithExpiredAndEmptyPass()
    {
        var user = await Given.User(visits: 0, validUntil: Today.AddDays(-10));
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 3);

        var response = await ChangeSeats(user, enrollment.Id, 2);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task DecreaseShortlyBeforeStartStillRefunds()
    {
        var user = await Given.User(visits: 0);
        var scheduledClass = await Given.Class(TimeSpan.FromMinutes(10));
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 3);

        var response = await ChangeSeats(user, enrollment.Id, 1);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(2, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task SameSeatsChangesNothing()
    {
        var user = await Given.User(visits: 0, validUntil: Today.AddDays(-1));
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 2);
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 2);

        var response = await ChangeSeats(user, enrollment.Id, 2);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(2, (await JsonAssert.ReadAsync(response)).GetProperty("seats").GetInt32());
        Assert.AreEqual(0, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task SameSeatsStillChecksClassState()
    {
        var user = await Given.User();
        var started = await Given.Enrollment(user, await Given.Class(TimeSpan.Zero), seats: 1);
        var cancelledClass = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3), cancelled: true), seats: 1);
        var cancelledEnrollment = await Given.Enrollment(
            user,
            await Given.Class(TimeSpan.FromHours(3)),
            seats: 1,
            cancelled: true);

        await AssertConflict(user, started.Id, 1, "class-started");
        await AssertConflict(user, cancelledClass.Id, 1, "class-cancelled");
        await AssertConflict(user, cancelledEnrollment.Id, 1, "enrollment-cancelled");
    }

    [TestMethod]
    public async Task IncreaseWithinCapacityCountsOwnSeats()
    {
        var user = await Given.User(visits: 5);
        var other = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 5);
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 2);
        await Given.Enrollment(other, scheduledClass, seats: 2);

        Assert.AreEqual(HttpStatusCode.OK, (await ChangeSeats(user, enrollment.Id, 3)).StatusCode);
        await AssertConflict(user, enrollment.Id, 4, "capacity-exceeded");
    }

    [TestMethod]
    public async Task IncreaseNeedsVisitsForDifferenceOnly()
    {
        var user = await Given.User(visits: 1);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 3);

        await AssertConflict(user, enrollment.Id, 5, "pass-exhausted");
        Assert.AreEqual(HttpStatusCode.OK, (await ChangeSeats(user, enrollment.Id, 4)).StatusCode);
        Assert.AreEqual(0, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task IncreaseWithExpiredPassIsRefused()
    {
        var user = await Given.User(visits: 10, validUntil: Today.AddDays(-1));
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 1);

        await AssertConflict(user, enrollment.Id, 2, "pass-expired");
    }

    [TestMethod]
    public async Task EnrollmentCancellationWinsOverClassCancellation()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(-1), cancelled: true);
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 1, cancelled: true);

        await AssertConflict(user, enrollment.Id, 2, "enrollment-cancelled");
    }

    [TestMethod]
    public async Task ClassCancellationWinsOverStart()
    {
        var user = await Given.User();
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(-1), cancelled: true));

        await AssertConflict(user, enrollment.Id, 2, "class-cancelled");
    }

    [TestMethod]
    public async Task ClassStartWinsOverPassProblems()
    {
        var user = await Given.User(visits: 0, validUntil: Today.AddDays(-1));
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(-1)));

        await AssertConflict(user, enrollment.Id, 2, "class-started");
    }

    [TestMethod]
    public async Task PassExpiryWinsOverExhaustion()
    {
        var user = await Given.User(visits: 0, validUntil: Today.AddDays(-1));
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)));

        await AssertConflict(user, enrollment.Id, 2, "pass-expired");
    }

    [TestMethod]
    public async Task PassExhaustionWinsOverCapacity()
    {
        var user = await Given.User(visits: 0);
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3), capacity: 1));

        await AssertConflict(user, enrollment.Id, 2, "pass-exhausted");
    }

    [TestMethod]
    public async Task EnrollmentOfOtherUserIsNotFound()
    {
        var owner = await Given.User();
        var stranger = await Given.User();
        var enrollment = await Given.Enrollment(owner, await Given.Class(TimeSpan.FromHours(3)));

        Assert.AreEqual(HttpStatusCode.NotFound, (await ChangeSeats(stranger, enrollment.Id, 2)).StatusCode);
        Assert.AreEqual(1, await StoredSeats(enrollment.Id));
    }

    [TestMethod]
    public async Task UnknownEnrollmentIsNotFound()
    {
        var user = await Given.User();

        Assert.AreEqual(HttpStatusCode.NotFound, (await ChangeSeats(user, long.MaxValue, 2)).StatusCode);
    }

    [TestMethod]
    [DataRow("""{ "seats": 0 }""")]
    [DataRow("""{ "seats": -2 }""")]
    [DataRow("""{ }""")]
    [DataRow("""{ "seats": null }""")]
    [DataRow("""{ "seats": "2" }""")]
    [DataRow("""{ "seats": 2.5 }""")]
    [DataRow("""broken""")]
    [DataRow("")]
    public async Task InvalidBodyIsBadRequest(string json)
    {
        var user = await Given.User();
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 1);

        var response = await Send(user, $"/enrollments/{enrollment.Id}/seats", json);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(1, await StoredSeats(enrollment.Id));
    }

    [TestMethod]
    public async Task NonNumericIdIsBadRequest()
    {
        var user = await Given.User();

        var response = await Send(user, "/enrollments/abc/seats", """{ "seats": 1 }""");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task RequestWithoutTokenIsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(ApiClient.Post("/enrollments/1/seats", """{ "seats": 1 }"""));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ParallelIncreasesNeverExceedCapacity()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 6);
        var enrollments = new List<(TestUser User, Enrollment Enrollment)>();

        for (var i = 0; i < 5; i++)
        {
            var user = await Given.User(visits: 5);
            enrollments.Add((user, await Given.Enrollment(user, scheduledClass, seats: 1)));
        }

        var responses = await Task.WhenAll(enrollments.Select(x => ChangeSeats(x.User, x.Enrollment.Id, 2)));

        Assert.AreEqual(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(6, await _factory.QueryAsync(db => db.Enrollments
            .Where(x => x.ClassId == scheduledClass.Id && x.Status == EnrollmentStatus.Active)
            .SumAsync(x => x.Seats)));
    }

    private static async Task AssertConflict(TestUser user, long enrollmentId, int seats, string reason)
    {
        var seatsBefore = await StoredSeats(enrollmentId);
        var visitsBefore = await RemainingVisits(user);

        var response = await ChangeSeats(user, enrollmentId, seats);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual(reason, await response.ReadReasonAsync());
        Assert.AreEqual(seatsBefore, await StoredSeats(enrollmentId));
        Assert.AreEqual(visitsBefore, await RemainingVisits(user));
    }

    private static Task<int> StoredSeats(long enrollmentId)
    {
        return _factory.QueryAsync(db => db.Enrollments.Where(x => x.Id == enrollmentId).Select(x => x.Seats).SingleAsync());
    }

    private static Task<int> RemainingVisits(TestUser user)
    {
        return _factory.QueryAsync(db => db.Passes.Where(x => x.UserId == user.Id).Select(x => x.RemainingVisits).SingleAsync());
    }

    private static Task<HttpResponseMessage> ChangeSeats(TestUser user, long enrollmentId, int seats)
    {
        return Send(user, $"/enrollments/{enrollmentId}/seats", JsonSerializer.Serialize(new { seats }));
    }

    private static async Task<HttpResponseMessage> Send(TestUser user, string url, string json)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.SendAsync(ApiClient.Post(url, json));
    }
}
