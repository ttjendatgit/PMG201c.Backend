namespace PMG201c.Backend.DTOs.Submissions;

public class SubmissionResponse
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public string? StudentId { get; set; }
    public string? StudentName { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public string? ExtractedText { get; set; }
    public string ExtractionStatus { get; set; } = string.Empty;
    public string? ExtractionError { get; set; }
    public string GradingStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
