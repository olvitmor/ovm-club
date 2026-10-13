using Common.Database.Repositories.Generic;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class WaitlistRepository : GenericRepository<WaitlistEntry>, IWaitlistRepository
{
    public WaitlistRepository(DbSet<WaitlistEntry> dbSet)
        : base(dbSet)
    {
    }

    public async Task<bool> IsWaitingAsync(long userId, long classId, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(
            x => x.UserId == userId && x.ClassId == classId && x.Status == WaitlistStatus.Waiting,
            ct);
    }

    public async Task<List<WaitlistEntry>> GetWaitingAsync(long classId, int maxSeats, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(x => x.ClassId == classId && x.Status == WaitlistStatus.Waiting && x.Seats <= maxSeats)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<int> GetPositionAsync(WaitlistEntry entry, CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(
            x => x.ClassId == entry.ClassId
                && x.Status == WaitlistStatus.Waiting
                && (x.CreatedAt < entry.CreatedAt || (x.CreatedAt == entry.CreatedAt && x.Id <= entry.Id)),
            ct);
    }
}
