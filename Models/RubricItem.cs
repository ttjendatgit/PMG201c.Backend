
namespace PMG201c.Backend.Models;

public class RubricItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssessmentId { get; set; }

    public int QuestionNo { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public double MaxRawScore { get; set; }

    public double MaxConvertedScore { get; set; }

    public int OrderIndex { get; set; }

    public Assessment Assessment { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}