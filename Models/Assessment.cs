namespace PMG201c.Backend.Models;

public class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeacherId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? CourseCode { get; set; }

    public string? Description { get; set; }

    public string? QuestionText { get; set; }

    public string? GuideText { get; set; }

    public double TotalRawScore { get; set; } = 100;

    public double TotalConvertedScore { get; set; } = 10;

    public string Status { get; set; } = "DRAFT";

    public User Teacher { get; set; } = null!;

    public ICollection<AssessmentFile> Files { get; set; } = new List<AssessmentFile>();

    public ICollection<RubricItem> RubricItems { get; set; } = new List<RubricItem>();

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}