using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Data;
using PMG201c.Backend.DTOs.Grading;
using PMG201c.Backend.DTOs.Review;
using PMG201c.Backend.Models;

namespace PMG201c.Backend.Services;

public class ReviewService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReviewService> _log;

    public ReviewService(AppDbContext db, ILogger<ReviewService> log)
    {
        _db  = db;
        _log = log;
    }

    // ── PUT /api/grading-results/{gradingResultId}/review ────────────────────

    public async Task<GradingResultResponse> ReviewAsync(
        Guid teacherId, Guid gradingResultId, ReviewGradingResultRequest request)
    {
        var result = await LoadResultForTeacher(teacherId, gradingResultId);

        foreach (var itemReq in request.Items)
        {
            var item = result.Items.FirstOrDefault(i => i.Id == itemReq.GradingResultItemId);
            if (item is null) continue; // skip items that don't belong to this result

            if (itemReq.ReviewedRawScore > item.MaxRawScore)
                throw new ArgumentOutOfRangeException(nameof(itemReq.ReviewedRawScore),
                    $"reviewedRawScore ({itemReq.ReviewedRawScore}) exceeds maxRawScore " +
                    $"({item.MaxRawScore}) for item '{item.Title}'.");

            var converted = item.MaxRawScore > 0
                ? Math.Round(itemReq.ReviewedRawScore / item.MaxRawScore * item.MaxConvertedScore, 2)
                : 0;

            item.ReviewedRawScore       = Math.Round(itemReq.ReviewedRawScore, 2);
            item.ReviewedConvertedScore = converted;
            item.TeacherComment         = itemReq.TeacherComment;
            item.IsScoreOverridden      = Math.Abs(itemReq.ReviewedRawScore - item.AwardedRawScore) > 0.001;
            item.UpdatedAt              = DateTime.UtcNow;
        }

        // Totals: use ReviewedRawScore for reviewed items, AwardedRawScore for unreviewed items
        result.ReviewedRawScore = Math.Round(
            result.Items.Sum(i => i.ReviewedRawScore ?? i.AwardedRawScore), 2);
        result.ReviewedConvertedScore = Math.Round(
            result.Items.Sum(i => i.ReviewedConvertedScore ?? i.AwardedConvertedScore), 2);

        result.TeacherOverallComment = request.TeacherOverallComment;
        result.ReviewStatus          = "REVIEWED";
        result.ReviewedAt            = DateTime.UtcNow;
        result.UpdatedAt             = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return GradingJobService.MapResult(result);
    }

    // ── POST /api/grading-results/{gradingResultId}/finalize ─────────────────

    public async Task<FinalizeGradingResultResponse> FinalizeAsync(Guid teacherId, Guid gradingResultId)
    {
        var result = await LoadResultForTeacher(teacherId, gradingResultId);

        // Use reviewed scores if available; fall back to AI scores
        result.FinalRawScore       = result.ReviewedRawScore       ?? result.TotalRawScore;
        result.FinalConvertedScore = result.ReviewedConvertedScore  ?? result.TotalConvertedScore;

        result.ReviewStatus = "FINALIZED";
        result.FinalizedAt  = DateTime.UtcNow;
        result.UpdatedAt    = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new FinalizeGradingResultResponse
        {
            Message = "Grading result finalized successfully.",
            Result  = GradingJobService.MapResult(result)
        };
    }

    // ── GET /api/assessments/{assessmentId}/review-results ───────────────────

    public async Task<List<ReviewResultSummaryResponse>?> GetReviewResultsAsync(
        Guid teacherId, Guid assessmentId)
    {
        var owns = await _db.Assessments
            .AnyAsync(a => a.Id == assessmentId && a.TeacherId == teacherId);

        if (!owns) return null;

        var results = await _db.GradingResults
            .Include(r => r.Submission)
            .Where(r => r.AssessmentId == assessmentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return results.Select(r => new ReviewResultSummaryResponse
        {
            GradingResultId        = r.Id,
            SubmissionId           = r.SubmissionId,
            StudentId              = r.Submission.StudentId,
            StudentName            = r.Submission.StudentName,
            OriginalFileName       = r.Submission.OriginalFileName,
            AiTotalRawScore        = r.TotalRawScore,
            AiTotalConvertedScore  = r.TotalConvertedScore,
            ReviewedRawScore       = r.ReviewedRawScore,
            ReviewedConvertedScore = r.ReviewedConvertedScore,
            FinalRawScore          = r.FinalRawScore,
            FinalConvertedScore    = r.FinalConvertedScore,
            ReviewStatus           = r.ReviewStatus,
            TeacherOverallComment  = r.TeacherOverallComment,
            UpdatedAt              = r.UpdatedAt
        }).ToList();
    }

    // ── POST /api/submissions/{submissionId}/manual-result ───────────────────

    public async Task<GradingResultResponse> ManualGradeAsync(
        Guid teacherId, Guid submissionId, ManualGradingRequest request)
    {
        var submission = await _db.Submissions
            .Include(s => s.Assessment)
                .ThenInclude(a => a.RubricItems)
            .FirstOrDefaultAsync(s => s.Id == submissionId
                                   && s.Assessment.TeacherId == teacherId)
            ?? throw new KeyNotFoundException("Submission not found.");

        var rubricMap = submission.Assessment.RubricItems.ToDictionary(r => r.Id);

        var existing = await _db.GradingResults
            .Include(r => r.Items)
            .Where(r => r.SubmissionId == submissionId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        GradingResult result;

        if (existing is not null)
        {
            _log.LogInformation("[ManualGrading] Updating GradingResult {ResultId} for submission {SubmissionId}",
                existing.Id, submissionId);

            result = existing;

            if (result.Items.Any())
            {
                foreach (var itemReq in request.Items)
                {
                    var item = result.Items.FirstOrDefault(i => i.RubricItemId == itemReq.RubricItemId);
                    if (item is null) continue;

                    var clamped   = Math.Clamp(itemReq.ReviewedRawScore, 0, item.MaxRawScore);
                    var converted = item.MaxRawScore > 0
                        ? Math.Round(clamped / item.MaxRawScore * item.MaxConvertedScore, 2)
                        : 0;

                    item.ReviewedRawScore       = Math.Round(clamped, 2);
                    item.ReviewedConvertedScore = converted;
                    item.TeacherComment         = itemReq.TeacherComment;
                    item.IsScoreOverridden      = true;
                    item.UpdatedAt              = DateTime.UtcNow;
                }
            }
            else
            {
                // ERROR result with no items — populate from manual request
                foreach (var item in BuildManualItems(request.Items, rubricMap))
                    result.Items.Add(item);

                var manualRaw  = Math.Round(result.Items.Sum(i => i.ReviewedRawScore ?? 0), 2);
                var manualConv = Math.Round(result.Items.Sum(i => i.ReviewedConvertedScore ?? 0), 2);
                result.TotalRawScore       = manualRaw;
                result.TotalConvertedScore = manualConv;
                result.Status              = "GRADED";
                result.ErrorMessage        = null;
            }

            result.ReviewedRawScore       = Math.Round(result.Items.Sum(i => i.ReviewedRawScore ?? i.AwardedRawScore), 2);
            result.ReviewedConvertedScore = Math.Round(result.Items.Sum(i => i.ReviewedConvertedScore ?? i.AwardedConvertedScore), 2);
            result.TeacherOverallComment  = request.TeacherOverallComment;
            result.ReviewStatus           = "REVIEWED";
            result.ReviewedAt             = DateTime.UtcNow;
            result.UpdatedAt              = DateTime.UtcNow;
        }
        else
        {
            _log.LogInformation("[ManualGrading] Creating GradingResult for submission {SubmissionId}", submissionId);

            var items     = BuildManualItems(request.Items, rubricMap);
            var totalRaw  = Math.Round(items.Sum(i => i.ReviewedRawScore ?? 0), 2);
            var totalConv = Math.Round(items.Sum(i => i.ReviewedConvertedScore ?? 0), 2);

            result = new GradingResult
            {
                SubmissionId           = submissionId,
                AssessmentId           = submission.Assessment.Id,
                TotalRawScore          = totalRaw,
                TotalConvertedScore    = totalConv,
                ReviewedRawScore       = totalRaw,
                ReviewedConvertedScore = totalConv,
                TeacherOverallComment  = request.TeacherOverallComment,
                AiModel                = "manual",
                AiOverallComment       = "Manual grading",
                Status                 = "GRADED",
                ReviewStatus           = "REVIEWED",
                ReviewedAt             = DateTime.UtcNow,
                Items                  = items
            };

            _db.GradingResults.Add(result);
        }

        submission.GradingStatus = "GRADED";
        submission.UpdatedAt     = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _log.LogInformation("[ManualGrading] Saved result {ResultId} for submission {SubmissionId}",
            result.Id, submissionId);

        return GradingJobService.MapResult(result);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static List<GradingResultItem> BuildManualItems(
        IEnumerable<ManualGradingItemRequest> itemRequests,
        Dictionary<Guid, RubricItem> rubricMap)
    {
        return itemRequests.Select(itemReq =>
        {
            rubricMap.TryGetValue(itemReq.RubricItemId, out var rubric);
            var maxRaw   = rubric?.MaxRawScore ?? 0;
            var maxConv  = rubric?.MaxConvertedScore ?? 0;
            var clamped  = Math.Clamp(itemReq.ReviewedRawScore, 0, maxRaw);
            var converted = maxRaw > 0
                ? Math.Round(clamped / maxRaw * maxConv, 2)
                : 0;

            return new GradingResultItem
            {
                RubricItemId           = itemReq.RubricItemId,
                QuestionNo             = rubric?.QuestionNo ?? itemReq.QuestionNo,
                Title                  = rubric?.Title ?? string.Empty,
                MaxRawScore            = maxRaw,
                MaxConvertedScore      = maxConv,
                AwardedRawScore        = 0,
                AwardedConvertedScore  = 0,
                ReviewedRawScore       = Math.Round(clamped, 2),
                ReviewedConvertedScore = converted,
                TeacherComment         = itemReq.TeacherComment,
                IsScoreOverridden      = true,
                AiComment              = "Manual grading",
                Evidence               = null
            };
        }).ToList();
    }

    private async Task<Models.GradingResult> LoadResultForTeacher(Guid teacherId, Guid gradingResultId)
    {
        var result = await _db.GradingResults
            .Include(r => r.Items)
            .Include(r => r.Assessment)
            .FirstOrDefaultAsync(r => r.Id == gradingResultId
                                   && r.Assessment.TeacherId == teacherId);

        return result ?? throw new KeyNotFoundException("Grading result not found.");
    }
}
