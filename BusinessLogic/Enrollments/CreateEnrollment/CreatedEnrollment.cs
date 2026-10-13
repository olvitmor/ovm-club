namespace BusinessLogic.Enrollments.CreateEnrollment;

public record CreatedEnrollment(long Id, long ClassId, int Seats, string Status, DateTime CreatedAt);
