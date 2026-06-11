namespace PMG201c.Backend.Services;

/// <summary>
/// Deterministic mock grading provider for pipeline testing.
/// Each (submission text, question number) pair always receives the same score (50–89 % of max).
/// Replace with a real provider (OpenRouter, Gemini, etc.) by implementing IAiGradingService.
/// </summary>
public class MockAiGradingService : IAiGradingService
{
    public Task<AiGradingResponse> GradeAsync(AiGradingRequest request, CancellationToken ct = default)
    {
        var preview = request.StudentText.Length > 120
            ? request.StudentText[..120] + "…"
            : request.StudentText;

        var items = request.RubricItems.Select(rubric =>
        {
            // Stable hash so same submission always gets same score per question
            var seed = StableHash($"{request.StudentText}|{rubric.QuestionNo}");
            var ratio = 0.50 + (seed % 40) / 100.0; // 50 – 89 %
            var awarded = Math.Round(rubric.MaxRawScore * ratio, 2);

            return new AiGradedItem(
                QuestionNo: rubric.QuestionNo,
                AwardedRawScore: awarded,
                Comment: $"[Mock] Assessed '{rubric.Title}' based on submission content. " +
                         $"Score ratio: {ratio:P0}.",
                Evidence: preview
            );
        }).ToList();

        return Task.FromResult(new AiGradingResponse(
            AiModel: "mock-v1",
            OverallComment: $"[Mock AI] Evaluated {request.RubricItems.Count} criteria across " +
                            $"{request.StudentText.Length} chars of student text.",
            Items: items
        ));
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return Math.Abs(h);
        }
    }
}
