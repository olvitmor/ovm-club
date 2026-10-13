using BusinessLogic.Enrollments.Helpers;
using DataAccessLayer.UnitOfWork;
using DataReadAccessLayer;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Handlers;

[TestClass]
public class EnrollmentCardReaderTests : DatabaseTestBase
{
    [TestMethod]
    public async Task ReadAndWriteModelsBuildSameCard()
    {
        var room = await Given.Room("Hall W");
        var user = await Given.User(visits: 6, validUntil: new DateOnly(2031, 2, 3));
        var scheduledClass = await Given.Class(
            TimeSpan.FromHours(2),
            roomId: room.Id,
            cancelled: true,
            durationMinutes: 90,
            title: "Pilates");
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 3, cancelled: true, refundedVisits: 3);

        await using var scope = Factory.Services.CreateAsyncScope();
        var fromRead = await EnrollmentCardReader.ReadAsync(
            scope.ServiceProvider.GetRequiredService<ClubReadDbContext>(),
            user.Id,
            enrollment.Id,
            CancellationToken.None);
        var fromWrite = await EnrollmentCardReader.ReadAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            user.Id,
            enrollment.Id,
            CancellationToken.None);

        Assert.IsNotNull(fromRead);
        Assert.AreEqual(fromRead, fromWrite);
        Assert.AreEqual("Pilates", fromRead.Class.Title);
        Assert.IsTrue(fromRead.Class.IsCancelled);
        Assert.AreEqual("Hall W", fromRead.Class.Room.Name);
        Assert.AreEqual(6, fromRead.Pass.RemainingVisits);
    }

    [TestMethod]
    public async Task BothModelsHideEnrollmentOfOtherUser()
    {
        var owner = await Given.User();
        var stranger = await Given.User();
        var enrollment = await Given.Enrollment(owner, await Given.Class(TimeSpan.FromHours(2)));

        await using var scope = Factory.Services.CreateAsyncScope();

        Assert.IsNull(await EnrollmentCardReader.ReadAsync(
            scope.ServiceProvider.GetRequiredService<ClubReadDbContext>(),
            stranger.Id,
            enrollment.Id,
            CancellationToken.None));
        Assert.IsNull(await EnrollmentCardReader.ReadAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            stranger.Id,
            enrollment.Id,
            CancellationToken.None));
    }
}
