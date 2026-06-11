namespace PMG201c.Backend.DTOs.Rubric;

public class RubricItemResponse
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public int QuestionNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double MaxRawScore { get; set; }
    public double MaxConvertedScore { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
