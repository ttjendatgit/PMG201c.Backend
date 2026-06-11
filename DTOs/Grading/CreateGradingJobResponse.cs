namespace PMG201c.Backend.DTOs.Grading;

public class CreateGradingJobResponse
{
    public string Message { get; set; } = string.Empty;
    public GradingJobResponse Job { get; set; } = null!;
}
