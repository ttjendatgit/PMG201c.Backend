namespace PMG201c.Backend.DTOs.Grading;

public class GradingResultResponse
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid? GradingJobId { get; set; }

    // AI scores
    public double TotalRawScore { get; set; }
    public double TotalConvertedScore { get; set; }
    public string? AiOverallComment { get; set; }
    public string? AiModel { get; set; }

    // Teacher review scores
    public double? ReviewedRawScore { get; set; }
    public double? ReviewedConvertedScore { get; set; }
    public double? FinalRawScore { get; set; }
    public double? FinalConvertedScore { get; set; }
    public string? TeacherOverallComment { get; set; }
    public string ReviewStatus { get; set; } = "AI_GRADED";
    public DateTime? ReviewedAt { get; set; }
    public DateTime? FinalizedAt { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public List<GradingResultItemResponse> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
