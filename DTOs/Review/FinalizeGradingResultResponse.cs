using PMG201c.Backend.DTOs.Grading;

namespace PMG201c.Backend.DTOs.Review;

public class FinalizeGradingResultResponse
{
    public string Message { get; set; } = string.Empty;
    public GradingResultResponse Result { get; set; } = null!;
}
