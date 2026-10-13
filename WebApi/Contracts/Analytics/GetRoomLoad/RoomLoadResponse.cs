using BusinessLogic.Analytics.GetRoomLoad;

namespace WebApi.Contracts.Analytics.GetRoomLoad;

public record RoomLoadResponse(IReadOnlyList<RoomLoadItemResponse> Items);

public record RoomLoadItemResponse(
    DateOnly Date,
    RoomLoadRoomResponse Room,
    int ClassesCount,
    int TotalCapacity,
    int BookedSeats)
{
    public static RoomLoadItemResponse From(RoomLoad load)
    {
        return new RoomLoadItemResponse(
            load.Date,
            new RoomLoadRoomResponse(load.Room.Id, load.Room.Name),
            load.ClassesCount,
            load.TotalCapacity,
            load.BookedSeats);
    }
}

public record RoomLoadRoomResponse(long Id, string Name);
