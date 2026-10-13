using System.Net;
using System.Net.Http.Json;
using DataAccessLayer.Seeding;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class RateLimitTests
{
    private const int Limit = 60;

    private static ApiApplicationFactory _factory = null!;

    private static Given Given => _factory.Given;

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _factory = new ApiApplicationFactory(new Dictionary<string, string?> { ["RateLimit:PermitLimit"] = $"{Limit}" });
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task SixtyFirstRequestIsTooManyWithRetryAfter()
    {
        using var client = _factory.CreateClientFor(await Given.User());

        for (var i = 0; i < Limit; i++)
        {
            Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/enrollments/{long.MaxValue}")).StatusCode);
        }

        var response = await client.GetAsync($"/enrollments/{long.MaxValue}");

        Assert.AreEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        var retryAfter = int.Parse(response.Headers.GetValues("Retry-After").Single());
        Assert.IsTrue(retryAfter is >= 1 and <= 60, $"Retry-After {retryAfter}");
        Assert.AreEqual(429, (await JsonAssert.ReadAsync(response)).GetProperty("status").GetInt32());
    }

    [TestMethod]
    public async Task LimitIsSharedAcrossEndpointsAndCountsBadRequests()
    {
        using var client = _factory.CreateClientFor(await Given.User());

        for (var i = 0; i < Limit / 2; i++)
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, (await client.GetAsync("/schedule")).StatusCode);
            Assert.AreEqual(
                HttpStatusCode.NotFound,
                (await client.SendAsync(ApiClient.Post($"/enrollments/{long.MaxValue}/cancel", null))).StatusCode);
        }

        Assert.AreEqual(
            HttpStatusCode.TooManyRequests,
            (await client.PostJsonAsync("/enrollments/waitlist", new { classId = 1, seats = 1 }, ApiClient.NewKey())).StatusCode);
    }

    [TestMethod]
    public async Task UsersHaveIndependentLimits()
    {
        using var first = _factory.CreateClientFor(await Given.User());
        using var second = _factory.CreateClientFor(await Given.User());

        for (var i = 0; i <= Limit; i++)
        {
            await first.GetAsync($"/enrollments/{long.MaxValue}");
        }

        Assert.AreEqual(HttpStatusCode.TooManyRequests, (await first.GetAsync($"/enrollments/{long.MaxValue}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await second.GetAsync($"/enrollments/{long.MaxValue}")).StatusCode);
    }

    [TestMethod]
    public async Task UnauthorizedRequestsAreNotLimited()
    {
        using var anonymous = _factory.CreateClient();

        for (var i = 0; i <= Limit; i++)
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/enrollments/{long.MaxValue}")).StatusCode);
        }
    }

    [TestMethod]
    public async Task ForbiddenRequestsDoNotSpendLimit()
    {
        var user = await Given.User();
        using var client = _factory.CreateClientFor(user);

        for (var i = 0; i < Limit; i++)
        {
            Assert.AreEqual(HttpStatusCode.Forbidden, (await client.GetAsync("/analytics/load?from=2026-09-22&to=2026-09-22")).StatusCode);
        }

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/enrollments/{long.MaxValue}")).StatusCode);
    }

    [TestMethod]
    public async Task LoginIsNotLimited()
    {
        var user = await Given.User();
        using var client = _factory.CreateClient();

        for (var i = 0; i <= Limit; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/auth/login",
                new { email = user.Email, password = DatabaseSeeder.DefaultPassword });
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [TestMethod]
    public async Task WithoutRedisInMemoryLimitApplies()
    {
        await using var factory = new ApiApplicationFactory(new Dictionary<string, string?>
        {
            ["RateLimit:PermitLimit"] = $"{Limit}",
            ["ConnectionStrings:Redis"] = "localhost:1,connectTimeout=200"
        });
        using var client = factory.CreateClientFor(await factory.Given.User());

        for (var i = 0; i < Limit; i++)
        {
            Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/enrollments/{long.MaxValue}")).StatusCode);
        }

        var response = await client.GetAsync($"/enrollments/{long.MaxValue}");

        Assert.AreEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.IsTrue(response.Headers.Contains("Retry-After"));
    }
}
