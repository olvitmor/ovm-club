using BusinessLogic.Results;
using DataReadAccessLayer;
using Microsoft.EntityFrameworkCore;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.GetEnrollmentCard;

public sealed class GetEnrollmentCardQueryHandler : IQueryHandler<GetEnrollmentCardQuery, Result<EnrollmentCard>>
{
    private readonly ClubReadDbContext _db;

    public GetEnrollmentCardQueryHandler(ClubReadDbContext db)
    {
        _db = db;
    }

    public async Task<Result<EnrollmentCard>> HandleAsync(GetEnrollmentCardQuery query, CancellationToken ct)
    {
        var card = await (
                from enrollment in _db.Enrollments
                join scheduledClass in _db.Classes on enrollment.ClassId equals scheduledClass.Id
                join room in _db.Rooms on scheduledClass.RoomId equals room.Id
                join pass in _db.Passes on enrollment.UserId equals pass.UserId
                where enrollment.Id == query.EnrollmentId && enrollment.UserId == query.UserId
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

        return card == null ? Result<EnrollmentCard>.NotFound() : card;
    }
}
