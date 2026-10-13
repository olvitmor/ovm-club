using System.ComponentModel.DataAnnotations;

namespace WebApi.Contracts.Enrollments.ChangeEnrollmentSeats;

public class ChangeEnrollmentSeatsRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? Seats { get; set; }
}
