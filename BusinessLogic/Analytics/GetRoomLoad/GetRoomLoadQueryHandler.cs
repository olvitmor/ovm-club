using DataAccessLayer.Entities;
using DataReadAccessLayer;
using Microsoft.EntityFrameworkCore;
using Ovm.Cqrs;

namespace BusinessLogic.Analytics.GetRoomLoad;

public sealed class GetRoomLoadQueryHandler : IQueryHandler<GetRoomLoadQuery, IReadOnlyList<RoomLoad>>
{
    private readonly ClubReadDbContext _db;

    public GetRoomLoadQueryHandler(ClubReadDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RoomLoad>> HandleAsync(GetRoomLoadQuery query, CancellationToken ct)
    {
        var from = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusive = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var classes = _db.Classes.Where(x => !x.IsCancelled && x.StartsAt >= from && x.StartsAt < toExclusive);

        if (query.RoomId != null)
        {
            classes = classes.Where(x => x.RoomId == query.RoomId);
        }

        var rows = await (
                from scheduledClass in classes
                join room in _db.Rooms on scheduledClass.RoomId equals room.Id
                select new
                {
                    Date = DateOnly.FromDateTime(scheduledClass.StartsAt),
                    RoomId = room.Id,
                    RoomName = room.Name,
                    scheduledClass.Capacity,
                    BookedSeats = _db.Enrollments
                        .Where(x => x.ClassId == scheduledClass.Id && x.Status == EnrollmentStatus.Active)
                        .Sum(x => x.Seats)
                }
                into row
                group row by new { row.Date, row.RoomId, row.RoomName }
                into day
                orderby day.Key.Date, day.Key.RoomId
                select new RoomLoad(
                    day.Key.Date,
                    new RoomLoadRoom(day.Key.RoomId, day.Key.RoomName),
                    day.Count(),
                    day.Sum(x => x.Capacity),
                    day.Sum(x => x.BookedSeats)))
            .ToListAsync(ct);

        return rows;
    }
}
