using BusinessLogic.Results;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.ChangeEnrollmentSeats;

public sealed record ChangeEnrollmentSeatsCommand(long UserId, long EnrollmentId, int Seats)
    : ICommand<Result<EnrollmentCard>>;
