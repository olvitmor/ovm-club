using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class CreateEnrollmentConcurrencyTests
{
    private const int Parallelism = 20;

    private static ApiApplicationFactory _factory = null!;

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
    public async Task ParallelRequestsNeverExceedCapacity()
    {
        var scheduledClass = await _factory.Given.Class(TimeSpan.FromHours(3), capacity: 3);
        var users = new List<TestUser>();

        for (var i = 0; i < Parallelism; i++)
        {
            users.Add(await _factory.Given.User(visits: 5));
        }

        var responses = await Task.WhenAll(users.Select(user => Enroll(user, scheduledClass.Id)));

        Assert.AreEqual(3, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.AreEqual(Parallelism - 3, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));

        var takenSeats = await _factory.QueryAsync(db => db.Enrollments
            .Where(x => x.ClassId == scheduledClass.Id)
            .SumAsync(x => x.Seats));
        Assert.AreEqual(3, takenSeats);
    }

    [TestMethod]
    public async Task ParallelRequestsNeverOverspendPass()
    {
        var user = await _factory.Given.User(visits: 3);
        var classes = new List<long>();

        for (var i = 0; i < Parallelism; i++)
        {
            classes.Add((await _factory.Given.Class(TimeSpan.FromHours(3), capacity: 10)).Id);
        }

        var responses = await Task.WhenAll(classes.Select(classId => Enroll(user, classId)));

        Assert.AreEqual(3, responses.Count(x => x.StatusCode == HttpStatusCode.Created));

        var remaining = await _factory.QueryAsync(db => db.Passes
            .Where(x => x.UserId == user.Id)
            .Select(x => x.RemainingVisits)
            .SingleAsync());
        var spent = await _factory.QueryAsync(db => db.Enrollments.Where(x => x.UserId == user.Id).SumAsync(x => x.Seats));

        Assert.AreEqual(0, remaining);
        Assert.AreEqual(3, spent);
    }

    private static async Task<HttpResponseMessage> Enroll(TestUser user, long classId)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.SendAsync(ApiClient.Post(
            "/enrollments",
            JsonSerializer.Serialize(new { classId, seats = 1 }),
            ApiClient.NewKey()));
    }
}
