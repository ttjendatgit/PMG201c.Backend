using System.ComponentModel.DataAnnotations;

namespace PMG201c.Backend.DTOs.Grading;

public class ManualGradingItemRequest
{
    [Required]
    public Guid RubricItemId { get; set; }

    [Required]
    public int QuestionNo { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "reviewedRawScore must be >= 0.")]
    public double ReviewedRawScore { get; set; }

    public string? TeacherComment { get; set; }
}
