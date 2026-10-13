using BusinessLogic.Results;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.CancelEnrollment;

public sealed record CancelEnrollmentCommand(long UserId, long EnrollmentId) : ICommand<Result<CancelledEnrollment>>;
