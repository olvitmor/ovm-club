using Common.Database.Repositories.Generic;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class EnrollmentRepository : GenericRepository<Enrollment>, IEnrollmentRepository
{
    public EnrollmentRepository(DbSet<Enrollment> dbSet)
        : base(dbSet)
    {
    }

    public async Task<List<Enrollment>> GetByUserAsync(long userId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<int> GetActiveSeatsAsync(long classId, CancellationToken ct = default)
    {
        var seats = await GetActiveSeatsAsync([classId], ct);

        return seats[classId];
    }

    public async Task<Dictionary<long, int>> GetActiveSeatsAsync(
        IReadOnlyCollection<long> classIds,
        CancellationToken ct = default)
    {
        var ids = classIds.Distinct().ToList();

        var seats = await _dbSet
            .Where(x => ids.Contains(x.ClassId) && x.Status == EnrollmentStatus.Active)
            .GroupBy(x => x.ClassId)
            .Select(x => new { ClassId = x.Key, Seats = x.Sum(e => e.Seats) })
            .ToDictionaryAsync(x => x.ClassId, x => x.Seats, ct);

        return ids.ToDictionary(id => id, id => seats.GetValueOrDefault(id));
    }
}
