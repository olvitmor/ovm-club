namespace BusinessLogic.Schedule.GetSchedule;

public record SchedulePage(IReadOnlyList<ScheduleItem> Items, ScheduleCursor? Next);

public record ScheduleItem(
    long ClassId,
    string Title,
    DateTime StartsAt,
    int DurationMinutes,
    ScheduleRoom Room,
    int Capacity,
    int FreeSeats,
    int MySeats);

public record ScheduleRoom(long Id, string Name);

public record ScheduleCursor(DateTime StartsAt, long Id);
