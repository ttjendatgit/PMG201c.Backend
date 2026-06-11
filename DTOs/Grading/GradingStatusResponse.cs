namespace PMG201c.Backend.DTOs.Grading;

public class GradingStatusResponse
{
    public Guid AssessmentId { get; set; }
    public int TotalSubmissions { get; set; }
    public int Uploaded { get; set; }
    public int Grading { get; set; }
    public int Graded { get; set; }
    public int Error { get; set; }
    public string? LatestJobStatus { get; set; }
    public Guid? LatestJobId { get; set; }
}
