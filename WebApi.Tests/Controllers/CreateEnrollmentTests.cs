using System.Net;
using System.Text.Json;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;
using WebApi.Time;

namespace WebApi.Tests.Controllers;

[TestClass]
public class CreateEnrollmentTests
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
    public async Task CreatesEnrollmentAndSpendsVisits()
    {
        var user = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 10);

        var response = await Enroll(user, scheduledClass.Id, 2);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAssert.ReadAsync(response);
        JsonAssert.HasExactlyProperties(body, "id", "classId", "seats", "status", "createdAt");
        Assert.AreEqual(scheduledClass.Id, body.GetProperty("classId").GetInt64());
        Assert.AreEqual(2, body.GetProperty("seats").GetInt32());
        Assert.AreEqual("active", body.GetProperty("status").GetString());
        Assert.AreEqual(Iso(_factory.Time.GetUtcNow().UtcDateTime), body.GetProperty("createdAt").GetString());

        var id = body.GetProperty("id").GetInt64();
        var stored = await _factory.QueryAsync(db => db.Enrollments.SingleAsync(x => x.Id == id));
        Assert.AreEqual(user.Id, stored.UserId);
        Assert.AreEqual(EnrollmentStatus.Active, stored.Status);
        Assert.AreEqual(2, stored.Seats);
        Assert.AreEqual(3, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task CreatedEnrollmentIsVisibleInCard()
    {
        var user = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));
        var created = await JsonAssert.ReadAsync(await Enroll(user, scheduledClass.Id, 1));

        using var client = _factory.CreateClientFor(user);
        var card = await JsonAssert.ReadAsync(await client.GetAsync($"/enrollments/{created.GetProperty("id").GetInt64()}"));

        Assert.AreEqual(created.GetProperty("createdAt").GetString(), card.GetProperty("createdAt").GetString());
        Assert.AreEqual(4, card.GetProperty("pass").GetProperty("remainingVisits").GetInt32());
    }

    [TestMethod]
    public async Task UserCanHoldSeveralEnrollmentsOnSameClass()
    {
        var user = await Given.User(visits: 5);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 1)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 2)).StatusCode);
        Assert.AreEqual(2, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task UnknownFieldsAreIgnored()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        var response = await Send(user, $$"""{ "classId": {{scheduledClass.Id}}, "seats": 1, "userId": 42, "extra": [1] }""");

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    [DataRow("""{ "seats": 1 }""")]
    [DataRow("""{ "classId": 1 }""")]
    [DataRow("""{ "classId": 1, "seats": 0 }""")]
    [DataRow("""{ "classId": 1, "seats": -1 }""")]
    [DataRow("""{ "classId": "abc", "seats": 1 }""")]
    [DataRow("""{ "classId": "1", "seats": 1 }""")]
    [DataRow("""{ "classId": 1, "seats": 1.5 }""")]
    [DataRow("""{ "classId": 1, "seats": "1" }""")]
    [DataRow("""{ "classId": 1, "seats": 99999999999 }""")]
    [DataRow("""{ "classId": null, "seats": 1 }""")]
    [DataRow("""{ "classId": 1, "seats": 1""")]
    [DataRow("""not json""")]
    [DataRow("""[]""")]
    [DataRow("")]
    public async Task InvalidBodyIsBadRequest(string json)
    {
        var user = await Given.User();

        var response = await Send(user, json);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingCreated(user);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task MissingIdempotencyKeyIsBadRequest(string? key)
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        var response = await SendWithKey(user, Body(scheduledClass.Id, 1), key);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingCreated(user);
    }

    [TestMethod]
    public async Task VeryLongIdempotencyKeyIsAccepted()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        var response = await SendWithKey(user, Body(scheduledClass.Id, 1), new string('k', 4000));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task UnknownClassIsNotFound()
    {
        var user = await Given.User();

        var response = await Enroll(user, long.MaxValue, 1);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNothingCreated(user);
    }

    [TestMethod]
    public async Task RequestWithoutTokenIsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostJsonAsync("/enrollments", new { classId = 1, seats = 1 }, ApiClient.NewKey());

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task CancelledClassIsRefused()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), cancelled: true);

        await AssertConflict(user, scheduledClass.Id, 1, "class-cancelled");
    }

    [TestMethod]
    public async Task ClassStartingNowIsRefused()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.Zero);

        await AssertConflict(user, scheduledClass.Id, 1, "class-started");
    }

    [TestMethod]
    public async Task ClassStartingInOneSecondIsOpen()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromSeconds(1));

        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 1)).StatusCode);
    }

    [TestMethod]
    public async Task ExpiredPassIsRefused()
    {
        var user = await Given.User(validUntil: Today.AddDays(-1));
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        await AssertConflict(user, scheduledClass.Id, 1, "pass-expired");
    }

    [TestMethod]
    public async Task PassValidUntilTodayIsAccepted()
    {
        var user = await Given.User(validUntil: Today);
        var scheduledClass = await Given.Class(TimeSpan.FromDays(10));

        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 1)).StatusCode);
    }

    [TestMethod]
    public async Task PassWithTooFewVisitsIsRefused()
    {
        var user = await Given.User(visits: 1);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        await AssertConflict(user, scheduledClass.Id, 2, "pass-exhausted");
    }

    [TestMethod]
    public async Task PassWithExactVisitsIsAccepted()
    {
        var user = await Given.User(visits: 2);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 2)).StatusCode);
        Assert.AreEqual(0, await RemainingVisits(user));
    }

    [TestMethod]
    public async Task OverCapacityIsRefused()
    {
        var other = await Given.User();
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 5);
        await Given.Enrollment(other, scheduledClass, seats: 4);

        await AssertConflict(user, scheduledClass.Id, 2, "capacity-exceeded");
    }

    [TestMethod]
    public async Task ExactFillAndCancelledEnrollmentsDoNotCount()
    {
        var other = await Given.User();
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 5);
        await Given.Enrollment(other, scheduledClass, seats: 3);
        await Given.Enrollment(other, scheduledClass, seats: 5, cancelled: true);

        Assert.AreEqual(HttpStatusCode.Created, (await Enroll(user, scheduledClass.Id, 2)).StatusCode);
    }

    [TestMethod]
    public async Task ClassCancellationWinsOverStart()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(-1), cancelled: true);

        await AssertConflict(user, scheduledClass.Id, 1, "class-cancelled");
    }

    [TestMethod]
    public async Task ClassStartWinsOverPassProblems()
    {
        var user = await Given.User(visits: 0, validUntil: Today.AddDays(-1));
        var scheduledClass = await Given.Class(TimeSpan.FromHours(-1));

        await AssertConflict(user, scheduledClass.Id, 1, "class-started");
    }

    [TestMethod]
    public async Task PassExpiryWinsOverExhaustion()
    {
        var user = await Given.User(visits: 0, validUntil: Today.AddDays(-1));
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3));

        await AssertConflict(user, scheduledClass.Id, 1, "pass-expired");
    }

    [TestMethod]
    public async Task PassExhaustionWinsOverCapacity()
    {
        var other = await Given.User();
        var user = await Given.User(visits: 0);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 1);
        await Given.Enrollment(other, scheduledClass);

        await AssertConflict(user, scheduledClass.Id, 1, "pass-exhausted");
    }

    private static async Task AssertConflict(TestUser user, long classId, int seats, string reason)
    {
        var visitsBefore = await RemainingVisits(user);

        var response = await Enroll(user, classId, seats);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(reason, await response.ReadReasonAsync());
        Assert.AreEqual(visitsBefore, await RemainingVisits(user));
        await AssertNothingCreated(user);
    }

    private static async Task AssertNothingCreated(TestUser user)
    {
        Assert.AreEqual(0, await _factory.QueryAsync(db => db.Enrollments.CountAsync(x => x.UserId == user.Id)));
    }

    private static Task<int> RemainingVisits(TestUser user)
    {
        return _factory.QueryAsync(db => db.Passes.Where(x => x.UserId == user.Id).Select(x => x.RemainingVisits).SingleAsync());
    }

    private static Task<HttpResponseMessage> Enroll(TestUser user, long classId, int seats)
    {
        return Send(user, Body(classId, seats));
    }

    private static Task<HttpResponseMessage> Send(TestUser user, string json)
    {
        return SendWithKey(user, json, ApiClient.NewKey());
    }

    private static async Task<HttpResponseMessage> SendWithKey(TestUser user, string json, string? key)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.SendAsync(ApiClient.Post("/enrollments", json, key));
    }

    private static string Body(long classId, int seats)
    {
        return JsonSerializer.Serialize(new { classId, seats });
    }

    private static string Iso(DateTime value)
    {
        var options = new JsonSerializerOptions { Converters = { new UtcDateTimeJsonConverter() } };

        return JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, options))!;
    }
}
