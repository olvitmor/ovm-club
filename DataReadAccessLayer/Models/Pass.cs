using System;

namespace DataReadAccessLayer.Models;

public partial class Pass
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public int RemainingVisits { get; set; }

    public DateOnly ValidUntil { get; set; }
}
