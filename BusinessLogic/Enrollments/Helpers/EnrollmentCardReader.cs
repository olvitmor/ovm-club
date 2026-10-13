using BusinessLogic.Enrollments.CommonModels;
using DataAccessLayer.UnitOfWork;
using DataReadAccessLayer;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Enrollments.Helpers;

public static class EnrollmentCardReader
{
    public static Task<EnrollmentCard?> ReadAsync(
        ClubReadDbContext db,
        long userId,
        long enrollmentId,
        CancellationToken ct)
    {
        return (
                from enrollment in db.Enrollments
                join scheduledClass in db.Classes on enrollment.ClassId equals scheduledClass.Id
                join room in db.Rooms on scheduledClass.RoomId equals room.Id
                join pass in db.Passes on enrollment.UserId equals pass.UserId
                where enrollment.Id == enrollmentId && enrollment.UserId == userId
                select new EnrollmentCard(
                    enrollment.Id,
                    enrollment.Status,
                    enrollment.Seats,
                    enrollment.CreatedAt,
                    new EnrollmentCardClass(
                        scheduledClass.Id,
                        scheduledClass.Title,
                        scheduledClass.StartsAt,
                        scheduledClass.DurationMinutes,
                        scheduledClass.IsCancelled,
                        new EnrollmentCardRoom(room.Id, room.Name)),
                    new EnrollmentCardPass(pass.RemainingVisits, pass.ValidUntil)))
            .FirstOrDefaultAsync(ct);
    }

    public static Task<EnrollmentCard?> ReadAsync(
        IUnitOfWork unitOfWork,
        long userId,
        long enrollmentId,
        CancellationToken ct)
    {
        return (
                from enrollment in unitOfWork.Enrollment.Query().AsNoTracking()
                join scheduledClass in unitOfWork.Class.Query().IgnoreQueryFilters()
                    on enrollment.ClassId equals scheduledClass.Id
                join room in unitOfWork.Room.Query() on scheduledClass.RoomId equals room.Id
                join pass in unitOfWork.Pass.Query() on enrollment.UserId equals pass.UserId
                where enrollment.Id == enrollmentId && enrollment.UserId == userId
                select new EnrollmentCard(
                    enrollment.Id,
                    enrollment.Status,
                    enrollment.Seats,
                    enrollment.CreatedAt,
                    new EnrollmentCardClass(
                        scheduledClass.Id,
                        scheduledClass.Title,
                        scheduledClass.StartsAt,
                        scheduledClass.DurationMinutes,
                        scheduledClass.IsCancelled,
                        new EnrollmentCardRoom(room.Id, room.Name)),
                    new EnrollmentCardPass(pass.RemainingVisits, pass.ValidUntil)))
            .FirstOrDefaultAsync(ct);
    }
}
