using System.ComponentModel.DataAnnotations;

namespace WebApi.Contracts.Enrollments.CreateEnrollment;

public class CreateEnrollmentRequest
{
    [Required]
    public long? ClassId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? Seats { get; set; }
}
