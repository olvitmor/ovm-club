using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace WebApi.Tests.Configuration;

[TestClass]
public class StartupTests
{
    [TestMethod]
    public async Task ApplicationStartsWithDefaultSettings()
    {
        await using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    [DataRow("Booking:RefundCutoffSeconds", "0")]
    [DataRow("Booking:WaitlistCloseBeforeSeconds", "-3600")]
    [DataRow("Schedule:DefaultLimit", "101")]
    [DataRow("RateLimit:PermitLimit", "0")]
    [DataRow("Idempotency:RetentionSeconds", "0")]
    [DataRow("Cache:EnrollmentCardTtlSeconds", "0")]
    public async Task InvalidSettingStopsStartup(string key, string value)
    {
        await using var factory = new ApiApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })));

        var exception = Assert.ThrowsException<Microsoft.Extensions.Options.OptionsValidationException>(
            () => factory.CreateClient());

        StringAssert.Contains(exception.Message, key.Split(':')[0]);
    }
}
