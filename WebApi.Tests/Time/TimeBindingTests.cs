using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.Tests.Time;

[TestClass]
public class TimeBindingTests
{
    private static WebApplicationFactory<Program> _factory = null!;
    private static HttpClient _client = null!;

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _factory = new ApiApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(TimeProbeController).Assembly)));

        _client = _factory.CreateClient();
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    [DataRow("2026-09-22T10:00:00", "2026-09-22T10:00:00Z")]
    [DataRow("2026-09-22T10:00:00Z", "2026-09-22T10:00:00Z")]
    [DataRow("2026-09-22T10:00:00%2B03:00", "2026-09-22T07:00:00Z")]
    [DataRow("2026-09-22T10:00:00.1234567Z", "2026-09-22T10:00:00.123456Z")]
    public async Task BindsDateTimeAsUtc(string query, string expected)
    {
        var response = await _client.GetAsync($"/test/time/datetime?value={query}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(expected, await ReadValue(response));
    }

    [TestMethod]
    [DataRow("2026-09-22")]
    [DataRow("garbage")]
    [DataRow("2026-09-22T10:00:00+03:00")]
    public async Task RejectsInvalidDateTimeWith400(string query)
    {
        var response = await _client.GetAsync($"/test/time/datetime?value={query}");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.IsTrue(problem!.Errors.ContainsKey("value"));
    }

    [TestMethod]
    public async Task BindsDate()
    {
        var response = await _client.GetAsync("/test/time/date?value=2026-09-22");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("2026-09-22", await ReadValue(response));
    }

    [TestMethod]
    [DataRow("2026-09-22T10:00:00")]
    [DataRow("22.09.2026")]
    public async Task RejectsInvalidDateWith400(string query)
    {
        var response = await _client.GetAsync($"/test/time/date?value={query}");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task MissingOptionalValueBindsToNull()
    {
        var response = await _client.GetAsync("/test/time/optional");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("none", await ReadValue(response));
    }

    private static async Task<string?> ReadValue(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("value").GetString();
    }
}

[ApiController]
[Route("test/time")]
public class TimeProbeController : ControllerBase
{
    [HttpGet("datetime")]
    public IActionResult GetDateTime([FromQuery] DateTime value)
    {
        return Ok(new { value });
    }

    [HttpGet("date")]
    public IActionResult GetDate([FromQuery] DateOnly value)
    {
        return Ok(new { value });
    }

    [HttpGet("optional")]
    public IActionResult GetOptional([FromQuery] DateTime? value)
    {
        return Ok(new { value = value?.ToString("O") ?? "none" });
    }
}
