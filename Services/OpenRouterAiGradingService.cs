using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PMG201c.Backend.Configuration;

namespace PMG201c.Backend.Services;

public sealed class OpenRouterAiGradingService : IAiGradingService
{
    private readonly HttpClient _http;
    private readonly AiOptions _aiOptions;
    private readonly OpenRouterOptions _options;
    private readonly AiPromptBuilderService _promptBuilder;
    private readonly ILogger<OpenRouterAiGradingService> _log;

    public OpenRouterAiGradingService(
        HttpClient http,
        IOptions<AiOptions> options,
        AiPromptBuilderService promptBuilder,
        ILogger<OpenRouterAiGradingService> log)
    {
        _http          = http;
        _aiOptions     = options.Value;
        _options       = options.Value.OpenRouter;
        _promptBuilder = promptBuilder;
        _log           = log;
    }

    public async Task<AiGradingResponse> GradeAsync(AiGradingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("OpenRouter API key is not configured.");

        string? retryInstruction = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await GradeOnceAsync(request, retryInstruction, ct);
            }
            catch (AiParsingException ex)
            {
                _log.LogWarning("[OpenRouter] Attempt {A}/2 – invalid JSON: {Msg}", attempt + 1, ex.Message);
                retryInstruction = _promptBuilder.BuildRetryInstruction(
                    "Phản hồi không phải JSON hợp lệ. Trả về CHỈ JSON theo schema, không markdown.");
                if (attempt == 1) throw;
            }
            catch (AiValidationException ex)
            {
                _log.LogWarning("[OpenRouter] Attempt {A}/2 – validation failed: {Msg}", attempt + 1, ex.Message);
                retryInstruction = _promptBuilder.BuildRetryInstruction(ex.Message);
                if (attempt == 1) throw;
            }
            catch (AiTransientException ex)
            {
                _log.LogWarning("[OpenRouter] Attempt {A}/2 – transient error: {Msg}", attempt + 1, ex.Message);
                if (attempt == 1) throw;
            }
        }

        // Unreachable but satisfies the compiler
        throw new InvalidOperationException("AI grading failed after retries.");
    }

    // ── Single attempt ────────────────────────────────────────────────────────

    private async Task<AiGradingResponse> GradeOnceAsync(
        AiGradingRequest request, string? retryInstruction, CancellationToken ct)
    {
        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userPrompt   = _promptBuilder.BuildUserPrompt(request);

        if (retryInstruction is not null)
            userPrompt += retryInstruction;

        var body = new
        {
            model       = _options.Model,
            messages    = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt   }
            },
            max_tokens  = _options.MaxTokens,
            temperature = _options.Temperature
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        req.Content = JsonContent.Create(body);

        _log.LogInformation(
            "[AI] Provider={Provider} Model={Model} RubricItems={Items} SubmissionLen={Len}{Retry}",
            _aiOptions.Provider,
            _options.Model,
            request.RubricItems.Count,
            request.StudentText.Length,
            retryInstruction != null ? " [RETRY]" : "");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(req, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new AiTransientException($"Request timed out after {_options.TimeoutSeconds}s.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AiTransientException($"Network error: {ex.Message}", ex);
        }

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                                 or System.Net.HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                "OpenRouter authentication failed. Check your API key configuration.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new AiTransientException("OpenRouter rate limit exceeded (429). Retrying.");

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            var preview = errBody.Length > 300 ? errBody[..300] + "…" : errBody;
            throw new InvalidOperationException(
                $"OpenRouter returned HTTP {(int)response.StatusCode}: {preview}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        _log.LogInformation("[OpenRouter] Response received, length={Len}", responseJson.Length);

        string content;
        try
        {
            content = ExtractChoiceContent(responseJson);
        }
        catch (Exception ex)
        {
            throw new AiParsingException(
                $"Failed to extract content from OpenRouter response: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(content))
            throw new AiParsingException("AI returned an empty response.");

        var aiResponse = ParseAiContent(content);

        ValidateAiResponse(aiResponse, request.RubricItems);

        // Safety-net: cap scores for sections that are absent from the submission
        aiResponse = ApplyHeuristicCaps(aiResponse, request.StudentText, request.RubricItems);

        return aiResponse;
    }

    // ── Heuristic post-processing ─────────────────────────────────────────────

    private AiGradingResponse ApplyHeuristicCaps(
        AiGradingResponse response, string studentText, IList<RubricItemInput> rubricItems)
    {
        var (usesMarkers, presentSections) =
            AiPromptBuilderService.DetectSectionMarkers(studentText, rubricItems);

        if (!usesMarkers) return response;

        var rubricNos = rubricItems.Select(r => r.QuestionNo).ToHashSet();
        var newItems  = response.Items.ToList();
        var modified  = false;

        for (var i = 0; i < newItems.Count; i++)
        {
            var item = newItems[i];
            if (!rubricNos.Contains(item.QuestionNo)) continue;
            if (presentSections.Contains(item.QuestionNo)) continue;
            if (item.AwardedRawScore <= 0) continue;

            _log.LogWarning(
                "[Heuristic] Capped questionNo={No} score {Old} → 0: section marker absent in submission.",
                item.QuestionNo, item.AwardedRawScore);

            newItems[i] = item with { AwardedRawScore = 0 };
            modified    = true;
        }

        return modified ? response with { Items = newItems } : response;
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static readonly HashSet<string> PlaceholderPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "nhận xét tổng quan bằng tiếng việt",
        "nhận xét ngắn gọn bằng tiếng việt",
        "nhận xét tổng quan",
        "tóm tắt bài làm: điểm mạnh là ..., còn thiếu ...",
        "ví dụ: bài làm đã nêu được x nhưng chưa đề cập y và z.",
        "overall comment",
        "nhận xét ở đây",
        "comment here",
    };

    private static readonly string[] PositivePraiseKeywords =
    {
        "đã hoàn chỉnh", "đầy đủ các ý", "nêu đầy đủ", "trình bày đầy đủ",
        "rất xuất sắc", "xuất sắc", "tuyệt vời", "hoàn hảo", "rất tốt",
        "làm rất tốt", "đã làm tốt", "thể hiện tốt", "sinh viên hiểu rõ",
        "sinh viên nắm vững", "đã nêu đủ", "đáp ứng đầy đủ", "trả lời đúng",
    };

    // Phrases meaning "completely absent" used to detect score/comment contradictions.
    private static readonly string[] StrongMissingKeywords =
    {
        "hoàn toàn không", "hoàn toàn thiếu", "không có bất kỳ", "không hề có",
        "không đề cập đến", "không được trả lời", "hoàn toàn bỏ sót",
        "completely missing", "not addressed at all", "entirely absent",
    };

    // Values the AI writes in the evidence field when nothing was found.
    private static readonly HashSet<string> NoEvidencePhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "không có bằng chứng",
        "không có",
        "không tìm thấy",
        "thiếu bằng chứng",
        "n/a",
        "na",
        "none",
        "no evidence",
        "không",
    };

    private static void ValidateAiResponse(AiGradingResponse response, IList<RubricItemInput> rubricItems)
    {
        // 1. Overall comment must not be empty or a placeholder.
        if (string.IsNullOrWhiteSpace(response.OverallComment))
            throw new AiValidationException("overallComment bị trống.");

        var overallNorm = response.OverallComment.Trim().ToLowerInvariant();
        if (PlaceholderPhrases.Contains(overallNorm))
            throw new AiValidationException(
                $"overallComment là câu mẫu placeholder: '{response.OverallComment}'");

        // 2. All expected rubric items must be present (extras are silently ignored).
        var rubricNos   = rubricItems.Select(r => r.QuestionNo).ToHashSet();
        var responseNos = response.Items.Select(i => i.QuestionNo).ToHashSet();
        var missing     = rubricNos.Except(responseNos).OrderBy(x => x).ToList();
        if (missing.Any())
            throw new AiValidationException(
                $"Thiếu tiêu chí rubric: questionNo = {string.Join(", ", missing)}");

        // 3. Per-item checks (only for items that belong to the rubric).
        var relevantItems = response.Items.Where(i => rubricNos.Contains(i.QuestionNo)).ToList();
        var allZero       = relevantItems.All(i => i.AwardedRawScore == 0);

        foreach (var item in relevantItems)
        {
            var rubric        = rubricItems.First(r => r.QuestionNo == item.QuestionNo);
            var commentLower  = (item.Comment  ?? "").ToLowerInvariant();
            var evidenceLower = (item.Evidence ?? "").Trim().ToLowerInvariant();

            // Empty comment
            if (string.IsNullOrWhiteSpace(item.Comment))
                throw new AiValidationException(
                    $"Nhận xét (comment) của tiêu chí questionNo={item.QuestionNo} bị trống.");

            // Comment copies the rubric title or description verbatim
            if (IsCommentCopiedFromRubric(item.Comment, rubric))
                throw new AiValidationException(
                    $"Nhận xét questionNo={item.QuestionNo} là bản sao tiêu đề/mô tả rubric. " +
                    "Nhận xét phải dựa trên bài làm thực tế của sinh viên.");

            // Comment contains a known placeholder phrase
            if (PlaceholderPhrases.Any(p => commentLower.Contains(p)))
                throw new AiValidationException(
                    $"Nhận xét questionNo={item.QuestionNo} chứa câu mẫu placeholder.");

            // All scores are 0 but this item's comment is clearly praising — contradiction
            if (allZero && PositivePraiseKeywords.Any(k => commentLower.Contains(k)))
                throw new AiValidationException(
                    $"Tất cả điểm là 0 nhưng nhận xét questionNo={item.QuestionNo} có vẻ khen ngợi – " +
                    "mâu thuẫn giữa điểm và nhận xét.");

            // Score > 0 but evidence is empty or explicitly says "no evidence"
            if (item.AwardedRawScore > 0)
            {
                var evidenceIsAbsent = string.IsNullOrWhiteSpace(item.Evidence)
                    || NoEvidencePhrases.Contains(evidenceLower)
                    || NoEvidencePhrases.Any(p => evidenceLower.StartsWith(p, StringComparison.Ordinal));

                if (evidenceIsAbsent)
                    throw new AiValidationException(
                        $"Tiêu chí questionNo={item.QuestionNo}: điểm {item.AwardedRawScore} > 0 " +
                        "nhưng evidence trống hoặc ghi 'Không có bằng chứng'. " +
                        "Phải trích dẫn bằng chứng từ bài làm, hoặc đặt điểm về 0.");
            }

            // High score but comment strongly implies the content is completely missing
            if (item.AwardedRawScore > rubric.MaxRawScore * 0.5
                && StrongMissingKeywords.Any(k => commentLower.Contains(k)))
            {
                throw new AiValidationException(
                    $"Mâu thuẫn questionNo={item.QuestionNo}: điểm {item.AwardedRawScore}/{rubric.MaxRawScore} " +
                    "nhưng nhận xét chỉ nội dung hoàn toàn thiếu. " +
                    "Điều chỉnh điểm về 0 hoặc sửa nhận xét cho phù hợp.");
            }
        }
    }

    // Returns true when the comment is essentially copied from the rubric definition.
    private static bool IsCommentCopiedFromRubric(string comment, RubricItemInput rubric)
    {
        static string Normalize(string s) =>
            s.Trim().ToLowerInvariant().TrimEnd('.', '!', ',', ';', ':');

        var normComment = Normalize(comment);
        var normTitle   = Normalize(rubric.Title);

        if (normComment == normTitle) return true;

        // Title is a leading prefix and comment adds fewer than 25 extra characters
        if (normComment.StartsWith(normTitle, StringComparison.Ordinal)
            && comment.Length < rubric.Title.Length + 25) return true;

        if (!string.IsNullOrWhiteSpace(rubric.Description))
        {
            var normDesc = Normalize(rubric.Description);
            if (normComment == normDesc) return true;
        }

        return false;
    }

    // ── Response extraction ──────────────────────────────────────────────────

    private string ExtractChoiceContent(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");

        var content = message.GetProperty("content").GetString() ?? "";
        var reasoning = message.TryGetProperty("reasoning_content", out var rc)
            ? rc.GetString() ?? "" : "";

        _log.LogInformation("[ExtractChoice] contentLen={CLen} reasoningLen={RLen} contentStarts={CStart} reasoningStarts={RStart}",
            content.Length, reasoning.Length,
            content.Length > 0 ? content[..Math.Min(50, content.Length)] : "(empty)",
            reasoning.Length > 0 ? reasoning[..Math.Min(50, reasoning.Length)] : "(empty)");

        // If content looks like valid JSON, use it
        if (!string.IsNullOrWhiteSpace(content) && content.TrimStart().StartsWith('{'))
            return content;

        // Otherwise try reasoning_content (some models put the answer there)
        if (!string.IsNullOrWhiteSpace(reasoning) && reasoning.TrimStart().StartsWith('{'))
            return reasoning;

        // Fallback to content even if not JSON (ParseAiContent will handle it)
        return content;
    }

    // ── Robust JSON parsing ──────────────────────────────────────────────────

    private AiGradingResponse ParseAiContent(string content)
    {
        // 1. Direct parse
        var result = TryDeserializeAiJson(content);
        if (result is not null) return result;

        // 2. Strip markdown code fences
        var stripped = StripMarkdownFences(content);
        result = TryDeserializeAiJson(stripped);
        if (result is not null) return result;

        // 3. Extract first { … } JSON object from surrounding text
        var extracted = ExtractFirstJsonObject(stripped);
        if (extracted is not null)
        {
            result = TryDeserializeAiJson(extracted);
            if (result is not null) return result;
        }

        _log.LogWarning("[OpenRouter] Could not parse AI JSON. Content length={Len}. Preview: {Preview}",
            content.Length, content.Length > 500 ? content[..500] + "..." : content);
        throw new AiParsingException(
            $"AI response could not be parsed as valid JSON (length={content.Length}).");
    }

    private AiGradingResponse? TryDeserializeAiJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var overallComment = root.TryGetProperty("overallComment", out var oc)
                ? oc.GetString() : null;

            var items = new List<AiGradedItem>();
            if (root.TryGetProperty("items", out var itemsEl))
            {
                foreach (var el in itemsEl.EnumerateArray())
                {
                    if (!el.TryGetProperty("questionNo",      out var qn))  continue;
                    if (!el.TryGetProperty("awardedRawScore", out var ars)) continue;

                    var comment  = el.TryGetProperty("comment",  out var c) ? c.GetString() : null;
                    var evidence = el.TryGetProperty("evidence", out var e) ? e.GetString() : null;

                    items.Add(new AiGradedItem(
                        QuestionNo:      qn.GetInt32(),
                        AwardedRawScore: ars.GetDouble(),
                        Comment:         comment,
                        Evidence:        evidence));
                }
            }

            return new AiGradingResponse(_options.Model, overallComment, items);
        }
        catch
        {
            return null;
        }
    }

    // ── Parsing helpers ──────────────────────────────────────────────────────

    private static string StripMarkdownFences(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            t = t["```json".Length..].TrimStart('\n', '\r');
        else if (t.StartsWith("```"))
            t = t[3..].TrimStart('\n', '\r');
        if (t.EndsWith("```"))
            t = t[..^3].TrimEnd();
        return t.Trim();
    }

    private static string? ExtractFirstJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '{': depth++; break;
                case '}':
                    if (--depth == 0)
                        return text[start..(i + 1)];
                    break;
            }
        }
        return null;
    }
}

// ── Internal exception types ──────────────────────────────────────────────────

internal sealed class AiParsingException : Exception
{
    public AiParsingException(string message, Exception? inner = null)
        : base(message, inner) { }
}

internal sealed class AiValidationException : Exception
{
    public AiValidationException(string message) : base(message) { }
}

internal sealed class AiTransientException : Exception
{
    public AiTransientException(string message, Exception? inner = null)
        : base(message, inner) { }
}
