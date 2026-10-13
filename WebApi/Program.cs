using System.Text;
using System.Text.Json.Serialization;
using BusinessLogic;
using BusinessLogic.Options;
using Common.Caching;
using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.Seeding;
using DataAccessLayer.UnitOfWork;
using DataReadAccessLayer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using WebApi.Auth;
using WebApi.Background;
using WebApi.Configuration;
using WebApi.Middleware;
using WebApi.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services
    .AddControllers(options => options.ModelBinderProviders.Insert(0, new TimeModelBinderProvider()))
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());

        // решение: число в кавычках ("classId": "1") — неверный формат поля и 400, а не молча принятое число,
        // как по умолчанию в веб-настройках System.Text.Json.
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.AddBearerAuthentication());
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<ClubDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddDbContext<ClubReadDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplicationOptions();
builder.Services.AddBusinessLogic();
builder.Services.AddSingleton<IdempotencyKeyCleanupService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<IdempotencyKeyCleanupService>());

// решение: приложение стартует и работает без Redis — кэш карточки пропускается, а команды к недоступному Redis
// падают сразу, а не ждут переподключения в очереди.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!);
    options.AbortOnConnectFail = false;
    options.BacklogPolicy = BacklogPolicy.FailFast;

    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddSingleton<ICacheService>(provider => new RedisCacheService(
    provider.GetRequiredService<IConnectionMultiplexer>(),
    provider.GetRequiredService<IOptions<CacheOptions>>().Value.EnrollmentCardTtl));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<AccessTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Services.GetRequiredService<IConnectionMultiplexer>();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ClubDbContext>();

    await dbContext.Database.MigrateAsync();
    var bookingOptions = scope.ServiceProvider.GetRequiredService<IOptions<BookingOptions>>().Value;

    await DatabaseSeeder.SeedAsync(dbContext, bookingOptions.RefundCutoff);
}

app.Run();

public partial class Program;
