namespace PMG201c.Backend.Models;

public class GradingResultItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GradingResultId { get; set; }

    /// <summary>Stored for traceability; NOT a FK so rubric can be changed without breaking history.</summary>
    public Guid? RubricItemId { get; set; }

    public int QuestionNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public double MaxRawScore { get; set; }
    public double AwardedRawScore { get; set; }
    public double AwardedConvertedScore { get; set; }
    public string? AiComment { get; set; }
    public string? Evidence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public GradingResult GradingResult { get; set; } = null!;
}
