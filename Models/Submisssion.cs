namespace PMG201c.Backend.Models;

public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssessmentId { get; set; }

    public string? StudentId { get; set; }

    public string? StudentName { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public int FileSize { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public string? ExtractedText { get; set; }

    public string ExtractionStatus { get; set; } = "PENDING";

    public string GradingStatus { get; set; } = "UPLOADED";

    public string? ExtractionError { get; set; }

    public Assessment Assessment { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}