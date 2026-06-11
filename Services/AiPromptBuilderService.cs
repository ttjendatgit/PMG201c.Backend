using System.Text;

namespace PMG201c.Backend.Services;

/// <summary>
/// Builds structured prompts to send to a real AI provider.
/// Used by production IAiGradingService implementations (OpenRouter, Gemini, etc.).
/// The mock does not call this, but the output is logged for debugging.
/// </summary>
public class AiPromptBuilderService
{
    public string BuildSystemPrompt() => """
        You are an expert academic grading assistant. Grade the student submission strictly
        against the rubric provided. For each rubric item return:
          - questionNo   : integer matching the rubric item
          - awardedRawScore : number between 0 and maxRawScore (DO NOT exceed maxRawScore)
          - comment      : concise feedback explaining the awarded score
          - evidence     : exact quote from the student submission supporting the score

        Respond ONLY with valid JSON in the following shape (no markdown fences):
        {
          "overallComment": "...",
          "items": [
            { "questionNo": 1, "awardedRawScore": 15, "comment": "...", "evidence": "..." }
          ]
        }
        """;

    public string BuildUserPrompt(AiGradingRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Exam Question");
        sb.AppendLine(request.QuestionText);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(request.GuideText))
        {
            sb.AppendLine("## Grading Guide / Model Answer");
            sb.AppendLine(request.GuideText);
            sb.AppendLine();
        }

        sb.AppendLine("## Rubric");
        foreach (var item in request.RubricItems)
        {
            sb.AppendLine($"  {item.QuestionNo}. {item.Title}  [max {item.MaxRawScore} pts]");
            if (!string.IsNullOrWhiteSpace(item.Description))
                sb.AppendLine($"     {item.Description}");
        }
        sb.AppendLine();

        sb.AppendLine("## Student Submission");
        sb.AppendLine(request.StudentText);

        return sb.ToString();
    }
}
