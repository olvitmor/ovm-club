using System.ComponentModel.DataAnnotations;

namespace WebApi.Contracts.Schedule.GetSchedule;

public class GetScheduleRequest
{
    [Required]
    public DateTime? From { get; set; }

    [Required]
    public DateTime? To { get; set; }

    public long? RoomId { get; set; }

    public int? Limit { get; set; }

    public string? ContinuationToken { get; set; }
}
