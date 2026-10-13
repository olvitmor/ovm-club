using System;

namespace DataReadAccessLayer.Models;

public partial class Class
{
    public long Id { get; set; }

    public long RoomId { get; set; }

    public string Title { get; set; } = null!;

    public DateTime StartsAt { get; set; }

    public int DurationMinutes { get; set; }

    public int Capacity { get; set; }

    public bool IsCancelled { get; set; }
}
