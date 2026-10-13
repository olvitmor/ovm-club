using System;

namespace DataReadAccessLayer.Models;

public partial class WaitlistEntry
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long ClassId { get; set; }

    public int Seats { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public long? EnrollmentId { get; set; }
}
