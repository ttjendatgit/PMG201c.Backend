namespace PMG201c.Backend.Models;

public class GradingJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssessmentId { get; set; }

    /// <summary>PENDING | RUNNING | COMPLETED | COMPLETED_WITH_ERRORS | ERROR</summary>
    public string Status { get; set; } = "PENDING";

    public int TotalSubmissions { get; set; }
    public int ProcessedSubmissions { get; set; }
    public int FailedSubmissions { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Assessment Assessment { get; set; } = null!;
    public ICollection<GradingResult> GradingResults { get; set; } = new List<GradingResult>();
}
