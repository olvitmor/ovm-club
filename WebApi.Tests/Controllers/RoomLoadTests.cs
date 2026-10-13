using System.Net;
using System.Text.Json;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Controllers;

[TestClass]
public class RoomLoadTests
{
    private static ApiApplicationFactory _factory = null!;

    private static Given Given => _factory.Given;

    private static DateTime Midnight => _factory.Time.GetUtcNow().UtcDateTime.Date.AddDays(2);

    private static DateOnly Day => DateOnly.FromDateTime(Midnight);

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
    public async Task ItemHasContractShapeAndSums()
    {
        var admin = await Given.Admin();
        var room = await Given.Room("Зал А");
        var first = await Given.Class(Midnight.AddHours(10) - Now, capacity: 10, roomId: room.Id);
        var second = await Given.Class(Midnight.AddHours(12) - Now, capacity: 5, roomId: room.Id);
        await Given.Enrollment(await Given.User(), first, seats: 3);
        await Given.Enrollment(await Given.User(), second, seats: 2);

        var body = await GetOk(admin, Query(Day, Day, room.Id));

        JsonAssert.HasExactlyProperties(body, "items");
        var item = Items(body).Single();
        JsonAssert.HasExactlyProperties(item, "date", "room", "classesCount", "totalCapacity", "bookedSeats");
        JsonAssert.HasExactlyProperties(item.GetProperty("room"), "id", "name");
        Assert.AreEqual(Day.ToString("yyyy-MM-dd"), item.GetProperty("date").GetString());
        Assert.AreEqual(room.Id, item.GetProperty("room").GetProperty("id").GetInt64());
        Assert.AreEqual("Зал А", item.GetProperty("room").GetProperty("name").GetString());
        Assert.AreEqual(2, item.GetProperty("classesCount").GetInt32());
        Assert.AreEqual(15, item.GetProperty("totalCapacity").GetInt32());
        Assert.AreEqual(5, item.GetProperty("bookedSeats").GetInt32());
    }

    [TestMethod]
    public async Task DayIsUtcDateOfStart()
    {
        var admin = await Given.Admin();
        var room = await Given.Room();
        await Given.Class(Midnight.AddMinutes(-30) - Now, roomId: room.Id);
        await Given.Class(Midnight.AddMinutes(30) - Now, roomId: room.Id);

        var items = Items(await GetOk(admin, Query(Day.AddDays(-1), Day, room.Id)));

        CollectionAssert.AreEqual(
            new[] { Day.AddDays(-1).ToString("yyyy-MM-dd"), Day.ToString("yyyy-MM-dd") },
            items.Select(x => x.GetProperty("date").GetString()).ToArray());
        Assert.IsTrue(items.All(x => x.GetProperty("classesCount").GetInt32() == 1));
    }

    [TestMethod]
    public async Task ToIsInclusive()
    {
        var admin = await Given.Admin();
        var room = await Given.Room();
        await Given.Class(Midnight.AddHours(23).AddMinutes(59) - Now, roomId: room.Id);
        await Given.Class(Midnight.AddDays(1) - Now, roomId: room.Id);

        var items = Items(await GetOk(admin, Query(Day, Day, room.Id)));

        Assert.AreEqual(1, items.Single().GetProperty("classesCount").GetInt32());
    }

    [TestMethod]
    public async Task CancelledClassesAndEnrollmentsAreExcluded()
    {
        var admin = await Given.Admin();
        var room = await Given.Room();
        var active = await Given.Class(Midnight.AddHours(10) - Now, capacity: 10, roomId: room.Id);
        var cancelled = await Given.Class(Midnight.AddHours(11) - Now, capacity: 20, roomId: room.Id, cancelled: true);
        await Given.Enrollment(await Given.User(), active, seats: 2);
        await Given.Enrollment(await Given.User(), active, seats: 4, cancelled: true);
        await Given.Enrollment(await Given.User(), cancelled, seats: 5);

        var item = Items(await GetOk(admin, Query(Day, Day, room.Id))).Single();

        Assert.AreEqual(1, item.GetProperty("classesCount").GetInt32());
        Assert.AreEqual(10, item.GetProperty("totalCapacity").GetInt32());
        Assert.AreEqual(2, item.GetProperty("bookedSeats").GetInt32());
    }

    [TestMethod]
    public async Task OnlyCancelledClassesGiveNoRow()
    {
        var admin = await Given.Admin();
        var room = await Given.Room();
        await Given.Class(Midnight.AddHours(10) - Now, roomId: room.Id, cancelled: true);

        Assert.AreEqual(0, Items(await GetOk(admin, Query(Day, Day, room.Id))).Count);
    }

    [TestMethod]
    public async Task RowsAreOrderedByDateThenRoom()
    {
        var admin = await Given.Admin();
        var firstRoom = await Given.Room();
        var secondRoom = await Given.Room();
        await Given.Class(Midnight.AddDays(1).AddHours(9) - Now, roomId: firstRoom.Id);
        await Given.Class(Midnight.AddHours(9) - Now, roomId: secondRoom.Id);
        await Given.Class(Midnight.AddHours(10) - Now, roomId: firstRoom.Id);

        var rows = Items(await GetOk(admin, Query(Day, Day.AddDays(1), null)))
            .Select(x => (Date: x.GetProperty("date").GetString(), Room: x.GetProperty("room").GetProperty("id").GetInt64()))
            .Where(x => x.Room == firstRoom.Id || x.Room == secondRoom.Id)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                (Day.ToString("yyyy-MM-dd"), firstRoom.Id),
                (Day.ToString("yyyy-MM-dd"), secondRoom.Id),
                (Day.AddDays(1).ToString("yyyy-MM-dd"), firstRoom.Id)
            },
            rows);
    }

    [TestMethod]
    public async Task UnknownRoomGivesEmptyList()
    {
        Assert.AreEqual(0, Items(await GetOk(await Given.Admin(), Query(Day, Day, long.MaxValue))).Count);
    }

    [TestMethod]
    public async Task UserIsForbidden()
    {
        Assert.AreEqual(HttpStatusCode.Forbidden, (await Get(await Given.User(), Query(Day, Day, null))).StatusCode);
    }

    [TestMethod]
    [DataRow("to=2026-09-22")]
    [DataRow("from=2026-09-22")]
    [DataRow("from=2026-09-22T10:00:00Z&to=2026-09-23")]
    [DataRow("from=2026-09-22&to=22.09.2026")]
    [DataRow("from=abc&to=2026-09-23")]
    [DataRow("from=2026-09-22&to=2026-09-21")]
    [DataRow("from=2026-09-22&to=2026-09-23&roomId=abc")]
    public async Task InvalidQueryIsBadRequest(string query)
    {
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Get(await Given.Admin(), query)).StatusCode);
    }

    private static string Query(DateOnly from, DateOnly to, long? roomId)
    {
        var query = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        return roomId == null ? query : $"{query}&roomId={roomId}";
    }

    private static List<JsonElement> Items(JsonElement body)
    {
        return body.GetProperty("items").EnumerateArray().ToList();
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

        return await client.GetAsync($"/analytics/load?{query}");
    }
}
