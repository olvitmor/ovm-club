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
}
