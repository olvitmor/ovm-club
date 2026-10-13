using System.ComponentModel.DataAnnotations;

namespace WebApi.Contracts.Analytics.GetRoomLoad;

public class GetRoomLoadRequest
{
    [Required]
    public DateOnly? From { get; set; }

    [Required]
    public DateOnly? To { get; set; }

    public long? RoomId { get; set; }
}
