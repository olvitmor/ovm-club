using Common.Database.Interfaces;

namespace DataAccessLayer.Entities;

public class IdempotencyKey : IEntity
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Operation { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string RequestHash { get; set; } = null!;

    public string? ResponseBody { get; set; }

    public DateTime CreatedAt { get; set; }
}
