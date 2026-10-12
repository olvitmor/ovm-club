using System.Net.Http.Headers;
using DataAccessLayer;
using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WebApi.Auth;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests;

public class ApiApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly Dictionary<string, string?> DefaultTestSettings = new()
    {
        ["RateLimit:PermitLimit"] = "1000000"
    };

    private readonly Dictionary<string, string?> _settings;

    public ApiApplicationFactory(IReadOnlyDictionary<string, string?>? settings = null)
    {
        _settings = new Dictionary<string, string?>(DefaultTestSettings);

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            _settings[key] = value;
        }

        var now = DateTimeOffset.UtcNow;
        Time = new FakeTimeProvider(new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero));
    }

    public FakeTimeProvider Time { get; }

    public Given Given => new(Services, Time);

    public HttpClient CreateClientFor(TestUser user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(user));

        return client;
    }

    public string CreateToken(TestUser user)
    {
        return Services
            .GetRequiredService<AccessTokenService>()
            .Create(new User { Id = user.Id, Email = user.Email, Role = user.Role })
            .AccessToken;
    }

    public async Task<T> QueryAsync<T>(Func<ClubDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();

        return await query(scope.ServiceProvider.GetRequiredService<ClubDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = TestDatabase.PostgresConnectionString,
                ["ConnectionStrings:Redis"] = TestDatabase.RedisConnectionString
            });

            configuration.AddInMemoryCollection(_settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }
}
