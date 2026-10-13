using System.Net;
using System.Text.Json;
using DataAccessLayer.Entities;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class ScheduleTests
{
    private static ApiApplicationFactory _factory = null!;

    private static Given Given => _factory.Given;

    private static DateTime Now => _factory.Time.GetUtcNow().UtcDateTime;

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
    public async Task ItemHasContractShape()
    {
        var user = await Given.User();
        var room = await Given.Room("Зал 1");
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 7, roomId: room.Id, durationMinutes: 45, title: "Йога");

        var body = await GetOk(user, Query(room.Id));

        JsonAssert.HasExactlyProperties(body, "items", "continuationToken");
        Assert.AreEqual(JsonValueKind.Null, body.GetProperty("continuationToken").ValueKind);
        var item = body.GetProperty("items").EnumerateArray().Single();
        JsonAssert.HasExactlyProperties(
            item,
            "classId", "title", "startsAt", "durationMinutes", "room", "capacity", "freeSeats", "mySeats");
        JsonAssert.HasExactlyProperties(item.GetProperty("room"), "id", "name");
        Assert.AreEqual(scheduledClass.Id, item.GetProperty("classId").GetInt64());
        Assert.AreEqual("Йога", item.GetProperty("title").GetString());
        Assert.AreEqual(Format(scheduledClass.StartsAt), item.GetProperty("startsAt").GetString());
        Assert.AreEqual(45, item.GetProperty("durationMinutes").GetInt32());
        Assert.AreEqual("Зал 1", item.GetProperty("room").GetProperty("name").GetString());
        Assert.AreEqual(7, item.GetProperty("capacity").GetInt32());
        Assert.AreEqual(7, item.GetProperty("freeSeats").GetInt32());
        Assert.AreEqual(0, item.GetProperty("mySeats").GetInt32());
    }

    [TestMethod]
    public async Task SeatsCountOnlyActiveEnrollments()
    {
        var user = await Given.User();
        var other = await Given.User();
        var room = await Given.Room();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 5, roomId: room.Id);
        await Given.Enrollment(user, scheduledClass, seats: 1);
        await Given.Enrollment(user, scheduledClass, seats: 2);
        await Given.Enrollment(user, scheduledClass, seats: 4, cancelled: true);
        await Given.Enrollment(other, scheduledClass, seats: 1);

        var item = Items(await GetOk(user, Query(room.Id))).Single();

        Assert.AreEqual(1, item.GetProperty("freeSeats").GetInt32());
        Assert.AreEqual(3, item.GetProperty("mySeats").GetInt32());
    }

    [TestMethod]
    public async Task FreeSeatsNeverNegative()
    {
        var user = await Given.User();
        var room = await Given.Room();
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), capacity: 2, roomId: room.Id);
        await Given.Enrollment(await Given.User(), scheduledClass, seats: 3);

        Assert.AreEqual(0, Items(await GetOk(user, Query(room.Id))).Single().GetProperty("freeSeats").GetInt32());
    }

    [TestMethod]
    public async Task CancelledClassesAreHidden()
    {
        var user = await Given.User();
        var room = await Given.Room();
        await Given.Class(TimeSpan.FromHours(3), roomId: room.Id, cancelled: true);
        var visible = await Given.Class(TimeSpan.FromHours(4), roomId: room.Id);

        CollectionAssert.AreEqual(new[] { visible.Id }, Ids(await GetOk(user, Query(room.Id))));
    }

    [TestMethod]
    public async Task FromIsInclusiveAndToExclusive()
    {
        var user = await Given.User();
        var room = await Given.Room();
        var atFrom = await Given.Class(TimeSpan.FromHours(1), roomId: room.Id);
        var atTo = await Given.Class(TimeSpan.FromHours(2), roomId: room.Id);

        var ids = Ids(await GetOk(user, Query(room.Id, from: atFrom.StartsAt, to: atTo.StartsAt)));

        CollectionAssert.AreEqual(new[] { atFrom.Id }, ids);
    }

    [TestMethod]
    public async Task PagesCoverAllClassesInOrderWithoutGapsOrDuplicates()
    {
        var user = await Given.User();
        var room = await Given.Room();
        var created = new List<Class>();

        foreach (var hours in new[] { 5, 3, 3, 3, 4, 3, 6 })
        {
            created.Add(await Given.Class(TimeSpan.FromHours(hours), roomId: room.Id));
        }

        var expected = created.OrderBy(x => x.StartsAt).ThenBy(x => x.Id).Select(x => x.Id).ToArray();
        var collected = new List<long>();
        var pages = 0;
        string? token = null;

        do
        {
            var body = await GetOk(user, Query(room.Id, limit: 3, token: token));
            collected.AddRange(Ids(body));
            token = body.GetProperty("continuationToken").GetString();
            pages++;
        }
        while (token != null);

        Assert.AreEqual(3, pages);
        CollectionAssert.AreEqual(expected, collected.ToArray());
    }

    [TestMethod]
    public async Task LimitCanChangeBetweenPages()
    {
        var user = await Given.User();
        var room = await Given.Room();

        for (var i = 1; i <= 4; i++)
        {
            await Given.Class(TimeSpan.FromHours(i), roomId: room.Id);
        }

        var first = await GetOk(user, Query(room.Id, limit: 1));
        var second = await GetOk(user, Query(room.Id, limit: 3, token: first.GetProperty("continuationToken").GetString()));

        Assert.AreEqual(3, Items(second).Count);
        Assert.AreEqual(JsonValueKind.Null, second.GetProperty("continuationToken").ValueKind);
    }

    [TestMethod]
    public async Task DefaultLimitIsTwenty()
    {
        var user = await Given.User();
        var room = await Given.Room();

        for (var i = 1; i <= 21; i++)
        {
            await Given.Class(TimeSpan.FromMinutes(i), roomId: room.Id);
        }

        var body = await GetOk(user, Query(room.Id));

        Assert.AreEqual(20, Items(body).Count);
        Assert.IsNotNull(body.GetProperty("continuationToken").GetString());
    }

    [TestMethod]
    public async Task UnknownRoomGivesEmptyList()
    {
        var body = await GetOk(await Given.User(), Query(long.MaxValue));

        Assert.AreEqual(0, Items(body).Count);
        Assert.AreEqual(JsonValueKind.Null, body.GetProperty("continuationToken").ValueKind);
    }

    [TestMethod]
    public async Task TokenWithOtherFiltersIsBadRequest()
    {
        var user = await Given.User();
        var room = await Given.Room();
        await Given.Class(TimeSpan.FromHours(1), roomId: room.Id);
        await Given.Class(TimeSpan.FromHours(2), roomId: room.Id);
        var token = (await GetOk(user, Query(room.Id, limit: 1))).GetProperty("continuationToken").GetString();

        Assert.AreEqual(HttpStatusCode.BadRequest, (await Get(user, Query(room.Id + 1, limit: 1, token: token))).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Get(user, Query(room.Id, from: Now.AddSeconds(1), token: token))).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Get(user, Query(null, limit: 1, token: token))).StatusCode);
    }

    [TestMethod]
    [DataRow("to=2026-09-22T10:00:00Z")]
    [DataRow("from=2026-09-22T10:00:00Z")]
    [DataRow("from=2026-09-22&to=2026-09-23T10:00:00Z")]
    [DataRow("from=abc&to=2026-09-23T10:00:00Z")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-22T10:00:00Z")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-22T09:00:00Z")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-23T10:00:00Z&roomId=abc")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-23T10:00:00Z&limit=0")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-23T10:00:00Z&limit=101")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-23T10:00:00Z&limit=x")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-23T10:00:00Z&continuationToken=garbage")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-23T10:00:00Z&continuationToken=e30")]
    public async Task InvalidQueryIsBadRequest(string query)
    {
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Get(await Given.User(), query)).StatusCode);
    }

    [TestMethod]
    public async Task LimitBoundsAreAccepted()
    {
        var user = await Given.User();

        Assert.AreEqual(HttpStatusCode.OK, (await Get(user, Query(long.MaxValue, limit: 1))).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await Get(user, Query(long.MaxValue, limit: 100))).StatusCode);
    }

    private static string Query(long? roomId, DateTime? from = null, DateTime? to = null, int? limit = null, string? token = null)
    {
        var query = $"from={Format(from ?? Now)}&to={Format(to ?? Now.AddDays(1))}";

        if (roomId != null)
        {
            query += $"&roomId={roomId}";
        }

        if (limit != null)
        {
            query += $"&limit={limit}";
        }

        if (token != null)
        {
            query += $"&continuationToken={Uri.EscapeDataString(token)}";
        }

        return query;
    }

    private static string Format(DateTime value)
    {
        return value.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFZ");
    }

    private static List<JsonElement> Items(JsonElement body)
    {
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    private static long[] Ids(JsonElement body)
    {
        return Items(body).Select(x => x.GetProperty("classId").GetInt64()).ToArray();
    }

    private static async Task<JsonElement> GetOk(TestUser user, string query)
    {
        var response = await Get(user, query);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

        return await JsonAssert.ReadAsync(response);
    }

    private static async Task<HttpResponseMessage> Get(TestUser user, string query)
    {
        using var client = _factory.CreateClientFor(user);

        return await client.GetAsync($"/schedule?{query}");
    }
}
