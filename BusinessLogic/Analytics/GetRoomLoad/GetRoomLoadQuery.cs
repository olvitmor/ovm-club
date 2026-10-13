using Ovm.Cqrs;

namespace BusinessLogic.Analytics.GetRoomLoad;

public sealed record GetRoomLoadQuery(DateOnly From, DateOnly To, long? RoomId) : IQuery<IReadOnlyList<RoomLoad>>;
