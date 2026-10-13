using BusinessLogic.Schedule.GetSchedule;

namespace WebApi.Contracts.Schedule.GetSchedule;

public record ScheduleResponse(IReadOnlyList<ScheduleItemResponse> Items, string? ContinuationToken);

public record ScheduleItemResponse(
    long ClassId,
    string Title,
    DateTime StartsAt,
    int DurationMinutes,
    ScheduleRoomResponse Room,
    int Capacity,
    int FreeSeats,
    int MySeats)
{
    public static ScheduleItemResponse From(ScheduleItem item)
    {
        return new ScheduleItemResponse(
            item.ClassId,
            item.Title,
            item.StartsAt,
            item.DurationMinutes,
            new ScheduleRoomResponse(item.Room.Id, item.Room.Name),
            item.Capacity,
            item.FreeSeats,
            item.MySeats);
    }
}

public record ScheduleRoomResponse(long Id, string Name);
