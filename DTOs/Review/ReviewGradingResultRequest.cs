using System.ComponentModel.DataAnnotations;

namespace PMG201c.Backend.DTOs.Review;

public class ReviewGradingResultRequest
{
    public string? TeacherOverallComment { get; set; }

    [Required]
    public List<ReviewGradingResultItemRequest> Items { get; set; } = new();
}
