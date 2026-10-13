using BusinessLogic.Enrollments.CreateEnrollment;

namespace WebApi.Contracts.Enrollments.CreateEnrollment;

public record CreatedEnrollmentResponse(long Id, long ClassId, int Seats, string Status, DateTime CreatedAt)
{
    public static CreatedEnrollmentResponse From(CreatedEnrollment enrollment)
    {
        return new CreatedEnrollmentResponse(
            enrollment.Id,
            enrollment.ClassId,
            enrollment.Seats,
            enrollment.Status,
            enrollment.CreatedAt);
    }
}
