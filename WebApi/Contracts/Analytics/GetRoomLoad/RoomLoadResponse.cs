using BusinessLogic.Analytics.GetRoomLoad;
using WebApi.Contracts.CommonModels;

namespace WebApi.Contracts.Analytics.GetRoomLoad;

public record RoomLoadResponse(IReadOnlyList<RoomLoadItemResponse> Items);

public record RoomLoadItemResponse(
    DateOnly Date,
    RoomResponse Room,
    int ClassesCount,
    int TotalCapacity,
    int BookedSeats)
{
    public static RoomLoadItemResponse From(RoomLoad load)
    {
        return new RoomLoadItemResponse(
            load.Date,
            new RoomResponse(load.Room.Id, load.Room.Name),
            load.ClassesCount,
            load.TotalCapacity,
            load.BookedSeats);
    }
}
