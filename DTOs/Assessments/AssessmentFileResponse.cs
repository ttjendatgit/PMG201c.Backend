namespace PMG201c.Backend.DTOs.Assessments;

public class AssessmentFileResponse
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public int Size { get; set; }
    public string? ExtractedText { get; set; }
    public string ExtractionStatus { get; set; } = string.Empty;
    public string? ExtractionError { get; set; }
    public DateTime CreatedAt { get; set; }
}
