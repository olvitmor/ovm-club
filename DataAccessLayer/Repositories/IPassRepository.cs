using Common.Database.Repositories.Generic;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IPassRepository : IGenericRepository<Pass>
{
    Task<Pass?> GetByUserAsync(long userId, CancellationToken ct = default);

    Task<List<Pass>> LockByUsersAsync(IReadOnlyCollection<long> userIds, CancellationToken ct = default);
}
