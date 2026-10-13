using System.Net;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class CancelEnrollmentTests
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
    public async Task CancelInTimeRefundsAllVisits()
    {
        var user = await Given.User(visits: 2);
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 3);

        var response = await Cancel(user, enrollment.Id);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAssert.ReadAsync(response);
        JsonAssert.HasExactlyProperties(body, "id", "status", "refundedVisits");
        Assert.AreEqual(enrollment.Id, body.GetProperty("id").GetInt64());
        Assert.AreEqual("cancelled", body.GetProperty("status").GetString());
        Assert.AreEqual(3, body.GetProperty("refundedVisits").GetInt32());

        var stored = await Stored(enrollment.Id);
        Assert.AreEqual(EnrollmentStatus.Cancelled, stored.Status);
        Assert.AreEqual(3, stored.RefundedVisits);
        Assert.AreEqual(_factory.Time.GetUtcNow().UtcDateTime, stored.CancelledAt);
        Assert.AreEqual(5, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task RefundBoundaryIsExactlyCutoff()
    {
        var user = await Given.User(visits: 0);
        var atCutoff = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(2)), seats: 2);
        var secondLate = await Given.Enrollment(
            user,
            await Given.Class(TimeSpan.FromHours(2) - TimeSpan.FromSeconds(1)),
            seats: 2);

        Assert.AreEqual(2, await CancelAndReadRefund(user, atCutoff.Id));
        Assert.AreEqual(0, await CancelAndReadRefund(user, secondLate.Id));
        Assert.AreEqual(2, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task LateCancelFreesSeatsWithoutRefund()
    {
        var user = await Given.User(visits: 0);
        var scheduledClass = await Given.Class(TimeSpan.FromMinutes(10), capacity: 3);
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 3);

        Assert.AreEqual(0, await CancelAndReadRefund(user, enrollment.Id));
        Assert.AreEqual(0, await ActiveSeats(scheduledClass.Id));
        Assert.AreEqual(0, await RemainingVisits(user));
        Assert.AreEqual(0, (await Stored(enrollment.Id)).RefundedVisits);
    }

    [TestMethod]
    public async Task ClubCancelledClassRefundsFullyAfterStart()
    {
        var user = await Given.User(visits: 0, validUntil: Today.AddDays(-1));
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(-1), cancelled: true), seats: 2);

        Assert.AreEqual(2, await CancelAndReadRefund(user, enrollment.Id));
        Assert.AreEqual(2, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task ClubCancelledClassRefundsFullyInsideCutoff()
    {
        var user = await Given.User(visits: 0);
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromMinutes(10), cancelled: true), seats: 2);

        Assert.AreEqual(2, await CancelAndReadRefund(user, enrollment.Id));
        Assert.AreEqual(2, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task StartedClassIsConflict()
    {
        var user = await Given.User(visits: 0);
        var scheduledClass = await Given.Class(TimeSpan.Zero, capacity: 2);
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 2);

        var response = await Cancel(user, enrollment.Id);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual("class-started", await response.ReadReasonAsync());
        Assert.AreEqual(EnrollmentStatus.Active, (await Stored(enrollment.Id)).Status);
        Assert.AreEqual(2, await ActiveSeats(scheduledClass.Id));
        Assert.AreEqual(0, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task RepeatReturnsSameResultAfterCutoffPassed()
    {
        var user = await Given.User(visits: 0);
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 2);

        Assert.AreEqual(2, await CancelAndReadRefund(user, enrollment.Id));
        var cancelledAt = (await Stored(enrollment.Id)).CancelledAt;

        _factory.Time.Advance(TimeSpan.FromHours(4));

        var response = await Cancel(user, enrollment.Id);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAssert.ReadAsync(response);
        Assert.AreEqual("cancelled", body.GetProperty("status").GetString());
        Assert.AreEqual(2, body.GetProperty("refundedVisits").GetInt32());
        Assert.AreEqual(cancelledAt, (await Stored(enrollment.Id)).CancelledAt);
        Assert.AreEqual(2, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task EarlierCancelledEnrollmentReturnsStoredRefund()
    {
        var user = await Given.User(visits: 0);
        var enrollment = await Given.Enrollment(
            user,
            await Given.Class(TimeSpan.FromHours(3)),
            seats: 3,
            cancelled: true,
            refundedVisits: 0);

        Assert.AreEqual(0, await CancelAndReadRefund(user, enrollment.Id));
        Assert.AreEqual(0, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task EnrollmentOfOtherUserIsNotFound()
    {
        var owner = await Given.User(visits: 0);
        var stranger = await Given.User();
        var enrollment = await Given.Enrollment(owner, await Given.Class(TimeSpan.FromHours(3)), seats: 1);

        Assert.AreEqual(HttpStatusCode.NotFound, (await Cancel(stranger, enrollment.Id)).StatusCode);
        Assert.AreEqual(EnrollmentStatus.Active, (await Stored(enrollment.Id)).Status);
        Assert.AreEqual(0, await RemainingVisits(owner));
    }

    [TestMethod]
    public async Task UnknownEnrollmentIsNotFound()
    {
        var user = await Given.User();

        Assert.AreEqual(HttpStatusCode.NotFound, (await Cancel(user, long.MaxValue)).StatusCode);
    }

    [TestMethod]
    public async Task NonNumericIdIsBadRequest()
    {
        var user = await Given.User();
        using var client = _factory.CreateClientFor(user);

        var response = await client.PostAsync("/enrollments/abc/cancel", null);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task RequestWithoutTokenIsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/enrollments/1/cancel", null);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ParallelCancelsRefundOnce()
    {
        var user = await Given.User(visits: 0);
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 2);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Cancel(user, enrollment.Id)));

        Assert.IsTrue(responses.All(x => x.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(2, await RemainingVisits(user));
    }

    private static async Task<int> CancelAndReadRefund(TestUser user, long enrollmentId)
    {
        var response = await Cancel(user, enrollmentId);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        return (await JsonAssert.ReadAsync(response)).GetProperty("refundedVisits").GetInt32();
    }

    private static Task<Enrollment> Stored(long enrollmentId)
    {
        return _factory.QueryAsync(db => db.Enrollments.AsNoTracking().SingleAsync(x => x.Id == enrollmentId));
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
}
