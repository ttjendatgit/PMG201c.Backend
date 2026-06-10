namespace PMG201c.Backend.DTOs.Assessments;

public class AssessmentResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CourseCode { get; set; }
    public string? Description { get; set; }
    public double TotalRawScore { get; set; }
    public double TotalConvertedScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
