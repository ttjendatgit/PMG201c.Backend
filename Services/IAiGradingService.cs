namespace PMG201c.Backend.Services;

// ── Input records ────────────────────────────────────────────────────────────

public record RubricItemInput(
    int QuestionNo,
    string Title,
    string? Description,
    double MaxRawScore,
    double MaxConvertedScore
);

public record AiGradingRequest(
    string QuestionText,
    string? GuideText,
    string StudentText,
    IList<RubricItemInput> RubricItems
);

// ── Output records ───────────────────────────────────────────────────────────

public record AiGradedItem(
    int QuestionNo,
    double AwardedRawScore,
    string? Comment,
    string? Evidence
);

public record AiGradingResponse(
    string AiModel,
    string? OverallComment,
    IList<AiGradedItem> Items
);

// ── Interface ────────────────────────────────────────────────────────────────

public interface IAiGradingService
{
    Task<AiGradingResponse> GradeAsync(AiGradingRequest request, CancellationToken ct = default);
}
