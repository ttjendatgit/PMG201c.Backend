using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Data;
using PMG201c.Backend.DTOs.Grading;
using PMG201c.Backend.DTOs.Review;

namespace PMG201c.Backend.Services;

public class ReviewService
{
    private readonly AppDbContext _db;

    public ReviewService(AppDbContext db) => _db = db;

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

    // ── Helper ────────────────────────────────────────────────────────────────

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
