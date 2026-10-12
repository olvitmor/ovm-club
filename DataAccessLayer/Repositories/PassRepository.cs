using Common.Database.Repositories.Generic;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class PassRepository : GenericRepository<Pass>, IPassRepository
{
    public PassRepository(DbSet<Pass> dbSet)
        : base(dbSet)
    {
    }

    public async Task<Pass?> GetByUserAsync(long userId, CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.UserId == userId, ct);
    }

    public async Task<List<Pass>> LockByUsersAsync(IReadOnlyCollection<long> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var ids = userIds.Distinct().ToArray();

        return await _dbSet
            .FromSql($"SELECT * FROM passes WHERE user_id = ANY({ids}) ORDER BY id FOR UPDATE")
            .ToListAsync(ct);
    }
}
