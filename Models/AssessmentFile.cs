namespace PMG201c.Backend.Models;

public class AssessmentFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssessmentId { get; set; }

    public string FileType { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string? MimeType { get; set; }

    public int Size { get; set; }

    public string? ExtractedText { get; set; }

    public string ExtractionStatus { get; set; } = "PENDING";

    public string? ExtractionError { get; set; }

    public Assessment Assessment { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}