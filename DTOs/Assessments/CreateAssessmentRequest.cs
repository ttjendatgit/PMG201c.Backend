using System.ComponentModel.DataAnnotations;

namespace PMG201c.Backend.DTOs.Assessments;

public class CreateAssessmentRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? CourseCode { get; set; }

    public string? Description { get; set; }
}
