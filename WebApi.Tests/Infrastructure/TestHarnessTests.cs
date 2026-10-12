using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DataAccessLayer.Entities;
using DataAccessLayer.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Controllers;

namespace WebApi.Tests.Infrastructure;

[TestClass]
public class TestHarnessTests
{
    private static ApiApplicationFactory _factory = null!;
    private static WebApplicationFactory<Program> _app = null!;

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _factory = new ApiApplicationFactory();
        _app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(AuthProbeController).Assembly)));
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task CreatedUserCanLogIn()
    {
        var user = await _factory.Given.User();
        using var client = _app.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = user.Email, password = DatabaseSeeder.DefaultPassword });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/test/auth/me")).StatusCode);
    }

    [TestMethod]
    public async Task RequestWithoutTokenGets401()
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync("/test/auth/me");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RequestWithInvalidTokenGets401()
    {
        using var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        var response = await client.GetAsync("/test/auth/me");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task TokenIdentifiesUserAndRole()
    {
        var user = await _factory.Given.User();
        var admin = await _factory.Given.Admin();

        Assert.AreEqual($"{user.Id}:False", await Me(user));
        Assert.AreEqual($"{admin.Id}:True", await Me(admin));
    }

    [TestMethod]
    public async Task UnknownRouteGets404()
    {
        var user = await _factory.Given.User();
        using var client = ClientFor(user);

        var response = await client.GetAsync("/no-such-route");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task ApplicationSeesFakeTime()
    {
        var user = await _factory.Given.User();
        using var client = ClientFor(user);
        var expected = _factory.Time.GetUtcNow().AddHours(5);

        _factory.Time.Advance(TimeSpan.FromHours(5));
        var response = await client.GetFromJsonAsync<JsonElement>("/test/auth/now");

        Assert.AreEqual(expected.UtcDateTime, response.GetProperty("now").GetDateTime().ToUniversalTime());
    }

    [TestMethod]
    public async Task TokenStaysValidWhenFakeTimeMoves()
    {
        var user = await _factory.Given.User();
        using var client = ClientFor(user);

        _factory.Time.Advance(TimeSpan.FromDays(3));

        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/test/auth/me")).StatusCode);
    }

    [TestMethod]
    public async Task GivenCreatesConsistentData()
    {
        var user = await _factory.Given.User(visits: 3);
        var scheduledClass = await _factory.Given.Class(TimeSpan.FromHours(3), capacity: 4);
        var enrollment = await _factory.Given.Enrollment(user, scheduledClass, seats: 2);
        var entry = await _factory.Given.WaitlistEntry(user, scheduledClass, seats: 1);

        var stored = await _factory.QueryAsync(db => db.Enrollments
            .Include(x => x.Class)
            .SingleAsync(x => x.Id == enrollment.Id));
        var pass = await _factory.QueryAsync(db => db.Passes.SingleAsync(x => x.UserId == user.Id));
        var waiting = await _factory.QueryAsync(db => db.WaitlistEntries.SingleAsync(x => x.Id == entry.Id));

        Assert.AreEqual(user.PassId, pass.Id);
        Assert.AreEqual(3, pass.RemainingVisits);
        Assert.AreEqual(EnrollmentStatus.Active, stored.Status);
        Assert.AreEqual(4, stored.Class.Capacity);
        Assert.AreEqual(_factory.Time.GetUtcNow().UtcDateTime.AddHours(3), stored.Class.StartsAt);
        Assert.AreEqual(WaitlistStatus.Waiting, waiting.Status);
    }

    private static async Task<string?> Me(TestUser user)
    {
        using var client = ClientFor(user);

        return (await client.GetFromJsonAsync<JsonElement>("/test/auth/me")).GetProperty("me").GetString();
    }

    private static HttpClient ClientFor(TestUser user)
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken(user));

        return client;
    }
}

[Authorize]
[ApiController]
[Route("test/auth")]
public class AuthProbeController : BaseApiController
{
    private readonly TimeProvider _timeProvider;

    public AuthProbeController(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new { me = $"{CurrentUserId}:{IsAdmin}" });
    }

    [HttpGet("now")]
    public IActionResult Now()
    {
        return Ok(new { now = _timeProvider.GetUtcNow().UtcDateTime });
    }
}
