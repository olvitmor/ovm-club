using Common.Database.Repositories.Generic;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IIdempotencyKeyRepository : IGenericRepository<IdempotencyKey>
{
    Task<IdempotencyKey?> TryAcquireAsync(IdempotencyKey key, DateTime expiredBefore, CancellationToken ct = default);

    Task<IdempotencyKey?> GetAsync(long userId, string operation, string key, CancellationToken ct = default);

    Task<int> DeleteExpiredAsync(DateTime expiredBefore, int batchSize, CancellationToken ct = default);
}
