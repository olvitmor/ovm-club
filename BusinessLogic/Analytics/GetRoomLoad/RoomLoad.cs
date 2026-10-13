namespace BusinessLogic.Analytics.GetRoomLoad;

public record RoomLoad(DateOnly Date, RoomLoadRoom Room, int ClassesCount, int TotalCapacity, int BookedSeats);

public record RoomLoadRoom(long Id, string Name);
