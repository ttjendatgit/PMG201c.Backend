using System.ComponentModel.DataAnnotations;

namespace PMG201c.Backend.DTOs.Review;

public class ReviewGradingResultItemRequest
{
    [Required]
    public Guid GradingResultItemId { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "reviewedRawScore must be >= 0.")]
    public double ReviewedRawScore { get; set; }

    public string? TeacherComment { get; set; }
}
