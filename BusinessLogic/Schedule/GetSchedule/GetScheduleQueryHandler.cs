using DataAccessLayer.Entities;
using DataReadAccessLayer;
using Microsoft.EntityFrameworkCore;
using Ovm.Cqrs;

namespace BusinessLogic.Schedule.GetSchedule;

public sealed class GetScheduleQueryHandler : IQueryHandler<GetScheduleQuery, SchedulePage>
{
    private readonly ClubReadDbContext _db;

    public GetScheduleQueryHandler(ClubReadDbContext db)
    {
        _db = db;
    }

    public async Task<SchedulePage> HandleAsync(GetScheduleQuery query, CancellationToken ct)
    {
        var classes = _db.Classes.Where(x => !x.IsCancelled && x.StartsAt >= query.From && x.StartsAt < query.To);

        if (query.RoomId != null)
        {
            classes = classes.Where(x => x.RoomId == query.RoomId);
        }

        if (query.After != null)
        {
            classes = classes.Where(x => EF.Functions.GreaterThan(
                ValueTuple.Create(x.StartsAt, x.Id),
                ValueTuple.Create(query.After.StartsAt, query.After.Id)));
        }

        var rows = await (
                from scheduledClass in classes
                join room in _db.Rooms on scheduledClass.RoomId equals room.Id
                orderby scheduledClass.StartsAt, scheduledClass.Id
                select new { Class = scheduledClass, Room = room })
            .Take(query.Limit + 1)
            .ToListAsync(ct);

        var page = rows.Take(query.Limit).ToList();
        var classIds = page.Select(x => x.Class.Id).ToList();

        var seats = await _db.Enrollments
            .Where(x => classIds.Contains(x.ClassId) && x.Status == EnrollmentStatus.Active)
            .GroupBy(x => x.ClassId)
            .Select(x => new
            {
                ClassId = x.Key,
                Booked = x.Sum(e => e.Seats),
                Mine = x.Sum(e => e.UserId == query.UserId ? e.Seats : 0)
            })
            .ToDictionaryAsync(x => x.ClassId, ct);

        var items = page
            .Select(x =>
            {
                var classSeats = seats.GetValueOrDefault(x.Class.Id);

                return new ScheduleItem(
                    x.Class.Id,
                    x.Class.Title,
                    x.Class.StartsAt,
                    x.Class.DurationMinutes,
                    new ScheduleRoom(x.Room.Id, x.Room.Name),
                    x.Class.Capacity,
                    Math.Max(0, x.Class.Capacity - (classSeats?.Booked ?? 0)),
                    classSeats?.Mine ?? 0);
            })
            .ToList();

        var next = rows.Count > query.Limit
            ? new ScheduleCursor(page[^1].Class.StartsAt, page[^1].Class.Id)
            : null;

        return new SchedulePage(items, next);
    }
}
