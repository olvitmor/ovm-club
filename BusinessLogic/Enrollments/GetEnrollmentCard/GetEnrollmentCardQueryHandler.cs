using BusinessLogic.Enrollments.CommonModels;
using BusinessLogic.Enrollments.Helpers;
using BusinessLogic.Enrollments.Services;
using BusinessLogic.Results;
using DataReadAccessLayer;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.GetEnrollmentCard;

public sealed class GetEnrollmentCardQueryHandler : IQueryHandler<GetEnrollmentCardQuery, Result<EnrollmentCard>>
{
    private readonly ClubReadDbContext _db;
    private readonly EnrollmentCardCache _cache;

    public GetEnrollmentCardQueryHandler(ClubReadDbContext db, EnrollmentCardCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<EnrollmentCard>> HandleAsync(GetEnrollmentCardQuery query, CancellationToken ct)
    {
        var card = await _cache.GetOrLoadAsync(
            query.UserId,
            query.EnrollmentId,
            () => EnrollmentCardReader.ReadAsync(_db, query.UserId, query.EnrollmentId, ct),
            ct);

        return card == null ? Result<EnrollmentCard>.NotFound() : card;
    }
}
