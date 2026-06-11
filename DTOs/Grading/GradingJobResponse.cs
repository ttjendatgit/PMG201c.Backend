namespace PMG201c.Backend.DTOs.Grading;

public class GradingJobResponse
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalSubmissions { get; set; }
    public int ProcessedSubmissions { get; set; }
    public int FailedSubmissions { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
