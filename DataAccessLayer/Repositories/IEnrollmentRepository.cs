using Common.Database.Repositories.Generic;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IEnrollmentRepository : IGenericRepository<Enrollment>
{
    Task<List<Enrollment>> GetByUserAsync(long userId, CancellationToken ct = default);

    Task<int> GetActiveSeatsAsync(long classId, CancellationToken ct = default);

    Task<Dictionary<long, int>> GetActiveSeatsAsync(IReadOnlyCollection<long> classIds, CancellationToken ct = default);
}
