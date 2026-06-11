namespace PMG201c.Backend.Models;

public class GradingResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid? GradingJobId { get; set; }

    public double TotalRawScore { get; set; }
    public double TotalConvertedScore { get; set; }
    public string? AiOverallComment { get; set; }
    public string? AiModel { get; set; }

    /// <summary>GRADED | ERROR</summary>
    public string Status { get; set; } = "GRADED";
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Assessment Assessment { get; set; } = null!;
    public Submission Submission { get; set; } = null!;
    public GradingJob? GradingJob { get; set; }
    public ICollection<GradingResultItem> Items { get; set; } = new List<GradingResultItem>();
}
