using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace WebApi.Tests.Infrastructure;

public class Given
{
    private static readonly Lazy<string> PasswordHash = new(() =>
        new PasswordHasher<User>().HashPassword(new User(), DatabaseSeeder.DefaultPassword));

    private readonly IServiceProvider _services;
    private readonly FakeTimeProvider _time;

    public Given(IServiceProvider services, FakeTimeProvider time)
    {
        _services = services;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<Room> Room(string? name = null)
    {
        var room = new Room { Name = name ?? $"Room {Guid.NewGuid():N}" };

        await SaveAsync(room);

        return room;
    }

    public async Task<TestUser> User(int visits = 10, DateOnly? validUntil = null, string role = "user")
    {
        var user = new User
        {
            Email = $"test-{Guid.NewGuid():N}@club.test",
            PasswordHash = PasswordHash.Value,
            Role = role,
            CreatedAt = Now
        };

        await SaveAsync(user);

        var pass = new Pass
        {
            UserId = user.Id,
            RemainingVisits = visits,
            ValidUntil = validUntil ?? DateOnly.FromDateTime(Now).AddDays(30)
        };

        await SaveAsync(pass);

        return new TestUser(user.Id, user.Email, user.Role, pass.Id);
    }

    public Task<TestUser> Admin()
    {
        return User(role: "admin");
    }

    public async Task<Class> Class(
        TimeSpan startsIn,
        int capacity = 10,
        long? roomId = null,
        bool cancelled = false,
        int durationMinutes = 60,
        string title = "Test class")
    {
        var scheduledClass = new Class
        {
            RoomId = roomId ?? (await Room()).Id,
            Title = title,
            StartsAt = Now.Add(startsIn),
            DurationMinutes = durationMinutes,
            Capacity = capacity,
            IsCancelled = cancelled
        };

        await SaveAsync(scheduledClass);

        return scheduledClass;
    }

    public async Task<Enrollment> Enrollment(
        TestUser user,
        Class scheduledClass,
        int seats = 1,
        bool cancelled = false,
        int? refundedVisits = null,
        DateTime? createdAt = null)
    {
        var created = createdAt ?? Now;

        var enrollment = new Enrollment
        {
            UserId = user.Id,
            ClassId = scheduledClass.Id,
            Seats = seats,
            Status = cancelled ? EnrollmentStatus.Cancelled : EnrollmentStatus.Active,
            CreatedAt = created,
            CancelledAt = cancelled ? created : null,
            RefundedVisits = cancelled ? refundedVisits ?? 0 : null
        };

        await SaveAsync(enrollment);

        return enrollment;
    }

    public async Task<WaitlistEntry> WaitlistEntry(
        TestUser user,
        Class scheduledClass,
        int seats = 1,
        string status = WaitlistStatus.Waiting,
        DateTime? createdAt = null)
    {
        var entry = new WaitlistEntry
        {
            UserId = user.Id,
            ClassId = scheduledClass.Id,
            Seats = seats,
            Status = status,
            CreatedAt = createdAt ?? Now,
            CancelledAt = status == WaitlistStatus.Cancelled ? createdAt ?? Now : null
        };

        await SaveAsync(entry);

        return entry;
    }

    private async Task SaveAsync<T>(T entity) where T : class
    {
        await using var scope = _services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClubDbContext>();

        dbContext.Add(entity);
        await dbContext.SaveChangesAsync();
    }
}
