using DataReadAccessLayer;
using DataReadAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Database;

[TestClass]
public class ClubReadDbContextTests : DatabaseTestBase
{
    [TestMethod]
    public async Task QueriesAreNotTracked()
    {
        var user = await Given.User();
        await Given.Enrollment(user, await Given.Class(TimeSpan.FromHours(3)));

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClubReadDbContext>();

        var enrollments = await db.Enrollments.Where(x => x.UserId == user.Id).ToListAsync();

        Assert.AreEqual(1, enrollments.Count);
        Assert.AreEqual(QueryTrackingBehavior.NoTracking, db.ChangeTracker.QueryTrackingBehavior);
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public async Task SavingChangesIsForbidden()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClubReadDbContext>();

        db.Rooms.Add(new Room { Name = "Should not be saved" });

        Assert.ThrowsException<InvalidOperationException>(() => db.SaveChanges());
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [TestMethod]
    public async Task ReadModelHasNoClassQueryFilter()
    {
        var cancelled = await Given.Class(TimeSpan.FromHours(3), cancelled: true);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClubReadDbContext>();

        var stored = await db.Classes.SingleAsync(x => x.Id == cancelled.Id);

        Assert.IsTrue(stored.IsCancelled);
    }

    [TestMethod]
    public async Task ReadModelMatchesDatabaseSchema()
    {
        var room = await Given.Room("Hall R");
        var user = await Given.User(visits: 3);
        var scheduledClass = await Given.Class(TimeSpan.FromHours(3), roomId: room.Id);
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 2, cancelled: true, refundedVisits: 2);
        var entry = await Given.WaitlistEntry(user, scheduledClass, seats: 1);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClubReadDbContext>();

        Assert.AreEqual("Hall R", (await db.Rooms.SingleAsync(x => x.Id == room.Id)).Name);
        Assert.AreEqual(3, (await db.Passes.SingleAsync(x => x.UserId == user.Id)).RemainingVisits);
        Assert.AreEqual(2, (await db.Enrollments.SingleAsync(x => x.Id == enrollment.Id)).RefundedVisits);
        Assert.AreEqual(room.Id, (await db.Classes.SingleAsync(x => x.Id == scheduledClass.Id)).RoomId);
        Assert.AreEqual("waiting", (await db.WaitlistEntries.SingleAsync(x => x.Id == entry.Id)).Status);
    }
}
