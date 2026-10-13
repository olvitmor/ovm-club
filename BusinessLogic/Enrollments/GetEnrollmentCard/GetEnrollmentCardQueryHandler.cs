using BusinessLogic.Results;
using DataReadAccessLayer;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.GetEnrollmentCard;

public sealed class GetEnrollmentCardQueryHandler : IQueryHandler<GetEnrollmentCardQuery, Result<EnrollmentCard>>
{
    private readonly ClubReadDbContext _db;

    public GetEnrollmentCardQueryHandler(ClubReadDbContext db)
    {
        _db = db;
    }

    public async Task<Result<EnrollmentCard>> HandleAsync(GetEnrollmentCardQuery query, CancellationToken ct)
    {
        var card = await EnrollmentCardReader.ReadAsync(_db, query.UserId, query.EnrollmentId, ct);

        return card == null ? Result<EnrollmentCard>.NotFound() : card;
    }
}
