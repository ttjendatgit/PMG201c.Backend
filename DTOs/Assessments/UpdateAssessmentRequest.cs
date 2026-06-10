namespace PMG201c.Backend.DTOs.Assessments;

public class UpdateAssessmentRequest
{
    public string? Title { get; set; }
    public string? CourseCode { get; set; }
    public string? Description { get; set; }
    public double? TotalRawScore { get; set; }
    public double? TotalConvertedScore { get; set; }
    public string? Status { get; set; }
}
