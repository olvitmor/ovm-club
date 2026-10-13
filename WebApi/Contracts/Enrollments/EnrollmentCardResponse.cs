using BusinessLogic.Enrollments;

namespace WebApi.Contracts.Enrollments;

public record EnrollmentCardResponse(
    long Id,
    string Status,
    int Seats,
    DateTime CreatedAt,
    EnrollmentCardClassResponse Class,
    EnrollmentCardPassResponse Pass)
{
    public static EnrollmentCardResponse From(EnrollmentCard card)
    {
        return new EnrollmentCardResponse(
            card.Id,
            card.Status,
            card.Seats,
            card.CreatedAt,
            new EnrollmentCardClassResponse(
                card.Class.Id,
                card.Class.Title,
                card.Class.StartsAt,
                card.Class.DurationMinutes,
                card.Class.IsCancelled,
                new RoomResponse(card.Class.Room.Id, card.Class.Room.Name)),
            new EnrollmentCardPassResponse(card.Pass.RemainingVisits, card.Pass.ValidUntil));
    }
}

public record EnrollmentCardClassResponse(
    long Id,
    string Title,
    DateTime StartsAt,
    int DurationMinutes,
    bool IsCancelled,
    RoomResponse Room);

public record EnrollmentCardPassResponse(int RemainingVisits, DateOnly ValidUntil);
