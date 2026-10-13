using System.Net;
using System.Text.Json;
using WebApi.Tests.Infrastructure;
using WebApi.Time;

namespace WebApi.Tests.Controllers;

[TestClass]
public class EnrollmentCardTests
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
    public async Task ReturnsFullCard()
    {
        var room = await Given.Room("Hall Z");
        var user = await Given.User(visits: 7, validUntil: new DateOnly(2030, 1, 31));
        var scheduledClass = await Given.Class(
            TimeSpan.FromHours(5),
            roomId: room.Id,
            durationMinutes: 90,
            title: "Yoga");
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 2);

        var response = await GetCard(user, enrollment.Id);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var card = await JsonAssert.ReadAsync(response);

        JsonAssert.HasExactlyProperties(card, "id", "status", "seats", "createdAt", "class", "pass");
        Assert.AreEqual(enrollment.Id, card.GetProperty("id").GetInt64());
        Assert.AreEqual("active", card.GetProperty("status").GetString());
        Assert.AreEqual(2, card.GetProperty("seats").GetInt32());
        Assert.AreEqual(Iso(enrollment.CreatedAt), card.GetProperty("createdAt").GetString());

        var cardClass = card.GetProperty("class");
        JsonAssert.HasExactlyProperties(cardClass, "id", "title", "startsAt", "durationMinutes", "isCancelled", "room");
        Assert.AreEqual(scheduledClass.Id, cardClass.GetProperty("id").GetInt64());
        Assert.AreEqual("Yoga", cardClass.GetProperty("title").GetString());
        Assert.AreEqual(Iso(scheduledClass.StartsAt), cardClass.GetProperty("startsAt").GetString());
        Assert.AreEqual(90, cardClass.GetProperty("durationMinutes").GetInt32());
        Assert.IsFalse(cardClass.GetProperty("isCancelled").GetBoolean());

        var cardRoom = cardClass.GetProperty("room");
        JsonAssert.HasExactlyProperties(cardRoom, "id", "name");
        Assert.AreEqual(room.Id, cardRoom.GetProperty("id").GetInt64());
        Assert.AreEqual("Hall Z", cardRoom.GetProperty("name").GetString());

        var pass = card.GetProperty("pass");
        JsonAssert.HasExactlyProperties(pass, "remainingVisits", "validUntil");
        Assert.AreEqual(7, pass.GetProperty("remainingVisits").GetInt32());
        Assert.AreEqual("2030-01-31", pass.GetProperty("validUntil").GetString());
    }

    [TestMethod]
    public async Task ReturnsCancelledEnrollmentWithOriginalSeats()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(5));
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 3, cancelled: true, refundedVisits: 3);

        var card = await JsonAssert.ReadAsync(await GetCard(user, enrollment.Id));

        Assert.AreEqual("cancelled", card.GetProperty("status").GetString());
        Assert.AreEqual(3, card.GetProperty("seats").GetInt32());
    }

    [TestMethod]
    public async Task ReturnsEnrollmentOfClassCancelledByClub()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(5), cancelled: true);
        var enrollment = await Given.Enrollment(user, scheduledClass);

        var response = await GetCard(user, enrollment.Id);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var card = await JsonAssert.ReadAsync(response);
        Assert.IsTrue(card.GetProperty("class").GetProperty("isCancelled").GetBoolean());
    }

    [TestMethod]
    public async Task ReturnsEnrollmentOfStartedClass()
    {
        var user = await Given.User();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(-1));
        var enrollment = await Given.Enrollment(user, scheduledClass);

        Assert.AreEqual(HttpStatusCode.OK, (await GetCard(user, enrollment.Id)).StatusCode);
    }

    [TestMethod]
    public async Task ShowsCurrentPassState()
    {
        var user = await Given.User(visits: 0, validUntil: new DateOnly(2020, 1, 1));
        var scheduledClass = await Given.Class(TimeSpan.FromHours(5));
        var enrollment = await Given.Enrollment(user, scheduledClass);

        var pass = (await JsonAssert.ReadAsync(await GetCard(user, enrollment.Id))).GetProperty("pass");

        Assert.AreEqual(0, pass.GetProperty("remainingVisits").GetInt32());
        Assert.AreEqual("2020-01-01", pass.GetProperty("validUntil").GetString());
    }

    [TestMethod]
    public async Task EnrollmentOfOtherUserIsNotFound()
    {
        var owner = await Given.User();
        var stranger = await Given.User();
        var enrollment = await Given.Enrollment(owner, await Given.Class(TimeSpan.FromHours(5)));

        var response = await GetCard(stranger, enrollment.Id);

        await AssertNotFound(response);
    }

    [TestMethod]
    public async Task AdminCannotSeeEnrollmentOfOtherUser()
    {
        var owner = await Given.User();
        var admin = await Given.Admin();
        var enrollment = await Given.Enrollment(owner, await Given.Class(TimeSpan.FromHours(5)));

        await AssertNotFound(await GetCard(admin, enrollment.Id));
    }

    [TestMethod]
    [DataRow("999999999999")]
    [DataRow("0")]
    [DataRow("-1")]
    public async Task UnknownEnrollmentIsNotFound(string id)
    {
        var user = await Given.User();
        using var client = _factory.CreateClientFor(user);

        await AssertNotFound(await client.GetAsync($"/enrollments/{id}"));
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("1.5")]
    [DataRow("99999999999999999999")]
    public async Task NonNumericIdIsBadRequest(string id)
    {
        var user = await Given.User();
        using var client = _factory.CreateClientFor(user);

        var response = await client.GetAsync($"/enrollments/{id}");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task RequestWithoutTokenIsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/enrollments/1");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetCard(TestUser user, long enrollmentId)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.GetAsync($"/enrollments/{enrollmentId}");
    }

    private static async Task AssertNotFound(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await JsonAssert.ReadAsync(response);
        Assert.AreEqual(404, problem.GetProperty("status").GetInt32());
        Assert.IsFalse(problem.TryGetProperty("reason", out _));
    }

    private static string Iso(DateTime value)
    {
        var options = new JsonSerializerOptions { Converters = { new UtcDateTimeJsonConverter() } };

        return JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, options))!;
    }
}
