using BusinessLogic.Results;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.GetEnrollmentCard;

public sealed record GetEnrollmentCardQuery(long UserId, long EnrollmentId) : IQuery<Result<EnrollmentCard>>;
