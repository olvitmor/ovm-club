using Common.Database.Repositories.Generic;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IWaitlistRepository : IGenericRepository<WaitlistEntry>
{
    Task<bool> IsWaitingAsync(long userId, long classId, CancellationToken ct = default);

    Task<List<WaitlistEntry>> GetWaitingAsync(long classId, int maxSeats, CancellationToken ct = default);

    Task<int> GetPositionAsync(WaitlistEntry entry, CancellationToken ct = default);
}
