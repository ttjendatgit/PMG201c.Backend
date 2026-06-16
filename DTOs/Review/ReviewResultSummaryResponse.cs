namespace PMG201c.Backend.DTOs.Review;

public class ReviewResultSummaryResponse
{
    public Guid GradingResultId { get; set; }
    public Guid SubmissionId { get; set; }
    public string? StudentId { get; set; }
    public string? StudentName { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;

    // AI totals
    public double AiTotalRawScore { get; set; }
    public double AiTotalConvertedScore { get; set; }

    // Teacher review totals
    public double? ReviewedRawScore { get; set; }
    public double? ReviewedConvertedScore { get; set; }

    // Final totals
    public double? FinalRawScore { get; set; }
    public double? FinalConvertedScore { get; set; }

    public string ReviewStatus { get; set; } = "AI_GRADED";
    public string? TeacherOverallComment { get; set; }
    public DateTime UpdatedAt { get; set; }
}
