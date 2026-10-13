using BusinessLogic.Results;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.CreateEnrollment;

public sealed record CreateEnrollmentCommand(long UserId, long ClassId, int Seats) : ICommand<Result<CreatedEnrollment>>;
