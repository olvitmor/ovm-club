using System.Net;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class LeaveWaitlistTests
{
    private static ApiApplicationFactory _factory = null!;

    private static Given Given => _factory.Given;

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
    public async Task LeaveCancelsEntry()
    {
        var user = await Given.User();
        var entry = await Given.WaitlistEntry(user, await Given.Class(TimeSpan.FromHours(3)));

        var response = await Leave(user, entry.Id);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAssert.ReadAsync(response);
        JsonAssert.HasExactlyProperties(body, "id", "status");
        Assert.AreEqual(entry.Id, body.GetProperty("id").GetInt64());
        Assert.AreEqual("cancelled", body.GetProperty("status").GetString());

        var stored = await Stored(entry.Id);
        Assert.AreEqual(WaitlistStatus.Cancelled, stored.Status);
        Assert.AreEqual(_factory.Time.GetUtcNow().UtcDateTime, stored.CancelledAt);
    }

    [TestMethod]
    public async Task RepeatReturnsSameResultAndChangesNothing()
    {
        var user = await Given.User();
        var entry = await Given.WaitlistEntry(user, await Given.Class(TimeSpan.FromHours(3)));
        Assert.AreEqual(HttpStatusCode.OK, (await Leave(user, entry.Id)).StatusCode);
        var cancelledAt = (await Stored(entry.Id)).CancelledAt;

        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        var response = await Leave(user, entry.Id);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("cancelled", (await JsonAssert.ReadAsync(response)).GetProperty("status").GetString());
        Assert.AreEqual(cancelledAt, (await Stored(entry.Id)).CancelledAt);
    }

    [TestMethod]
    public async Task LeaveIsAllowedAfterWaitlistClosedAndClassStarted()
    {
        var user = await Given.User();
        var entry = await Given.WaitlistEntry(user, await Given.Class(TimeSpan.FromHours(-1)));

        Assert.AreEqual(HttpStatusCode.OK, (await Leave(user, entry.Id)).StatusCode);
    }

    [TestMethod]
    public async Task PromotedEntryIsConflict()
    {
        var user = await Given.User();
        var entry = await Given.WaitlistEntry(user, await Given.Class(TimeSpan.FromHours(3)), status: WaitlistStatus.Promoted);

        var response = await Leave(user, entry.Id);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual("already-promoted", await response.ReadReasonAsync());
        Assert.AreEqual(WaitlistStatus.Promoted, (await Stored(entry.Id)).Status);
    }

    [TestMethod]
    public async Task OthersMoveUpAfterLeave()
    {
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 1);
        await Given.Enrollment(await Given.User(), scheduledClass, seats: 1);
        var first = await Given.User();
        var entry = await Given.WaitlistEntry(first, scheduledClass, createdAt: DateTime.UtcNow.AddMinutes(-5));
        await Given.WaitlistEntry(await Given.User(), scheduledClass, createdAt: DateTime.UtcNow.AddMinutes(-4));

        await Leave(first, entry.Id);

        using var client = _factory.CreateClientFor(await Given.User());
        var response = await client.PostJsonAsync(
            "/enrollments/waitlist",
            new { classId = scheduledClass.Id, seats = 1 },
            ApiClient.NewKey());
        Assert.AreEqual(2, (await JsonAssert.ReadAsync(response)).GetProperty("position").GetInt32());
    }

    [TestMethod]
    public async Task EntryOfOtherUserIsNotFound()
    {
        var owner = await Given.User();
        var entry = await Given.WaitlistEntry(owner, await Given.Class(TimeSpan.FromHours(3)));

        Assert.AreEqual(HttpStatusCode.NotFound, (await Leave(await Given.User(), entry.Id)).StatusCode);
        Assert.AreEqual(WaitlistStatus.Waiting, (await Stored(entry.Id)).Status);
    }

    [TestMethod]
    public async Task UnknownEntryIsNotFound()
    {
        Assert.AreEqual(HttpStatusCode.NotFound, (await Leave(await Given.User(), long.MaxValue)).StatusCode);
    }

    [TestMethod]
    public async Task NonNumericIdIsBadRequest()
    {
        using var client = _factory.CreateClientFor(await Given.User());

        var response = await client.SendAsync(ApiClient.Post("/enrollments/waitlist/abc/cancel", null));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<WaitlistEntry> Stored(long id)
    {
        return _factory.QueryAsync(db => db.WaitlistEntries.AsNoTracking().SingleAsync(x => x.Id == id));
    }

    private static async Task<HttpResponseMessage> Leave(TestUser user, long entryId)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.SendAsync(ApiClient.Post($"/enrollments/waitlist/{entryId}/cancel", null));
    }
}
