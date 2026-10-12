using DataAccessLayer;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.Tests.Database;

[TestClass]
public class SchemaAndSeedingTests
{
    private static readonly TimeSpan RefundCutoff = TimeSpan.FromHours(2);

    private static ApiApplicationFactory _factory = null!;

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _factory = new ApiApplicationFactory();
        _factory.CreateClient().Dispose();
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task AllMigrationsAreAppliedAndModelHasNoPendingChanges()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClubDbContext>();

        Assert.AreEqual(0, (await dbContext.Database.GetPendingMigrationsAsync()).Count());
        Assert.IsFalse(dbContext.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public async Task CancelledSeededEnrollmentsHaveRefundByRule()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClubDbContext>();

        var cancelled = await dbContext.Enrollments
            .IgnoreQueryFilters()
            .Include(x => x.Class)
            .Where(x => x.Status == EnrollmentStatus.Cancelled)
            .ToListAsync();

        Assert.IsTrue(cancelled.Count > 0);

        foreach (var enrollment in cancelled)
        {
            var expected = enrollment.Class.IsCancelled
                || enrollment.Class.StartsAt - enrollment.CancelledAt!.Value >= RefundCutoff
                    ? enrollment.Seats
                    : 0;

            Assert.AreEqual(expected, enrollment.RefundedVisits, $"enrollment {enrollment.Id}");
        }
    }

    [TestMethod]
    public async Task ActiveSeededEnrollmentsHaveNoRefund()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClubDbContext>();

        var withRefund = await dbContext.Enrollments
            .IgnoreQueryFilters()
            .CountAsync(x => x.Status == EnrollmentStatus.Active && x.RefundedVisits != null);

        Assert.AreEqual(0, withRefund);
    }

    [TestMethod]
    public async Task OnlyOneWaitingEntryPerUserAndClass()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClubDbContext>();

        var classId = await dbContext.Classes.Select(x => x.Id).FirstAsync();
        var userId = await dbContext.Users.Select(x => x.Id).FirstAsync();

        WaitlistEntry Entry(string status) => new()
        {
            UserId = userId,
            ClassId = classId,
            Seats = 1,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.WaitlistEntries.AddRange(Entry(WaitlistStatus.Cancelled), Entry(WaitlistStatus.Cancelled));
        dbContext.WaitlistEntries.Add(Entry(WaitlistStatus.Waiting));
        await dbContext.SaveChangesAsync();

        dbContext.WaitlistEntries.Add(Entry(WaitlistStatus.Waiting));

        await Assert.ThrowsExceptionAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [TestMethod]
    public async Task IdempotencyKeyIsUniquePerUserOperationAndKey()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClubDbContext>();

        var userIds = await dbContext.Users.OrderBy(x => x.Id).Select(x => x.Id).Take(2).ToListAsync();

        IdempotencyKey Key(long userId, string operation) => new()
        {
            UserId = userId,
            Operation = operation,
            Key = "same-key",
            RequestHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.IdempotencyKeys.AddRange(
            Key(userIds[0], "enroll"),
            Key(userIds[0], "waitlist"),
            Key(userIds[1], "enroll"));
        await dbContext.SaveChangesAsync();

        dbContext.IdempotencyKeys.Add(Key(userIds[0], "enroll"));

        await Assert.ThrowsExceptionAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}
