using Microsoft.EntityFrameworkCore;

namespace DataReadAccessLayer;

public partial class ClubReadDbContext : DbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw ReadOnly();
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        throw ReadOnly();
    }

    private static InvalidOperationException ReadOnly()
    {
        return new InvalidOperationException($"{nameof(ClubReadDbContext)} is read-only");
    }
}
