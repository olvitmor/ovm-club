using BusinessLogic.Enrollments.CancelEnrollment;

namespace WebApi.Contracts.Enrollments.CancelEnrollment;

public record CancelledEnrollmentResponse(long Id, string Status, int RefundedVisits)
{
    public static CancelledEnrollmentResponse From(CancelledEnrollment enrollment)
    {
        return new CancelledEnrollmentResponse(enrollment.Id, enrollment.Status, enrollment.RefundedVisits);
    }
}
