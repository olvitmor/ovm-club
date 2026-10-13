using BusinessLogic.Enrollments;
using BusinessLogic.Enrollments.GetEnrollmentCard;
using DataReadAccessLayer;
using BusinessLogic.Results;
using DataAccessLayer.Entities;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Handlers;

[TestClass]
public class GetEnrollmentCardQueryHandlerTests : DatabaseTestBase
{
    [TestMethod]
    public async Task ReturnsCardWithClassRoomAndPass()
    {
        var room = await Given.Room("Hall H");
        var user = await Given.User(visits: 4, validUntil: new DateOnly(2030, 5, 1));
        var scheduledClass = await Given.Class(
            TimeSpan.FromHours(4),
            roomId: room.Id,
            cancelled: true,
            durationMinutes: 45,
            title: "Boxing");
        var enrollment = await Given.Enrollment(user, scheduledClass, seats: 2);

        var result = await Handle(user.Id, enrollment.Id);

        Assert.AreEqual(ResultStatus.Success, result.Status);
        var card = result.Value;
        Assert.AreEqual(enrollment.Id, card.Id);
        Assert.AreEqual(EnrollmentStatus.Active, card.Status);
        Assert.AreEqual(2, card.Seats);
        Assert.AreEqual(enrollment.CreatedAt, card.CreatedAt);
        Assert.AreEqual(scheduledClass.Id, card.Class.Id);
        Assert.AreEqual("Boxing", card.Class.Title);
        Assert.AreEqual(scheduledClass.StartsAt, card.Class.StartsAt);
        Assert.AreEqual(45, card.Class.DurationMinutes);
        Assert.IsTrue(card.Class.IsCancelled);
        Assert.AreEqual(room.Id, card.Class.Room.Id);
        Assert.AreEqual("Hall H", card.Class.Room.Name);
        Assert.AreEqual(4, card.Pass.RemainingVisits);
        Assert.AreEqual(new DateOnly(2030, 5, 1), card.Pass.ValidUntil);
    }

    [TestMethod]
    public async Task UnknownEnrollmentIsNotFound()
    {
        var user = await Given.User();

        Assert.AreEqual(ResultStatus.NotFound, (await Handle(user.Id, long.MaxValue)).Status);
    }

    [TestMethod]
    public async Task EnrollmentOfOtherUserIsNotFound()
    {
        var owner = await Given.User();
        var stranger = await Given.User();
        var enrollment = await Given.Enrollment(owner, await Given.Class(TimeSpan.FromHours(4)));

        Assert.AreEqual(ResultStatus.NotFound, (await Handle(stranger.Id, enrollment.Id)).Status);
    }

    private static async Task<Result<EnrollmentCard>> Handle(long userId, long enrollmentId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var handler = new GetEnrollmentCardQueryHandler(scope.ServiceProvider.GetRequiredService<ClubReadDbContext>());

        return await handler.HandleAsync(new GetEnrollmentCardQuery(userId, enrollmentId), CancellationToken.None);
    }
}
