using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PMG201c.Backend.Services;

public class ParsedRubricItem
{
    public int QuestionNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double MaxRawScore { get; set; }
    public int OrderIndex { get; set; }
}

public class ParseResult
{
    public List<ParsedRubricItem> Items { get; set; } = new();
    public double? DetectedTotalRawScore { get; set; }
}

public class RubricParserService
{
    private readonly ILogger<RubricParserService> _log;

    public RubricParserService(ILogger<RubricParserService> log)
    {
        _log = log;
    }

    // ── Heading WITHOUT inline score ──────────────────────────────────────────
    // Matches: "Yêu cầu 1: Title", "Yêu cầu 1 – Title", "Q1: Title", "Question 1 Title"
    // Does NOT match sub-items (1.1, 1.2) due to (?!\.\d)
    private static readonly Regex HeadingRegex = new(
        @"^(?:Question|Q|Yêu\s*cầu|Yeu\s*cau)\s*(\d+)(?!\.\d)\s*[\-:–—]?\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // ── Heading WITH score on the SAME line ───────────────────────────────────
    // Matches: "Q1 Title - 20 marks", "Yêu cầu 1: Title – 20 điểm", "Yêu cầu 1: Title | 20 điểm"
    private static readonly Regex SameLineRegex = new(
        @"^(?:Question|Q|Yêu\s*cầu|Yeu\s*cau)\s*(\d+)(?!\.\d)\s*[\-:–—]?\s*(.+?)\s*[\-–—|]\s*(\d+(?:\.\d+)?)\s*(?:marks|points|pts|điểm|đ\b|diem)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // ── Standalone score line ─────────────────────────────────────────────────
    // Matches: "20 điểm", "(20 điểm)", "Điểm tối đa: 20 điểm", "Max score: 20 points"
    private static readonly Regex ScoreLineRegex = new(
        @"^(?:(?:Điểm\s*tối\s*đa|Max\s*score)\s*:?\s*)?\(?\s*(\d+(?:\.\d+)?)\s*\)?\s*(?:điểm|đ\b|diem|marks|points|pts)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // ── Total heading (no inline score) ───────────────────────────────────────
    // Matches: "TỔNG ĐIỂM", "Tổng điểm", "Tổng điểm tối đa toàn bài",
    //          "// Tổng điểm tối đa toàn bài", "Total score"
    private static readonly Regex TotalHeadingRegex = new(
        @"^(?:\/\/\s*)?(?:TỔNG\s*ĐIỂM|Tổng\s*điểm(?:\s*tối\s*đa\s*toàn\s*bài)?|Tong\s*diem|Total(?:\s+score)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // ── Total line WITH inline score ──────────────────────────────────────────
    // Matches: "Tổng điểm tối đa toàn bài: 100 điểm", "Total score: 100 points", "TỔNG ĐIỂM | 100 điểm"
    private static readonly Regex TotalInlineRegex = new(
        @"^(?:\/\/\s*)?(?:TỔNG\s*ĐIỂM|Tổng\s*điểm(?:\s*tối\s*đa\s*toàn\s*bài)?|Tong\s*diem|Total(?:\s+score)?)\s*[\-:|]?\s*(\d+(?:\.\d+)?)\s*(?:marks|points|pts|điểm|đ\b|diem)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // ── Sub-item prefix ───────────────────────────────────────────────────────
    // Detects lines starting with "1.1", "2.3", etc.
    private static readonly Regex SubItemRegex = new(
        @"^\d+\.\d+",
        RegexOptions.Compiled
    );

    // ── Sub-item with score at end ────────────────────────────────────────────
    // Matches: "1.1 Some criterion – 5 điểm", "1.1 Some criterion | 5 điểm"
    private static readonly Regex SubItemScoreRegex = new(
        @"^\d+\.\d+.*?[\-–—:|]\s*(\d+(?:\.\d+)?)\s*(?:marks|points|pts|điểm|đ\b|diem)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // ─────────────────────────────────────────────────────────────────────────

    public ParseResult Parse(string guideText)
    {
        _log.LogDebug("[RubricParser] guideText length = {Length}", guideText.Length);

        var lines = guideText
            .Split('\n')
            .Select(l => l.Trim())
            .ToArray();

        // Dump first 60 lines so we can see the real format
        for (int d = 0; d < Math.Min(60, lines.Length); d++)
            _log.LogDebug("[RubricParser] line[{I}] = {Line}", d, lines[d]);

        // Strategy 1: overview block (headings followed by score on next 1–3 lines)
        var result = TryParseOverviewBlock(lines);
        if (result.Items.Count > 0)
        {
            _log.LogDebug("[RubricParser] strategy = overview-block, items = {Count}", result.Items.Count);
            LogItems(result);
            return result;
        }

        // Strategy 2: section-sum (top-level sections; sum sub-criteria scores)
        result = TryParseSectionSum(lines);
        if (result.Items.Count > 0)
        {
            _log.LogDebug("[RubricParser] strategy = section-sum, items = {Count}", result.Items.Count);
            LogItems(result);
            return result;
        }

        // Strategy 3: same-line fallback (original behaviour)
        result = TryParseSameLine(lines);
        _log.LogDebug("[RubricParser] strategy = same-line, items = {Count}", result.Items.Count);
        LogItems(result);
        return result;
    }

    // ── Strategy 1: Overview block ────────────────────────────────────────────

    private ParseResult TryParseOverviewBlock(string[] lines)
    {
        var result = new ParseResult();
        var seenQNos = new HashSet<int>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Inline total — save score and stop
            var inlineTotal = TotalInlineRegex.Match(line);
            if (inlineTotal.Success)
            {
                if (result.DetectedTotalRawScore is null)
                {
                    result.DetectedTotalRawScore = ParseDouble(inlineTotal.Groups[1].Value);
                    _log.LogDebug("[RubricParser] overview: inline total = {Total}", result.DetectedTotalRawScore);
                }
                break;
            }

            // Total heading — look ahead for score, then stop
            if (TotalHeadingRegex.IsMatch(line))
            {
                _log.LogDebug("[RubricParser] overview: total heading at line {I}", i);
                if (result.DetectedTotalRawScore is null)
                    TryReadLookaheadScore(lines, i, result);
                break;
            }

            // Skip sub-items (1.1, 2.3, …)
            if (SubItemRegex.IsMatch(line)) continue;

            var headingMatch = HeadingRegex.Match(line);
            if (!headingMatch.Success) continue;

            var qNo = int.Parse(headingMatch.Groups[1].Value);
            if (seenQNos.Contains(qNo)) continue;     // ignore duplicates

            _log.LogDebug("[RubricParser] overview: heading detected Q{No}", qNo);

            // Check whether the score is already on the same line
            var sameLineMatch = SameLineRegex.Match(line);
            if (sameLineMatch.Success)
            {
                // Use SameLineRegex group 2 (non-greedy) so the title never includes "| score"
                var cleanTitle = sameLineMatch.Groups[2].Value.Trim();
                var rawScore = ParseDouble(sameLineMatch.Groups[3].Value);
                _log.LogDebug("[RubricParser] overview: Q{No} title='{Title}' rawScore={Score} (same-line)", qNo, cleanTitle, rawScore);
                result.Items.Add(MakeItem(qNo, cleanTitle, rawScore, result.Items.Count));
                seenQNos.Add(qNo);
                continue;
            }

            var title = headingMatch.Groups[2].Value.Trim();

            // Look ahead up to 3 non-empty lines for the score
            double? foundScore = null;
            for (int j = i + 1; j < Math.Min(i + 6, lines.Length); j++)
            {
                if (string.IsNullOrWhiteSpace(lines[j])) continue;

                // Stop if we hit another heading or a total marker
                if (HeadingRegex.IsMatch(lines[j])
                    || TotalHeadingRegex.IsMatch(lines[j])
                    || TotalInlineRegex.IsMatch(lines[j]))
                    break;

                var scoreMatch = ScoreLineRegex.Match(lines[j]);
                if (scoreMatch.Success)
                {
                    foundScore = ParseDouble(scoreMatch.Groups[1].Value);
                    break;
                }
            }

            if (foundScore.HasValue)
            {
                _log.LogDebug("[RubricParser] overview: Q{No} rawScore={Score} (lookahead)", qNo, foundScore.Value);
                result.Items.Add(MakeItem(qNo, title, foundScore.Value, result.Items.Count));
                seenQNos.Add(qNo);
            }
        }

        return result;
    }

    // ── Strategy 2: Section-sum ───────────────────────────────────────────────

    private ParseResult TryParseSectionSum(string[] lines)
    {
        var result = new ParseResult();
        int? currentQNo = null;
        string? currentTitle = null;
        double currentSum = 0;
        bool currentHasSameLine = false;

        void FlushSection()
        {
            if (currentQNo is null) return;
            if (currentSum > 0)
            {
                _log.LogDebug("[RubricParser] section-sum: Q{No} title='{Title}' sumScore={Sum}",
                    currentQNo, currentTitle, currentSum);
                result.Items.Add(MakeItem(currentQNo.Value, currentTitle ?? $"Yêu cầu {currentQNo}", currentSum, result.Items.Count));
            }
            currentQNo = null;
            currentTitle = null;
            currentSum = 0;
            currentHasSameLine = false;
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Inline total
            var inlineTotal = TotalInlineRegex.Match(line);
            if (inlineTotal.Success)
            {
                if (result.DetectedTotalRawScore is null)
                    result.DetectedTotalRawScore = ParseDouble(inlineTotal.Groups[1].Value);
                FlushSection();
                break;
            }

            if (TotalHeadingRegex.IsMatch(line))
            {
                FlushSection();
                break;
            }

            // Top-level heading (skip sub-items)
            if (!SubItemRegex.IsMatch(line))
            {
                var headingMatch = HeadingRegex.Match(line);
                if (headingMatch.Success)
                {
                    FlushSection();
                    currentQNo = int.Parse(headingMatch.Groups[1].Value);
                    currentTitle = headingMatch.Groups[2].Value.Trim();

                    // Same-line score? Use SameLineRegex group 2 for clean title (no "| score" suffix)
                    var sameLineMatch = SameLineRegex.Match(line);
                    if (sameLineMatch.Success)
                    {
                        currentTitle = sameLineMatch.Groups[2].Value.Trim();
                        currentSum = ParseDouble(sameLineMatch.Groups[3].Value);
                        currentHasSameLine = true;
                    }
                    continue;
                }
            }

            // Sub-item with score — accumulate only when header came without same-line score
            if (currentQNo.HasValue && !currentHasSameLine && SubItemRegex.IsMatch(line))
            {
                var subScore = SubItemScoreRegex.Match(line);
                if (subScore.Success)
                    currentSum += ParseDouble(subScore.Groups[1].Value);
            }
        }

        FlushSection();
        return result;
    }

    // ── Strategy 3: Same-line (original behaviour) ────────────────────────────

    private ParseResult TryParseSameLine(string[] lines)
    {
        var result = new ParseResult();
        ParsedRubricItem? current = null;
        var descLines = new List<string>();

        void Flush()
        {
            if (current is null) return;
            if (descLines.Count > 0)
            {
                var desc = string.Join("\n", descLines).Trim();
                current.Description = desc.Length > 1990 ? desc[..1990] : desc;
            }
            result.Items.Add(current);
            current = null;
            descLines.Clear();
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var inlineTotal = TotalInlineRegex.Match(line);
            if (inlineTotal.Success && result.DetectedTotalRawScore is null)
            {
                result.DetectedTotalRawScore = ParseDouble(inlineTotal.Groups[1].Value);
                continue;
            }

            if (TotalHeadingRegex.IsMatch(line)) continue;

            var sameLineMatch = SameLineRegex.Match(line);
            if (sameLineMatch.Success)
            {
                Flush();
                var qNo = int.Parse(sameLineMatch.Groups[1].Value);
                var title = sameLineMatch.Groups[2].Value.Trim();
                var rawScore = ParseDouble(sameLineMatch.Groups[3].Value);
                _log.LogDebug("[RubricParser] same-line: Q{No} title='{Title}' rawScore={Score}", qNo, title, rawScore);
                current = MakeItem(qNo, title, rawScore, result.Items.Count);
            }
            else if (current is not null && !SubItemRegex.IsMatch(line))
            {
                descLines.Add(line);
            }
        }

        Flush();
        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TryReadLookaheadScore(string[] lines, int headingIndex, ParseResult result)
    {
        for (int j = headingIndex + 1; j < Math.Min(headingIndex + 4, lines.Length); j++)
        {
            if (string.IsNullOrWhiteSpace(lines[j])) continue;
            var m = ScoreLineRegex.Match(lines[j]);
            if (m.Success)
            {
                result.DetectedTotalRawScore = ParseDouble(m.Groups[1].Value);
                _log.LogDebug("[RubricParser] total raw score = {Total}", result.DetectedTotalRawScore);
            }
            return; // check at most first non-empty line
        }
    }

    private static ParsedRubricItem MakeItem(int qNo, string title, double rawScore, int existingCount) =>
        new()
        {
            QuestionNo = qNo,
            Title = title.Length > 0 ? title : $"Yêu cầu {qNo}",
            MaxRawScore = rawScore,
            OrderIndex = existingCount + 1
        };

    private static double ParseDouble(string s) =>
        double.Parse(s, CultureInfo.InvariantCulture);

    private void LogItems(ParseResult result)
    {
        foreach (var item in result.Items)
            _log.LogDebug("[RubricParser] item: Q{No} '{Title}' rawScore={Score}",
                item.QuestionNo, item.Title, item.MaxRawScore);

        _log.LogDebug("[RubricParser] detectedTotalRawScore = {Total}",
            result.DetectedTotalRawScore?.ToString(CultureInfo.InvariantCulture) ?? "(will sum items)");
    }
}
