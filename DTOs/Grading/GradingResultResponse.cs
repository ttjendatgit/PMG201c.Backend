namespace PMG201c.Backend.DTOs.Grading;

public class GradingResultResponse
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid? GradingJobId { get; set; }
    public double TotalRawScore { get; set; }
    public double TotalConvertedScore { get; set; }
    public string? AiOverallComment { get; set; }
    public string? AiModel { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public List<GradingResultItemResponse> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
