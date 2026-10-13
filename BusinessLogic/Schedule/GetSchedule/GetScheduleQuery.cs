using Ovm.Cqrs;

namespace BusinessLogic.Schedule.GetSchedule;

public sealed record GetScheduleQuery(
    long UserId,
    DateTime From,
    DateTime To,
    long? RoomId,
    int Limit,
    ScheduleCursor? After) : IQuery<SchedulePage>;
