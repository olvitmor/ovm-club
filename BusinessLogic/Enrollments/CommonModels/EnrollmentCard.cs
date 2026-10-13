namespace BusinessLogic.Enrollments.CommonModels;

public record EnrollmentCard(
    long Id,
    string Status,
    int Seats,
    DateTime CreatedAt,
    EnrollmentCardClass Class,
    EnrollmentCardPass Pass);

public record EnrollmentCardClass(
    long Id,
    string Title,
    DateTime StartsAt,
    int DurationMinutes,
    bool IsCancelled,
    EnrollmentCardRoom Room);

public record EnrollmentCardRoom(long Id, string Name);

public record EnrollmentCardPass(int RemainingVisits, DateOnly ValidUntil);
