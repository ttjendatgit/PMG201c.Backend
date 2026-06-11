namespace PMG201c.Backend.DTOs.Grading;

public class GradingResultItemResponse
{
    public Guid Id { get; set; }
    public Guid GradingResultId { get; set; }
    public Guid? RubricItemId { get; set; }
    public int QuestionNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public double MaxRawScore { get; set; }
    public double AwardedRawScore { get; set; }
    public double AwardedConvertedScore { get; set; }
    public string? AiComment { get; set; }
    public string? Evidence { get; set; }
    public DateTime CreatedAt { get; set; }
}
