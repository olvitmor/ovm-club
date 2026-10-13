using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class EnrollmentCardCacheTests
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
    public async Task SecondReadComesFromCache()
    {
        var user = await Given.User();
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 1);
        await Card(user, enrollment.Id);

        await _factory.QueryAsync(db => db.Enrollments
            .Where(x => x.Id == enrollment.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.Seats, 9)));

        Assert.AreEqual(1, (await Card(user, enrollment.Id)).GetProperty("seats").GetInt32());
    }

    [TestMethod]
    public async Task CreateRefreshesOtherCardsOfUser()
    {
        var user = await Given.User(visits: 5);
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 1);
        await Card(user, enrollment.Id);

        using (var client = _factory.CreateClientFor(user))
        {
            var created = await client.PostJsonAsync(
                "/enrollments",
                new { classId = (await Given.Class(TimeSpan.FromHours(3))).Id, seats = 2 },
                ApiClient.NewKey());
            Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        }

        Assert.AreEqual(3, RemainingVisits(await Card(user, enrollment.Id)));
    }

    [TestMethod]
    public async Task ChangeSeatsRefreshesCard()
    {
        var user = await Given.User(visits: 5);
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 1);
        await Card(user, enrollment.Id);

        await Post(user, $"/enrollments/{enrollment.Id}/seats", new { seats = 3 });

        var card = await Card(user, enrollment.Id);
        Assert.AreEqual(3, card.GetProperty("seats").GetInt32());
        Assert.AreEqual(3, RemainingVisits(card));
    }

    [TestMethod]
    public async Task CancelRefreshesCard()
    {
        var user = await Given.User(visits: 0);
        var enrollment = await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)), seats: 2);
        await Card(user, enrollment.Id);

        await Post(user, $"/enrollments/{enrollment.Id}/cancel", null);

        var card = await Card(user, enrollment.Id);
        Assert.AreEqual("cancelled", card.GetProperty("status").GetString());
        Assert.AreEqual(2, RemainingVisits(card));
    }

    [TestMethod]
    public async Task CancelRefreshesCardsOfPromotedUser()
    {
        var owner = await Given.User();
        var waiter = await Given.User(visits: 5);
        var waiterEnrollment = await Given.Enrollment(waiter, await Given.Class(TimeSpan.FromHours(5)), seats: 1);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 2);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 2);
        await Given.WaitlistEntry(waiter, scheduledClass, seats: 2);
        await Card(waiter, waiterEnrollment.Id);

        await Post(owner, $"/enrollments/{enrollment.Id}/cancel", null);

        Assert.AreEqual(3, RemainingVisits(await Card(waiter, waiterEnrollment.Id)));
    }

    [TestMethod]
    public async Task DecreaseRefreshesCardsOfPromotedUser()
    {
        var owner = await Given.User();
        var waiter = await Given.User(visits: 5);
        var waiterEnrollment = await Given.Enrollment(waiter, await Given.Class(TimeSpan.FromHours(5)), seats: 1);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 3);
        var enrollment = await Given.Enrollment(owner, scheduledClass, seats: 3);
        await Given.WaitlistEntry(waiter, scheduledClass, seats: 2);
        await Card(waiter, waiterEnrollment.Id);

        await Post(owner, $"/enrollments/{enrollment.Id}/seats", new { seats = 1 });

        Assert.AreEqual(3, RemainingVisits(await Card(waiter, waiterEnrollment.Id)));
    }

    [TestMethod]
    public async Task CachedCardIsNotVisibleToOtherUser()
    {
        var owner = await Given.User();
        var enrollment = await Given.Enrollment(owner, await Given.Class(TimeSpan.FromHours(3)));
        await Card(owner, enrollment.Id);

        using var client = _factory.CreateClientFor(await Given.User());

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/enrollments/{enrollment.Id}")).StatusCode);
    }

    [TestMethod]
    public async Task WithoutRedisCardAndCommandsWork()
    {
        await using var factory = new ApiApplicationFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = "localhost:1,connectTimeout=200"
        });
        var user = await factory.Given.User(visits: 5);
        var enrollment = await factory.Given.Enrollment(user, await factory.Given.Class(TimeSpan.FromHours(3)), seats: 1);
        using var client = factory.CreateClientFor(user);

        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync($"/enrollments/{enrollment.Id}")).StatusCode);
        var changed = await client.SendAsync(
            ApiClient.Post($"/enrollments/{enrollment.Id}/seats", JsonSerializer.Serialize(new { seats = 2 })));
        Assert.AreEqual(HttpStatusCode.OK, changed.StatusCode);
        var card = await JsonAssert.ReadAsync(await client.GetAsync($"/enrollments/{enrollment.Id}"));
        Assert.AreEqual(2, card.GetProperty("seats").GetInt32());
    }

    private static int RemainingVisits(JsonElement card)
    {
        return card.GetProperty("pass").GetProperty("remainingVisits").GetInt32();
    }

    private static async Task<JsonElement> Card(TestUser user, long enrollmentId)
    {
        using var client = _factory.CreateClientFor(user);
        var response = await client.GetAsync($"/enrollments/{enrollmentId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        return await JsonAssert.ReadAsync(response);
    }

    private static async Task Post(TestUser user, string url, object? body)
    {
        using var client = _factory.CreateClientFor(user);
        var response = await client.SendAsync(ApiClient.Post(url, body == null ? null : JsonSerializer.Serialize(body)));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
