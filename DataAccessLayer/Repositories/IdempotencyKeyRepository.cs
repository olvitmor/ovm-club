using Common.Database.Repositories.Generic;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class IdempotencyKeyRepository : GenericRepository<IdempotencyKey>, IIdempotencyKeyRepository
{
    public IdempotencyKeyRepository(DbSet<IdempotencyKey> dbSet)
        : base(dbSet)
    {
    }

    public async Task<IdempotencyKey?> TryAcquireAsync(
        IdempotencyKey key,
        DateTime expiredBefore,
        CancellationToken ct = default)
    {
        // решение: SQL, а не EF — в EF нет ON CONFLICT. Add + SaveChanges на дубликате ломает транзакцию Postgres (25P02),
        // а SELECT перед INSERT — гонка. Один запрос атомарно вставляет ключ, ждёт параллельный и перезаписывает просроченный.
        var acquired = await _dbSet
            .FromSql($"""
                INSERT INTO idempotency_keys (user_id, operation, key, request_hash, created_at)
                VALUES ({key.UserId}, {key.Operation}, {key.Key}, {key.RequestHash}, {key.CreatedAt})
                ON CONFLICT (user_id, operation, key) DO UPDATE
                    SET request_hash = EXCLUDED.request_hash,
                        response_body = NULL,
                        created_at = EXCLUDED.created_at
                    WHERE idempotency_keys.created_at < {expiredBefore}
                RETURNING *
                """)
            .ToListAsync(ct);

        return acquired.SingleOrDefault();
    }

    public async Task<IdempotencyKey?> GetAsync(long userId, string operation, string key, CancellationToken ct = default)
    {
        return await _dbSet.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Operation == operation && x.Key == key,
            ct);
    }

    public async Task<int> DeleteExpiredAsync(DateTime expiredBefore, int batchSize, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(x => x.CreatedAt < expiredBefore)
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ExecuteDeleteAsync(ct);
    }
}
