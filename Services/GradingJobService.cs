using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Data;
using PMG201c.Backend.DTOs.Grading;
using PMG201c.Backend.Models;

namespace PMG201c.Backend.Services;

public class GradingJobService
{
    private readonly AppDbContext _db;
    private readonly IAiGradingService _ai;
    private readonly ILogger<GradingJobService> _log;

    public GradingJobService(AppDbContext db, IAiGradingService ai, ILogger<GradingJobService> log)
    {
        _db  = db;
        _ai  = ai;
        _log = log;
    }

    // ── POST /api/assessments/{assessmentId}/grading-jobs ─────────────────────

    public async Task<CreateGradingJobResponse> CreateJobAsync(Guid teacherId, Guid assessmentId)
    {
        var assessment = await _db.Assessments
            .Include(a => a.RubricItems)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.TeacherId == teacherId)
            ?? throw new KeyNotFoundException("Assessment not found.");

        if (string.IsNullOrWhiteSpace(assessment.QuestionText))
            throw new InvalidOperationException(
                "Assessment has no question text. Upload a question file first.");

        if (!assessment.RubricItems.Any())
            throw new InvalidOperationException(
                "Assessment has no rubric items. Run parse-rubric first.");

        // Only grade submissions that are not currently being graded
        var submissions = await _db.Submissions
            .Where(s => s.AssessmentId == assessmentId && s.GradingStatus != "GRADING")
            .ToListAsync();

        if (!submissions.Any())
            throw new InvalidOperationException(
                "No submissions available to grade (all may be in GRADING state).");

        // Create job
        var job = new GradingJob
        {
            AssessmentId      = assessmentId,
            Status            = "PENDING",
            TotalSubmissions  = submissions.Count
        };
        _db.GradingJobs.Add(job);
        await _db.SaveChangesAsync();

        _log.LogInformation("[GradingJob] {Id} created for assessment {AId}, {N} submissions",
            job.Id, assessmentId, submissions.Count);

        job.Status    = "RUNNING";
        job.StartedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var rubricItems = assessment.RubricItems.OrderBy(r => r.OrderIndex).ToList();

        foreach (var submission in submissions)
        {
            try
            {
                submission.GradingStatus = "GRADING";
                submission.UpdatedAt     = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await GradeSubmissionCoreAsync(submission, assessment, rubricItems, job.Id);

                submission.GradingStatus = "GRADED";
                submission.UpdatedAt     = DateTime.UtcNow;
                job.ProcessedSubmissions++;
                job.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _log.LogDebug("[GradingJob] Graded submission {SId}", submission.Id);
            }
            catch (Exception ex)
            {
                _log.LogWarning("[GradingJob] Failed submission {SId}: {Err}", submission.Id, ex.Message);

                submission.GradingStatus = "ERROR";
                submission.UpdatedAt     = DateTime.UtcNow;
                job.ProcessedSubmissions++;
                job.FailedSubmissions++;
                job.UpdatedAt = DateTime.UtcNow;

                await SaveErrorResultAsync(submission.Id, assessmentId, job.Id, ex.Message);
                await _db.SaveChangesAsync();
            }
        }

        job.Status      = job.FailedSubmissions == 0               ? "COMPLETED"
                        : job.FailedSubmissions == job.TotalSubmissions ? "ERROR"
                        : "COMPLETED_WITH_ERRORS";
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt   = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _log.LogInformation("[GradingJob] {Id} finished → {Status}", job.Id, job.Status);

        var ok = job.ProcessedSubmissions - job.FailedSubmissions;
        return new CreateGradingJobResponse
        {
            Message = $"Grading complete: {ok} graded, {job.FailedSubmissions} failed " +
                      $"out of {job.TotalSubmissions} submission(s).",
            Job     = MapJob(job)
        };
    }

    // ── GET /api/assessments/{assessmentId}/grading-jobs/{jobId} ─────────────

    public async Task<GradingJobResponse?> GetJobAsync(Guid teacherId, Guid assessmentId, Guid jobId)
    {
        var job = await _db.GradingJobs
            .Include(j => j.Assessment)
            .FirstOrDefaultAsync(j => j.Id           == jobId
                                   && j.AssessmentId == assessmentId
                                   && j.Assessment.TeacherId == teacherId);

        return job is null ? null : MapJob(job);
    }

    // ── GET /api/assessments/{assessmentId}/grading-status ───────────────────

    public async Task<GradingStatusResponse?> GetStatusAsync(Guid teacherId, Guid assessmentId)
    {
        var owns = await _db.Assessments
            .AnyAsync(a => a.Id == assessmentId && a.TeacherId == teacherId);

        if (!owns) return null;

        var statuses = await _db.Submissions
            .Where(s => s.AssessmentId == assessmentId)
            .Select(s => s.GradingStatus)
            .ToListAsync();

        var latestJob = await _db.GradingJobs
            .Where(j => j.AssessmentId == assessmentId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new { j.Id, j.Status })
            .FirstOrDefaultAsync();

        return new GradingStatusResponse
        {
            AssessmentId     = assessmentId,
            TotalSubmissions = statuses.Count,
            Uploaded         = statuses.Count(s => s == "UPLOADED"),
            Grading          = statuses.Count(s => s == "GRADING"),
            Graded           = statuses.Count(s => s == "GRADED"),
            Error            = statuses.Count(s => s == "ERROR"),
            LatestJobStatus  = latestJob?.Status,
            LatestJobId      = latestJob?.Id
        };
    }

    // ── POST /api/submissions/{submissionId}/grade ────────────────────────────

    public async Task<GradingResultResponse?> GradeSingleAsync(Guid teacherId, Guid submissionId)
    {
        var submission = await _db.Submissions
            .Include(s => s.Assessment)
                .ThenInclude(a => a.RubricItems)
            .FirstOrDefaultAsync(s => s.Id == submissionId
                                   && s.Assessment.TeacherId == teacherId);

        if (submission is null) return null;

        var assessment = submission.Assessment;

        if (string.IsNullOrWhiteSpace(assessment.QuestionText))
            throw new InvalidOperationException(
                "Assessment has no question text. Upload a question file first.");

        if (!assessment.RubricItems.Any())
            throw new InvalidOperationException(
                "Assessment has no rubric items. Run parse-rubric first.");

        // Reject a second concurrent grade request for the same submission
        // instead of racing two AI calls to RemoveRange/Add the same
        // GradingResult row (checked before we touch GradingStatus ourselves).
        if (submission.GradingStatus == "GRADING")
            throw new GradingInProgressException(
                "This submission is already being graded. Please wait for it to finish.");

        submission.GradingStatus = "GRADING";
        submission.UpdatedAt     = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var rubricItems = assessment.RubricItems.OrderBy(r => r.OrderIndex).ToList();

        try
        {
            var result = await GradeSubmissionCoreAsync(submission, assessment, rubricItems, jobId: null);

            submission.GradingStatus = "GRADED";
            submission.UpdatedAt     = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return MapResult(result);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Grading] Single grade failed for {Id}: {Err}", submissionId, ex.Message);
            submission.GradingStatus = "ERROR";
            submission.UpdatedAt     = DateTime.UtcNow;
            await SaveErrorResultAsync(submission.Id, assessment.Id, jobId: null, ex.Message);
            await _db.SaveChangesAsync();
            throw;
        }
    }

    // ── GET /api/submissions/{submissionId}/grading-result ───────────────────

    public async Task<GradingResultResponse?> GetResultAsync(Guid teacherId, Guid submissionId)
    {
        var owns = await _db.Submissions
            .Include(s => s.Assessment)
            .AnyAsync(s => s.Id == submissionId && s.Assessment.TeacherId == teacherId);

        if (!owns) return null;

        var result = await _db.GradingResults
            .Include(r => r.Items)
            .Where(r => r.SubmissionId == submissionId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        return result is null ? null : MapResult(result);
    }

    // ── Core grading ─────────────────────────────────────────────────────────

    private async Task<GradingResult> GradeSubmissionCoreAsync(
        Submission submission,
        Assessment assessment,
        IList<RubricItem> rubricItems,
        Guid? jobId)
    {
        if (string.IsNullOrWhiteSpace(submission.ExtractedText))
            throw new InvalidOperationException(
                "Submission has no extracted text (extraction may have failed).");

        // Remove any previous result for this submission
        var previous = await _db.GradingResults
            .Where(r => r.SubmissionId == submission.Id)
            .ToListAsync();
        if (previous.Any())
            _db.GradingResults.RemoveRange(previous);

        var aiRequest = new AiGradingRequest(
            QuestionText : assessment.QuestionText!,
            GuideText    : assessment.GuideText,
            StudentText  : submission.ExtractedText,
            RubricItems  : rubricItems.Select(r => new RubricItemInput(
                r.QuestionNo, r.Title, r.Description, r.MaxRawScore, r.MaxConvertedScore
            )).ToList()
        );

        var aiResponse = await _ai.GradeAsync(aiRequest);

        var resultItems = rubricItems.Select(rubric =>
        {
            var aiItem   = aiResponse.Items.FirstOrDefault(i => i.QuestionNo == rubric.QuestionNo);
            var awarded  = aiItem is not null
                ? Math.Clamp(aiItem.AwardedRawScore, 0, rubric.MaxRawScore)
                : 0;
            var converted = rubric.MaxRawScore > 0
                ? Math.Round(awarded / rubric.MaxRawScore * rubric.MaxConvertedScore, 2)
                : 0;

            return new GradingResultItem
            {
                RubricItemId          = rubric.Id,
                QuestionNo            = rubric.QuestionNo,
                Title                 = rubric.Title,
                MaxRawScore           = rubric.MaxRawScore,
                MaxConvertedScore     = rubric.MaxConvertedScore,
                AwardedRawScore       = Math.Round(awarded, 2),
                AwardedConvertedScore = converted,
                AiComment             = aiItem?.Comment,
                Evidence              = aiItem?.Evidence
            };
        }).ToList();

        var totalRaw       = Math.Round(resultItems.Sum(i => i.AwardedRawScore), 2);
        var totalConverted = Math.Round(resultItems.Sum(i => i.AwardedConvertedScore), 2);

        var result = new GradingResult
        {
            SubmissionId        = submission.Id,
            AssessmentId        = assessment.Id,
            GradingJobId        = jobId,
            TotalRawScore       = totalRaw,
            TotalConvertedScore = totalConverted,
            AiOverallComment    = aiResponse.OverallComment,
            AiModel             = aiResponse.AiModel,
            Status              = "GRADED",
            Items               = resultItems
        };

        _db.GradingResults.Add(result);
        await _db.SaveChangesAsync();
        return result;
    }

    private async Task SaveErrorResultAsync(
        Guid submissionId, Guid assessmentId, Guid? jobId, string error)
    {
        var previous = await _db.GradingResults
            .Where(r => r.SubmissionId == submissionId)
            .ToListAsync();
        if (previous.Any())
            _db.GradingResults.RemoveRange(previous);

        _db.GradingResults.Add(new GradingResult
        {
            SubmissionId = submissionId,
            AssessmentId = assessmentId,
            GradingJobId = jobId,
            Status       = "ERROR",
            ErrorMessage = error.Length > 2000 ? error[..2000] : error
        });
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static GradingJobResponse MapJob(GradingJob j) => new()
    {
        Id                   = j.Id,
        AssessmentId         = j.AssessmentId,
        Status               = j.Status,
        TotalSubmissions     = j.TotalSubmissions,
        ProcessedSubmissions = j.ProcessedSubmissions,
        FailedSubmissions    = j.FailedSubmissions,
        ErrorMessage         = j.ErrorMessage,
        CreatedAt            = j.CreatedAt,
        StartedAt            = j.StartedAt,
        CompletedAt          = j.CompletedAt,
        UpdatedAt            = j.UpdatedAt
    };

    internal static GradingResultResponse MapResult(GradingResult r) => new()
    {
        Id                    = r.Id,
        SubmissionId          = r.SubmissionId,
        AssessmentId          = r.AssessmentId,
        GradingJobId          = r.GradingJobId,
        TotalRawScore         = r.TotalRawScore,
        TotalConvertedScore   = r.TotalConvertedScore,
        AiOverallComment      = r.AiOverallComment,
        AiModel               = r.AiModel,
        ReviewedRawScore      = r.ReviewedRawScore,
        ReviewedConvertedScore = r.ReviewedConvertedScore,
        FinalRawScore         = r.FinalRawScore,
        FinalConvertedScore   = r.FinalConvertedScore,
        TeacherOverallComment = r.TeacherOverallComment,
        ReviewStatus          = r.ReviewStatus,
        ReviewedAt            = r.ReviewedAt,
        FinalizedAt           = r.FinalizedAt,
        Status                = r.Status,
        ErrorMessage          = r.ErrorMessage,
        Items                 = r.Items
            .OrderBy(i => i.QuestionNo)
            .Select(i => new GradingResultItemResponse
            {
                Id                     = i.Id,
                GradingResultId        = i.GradingResultId,
                RubricItemId           = i.RubricItemId,
                QuestionNo             = i.QuestionNo,
                Title                  = i.Title,
                MaxRawScore            = i.MaxRawScore,
                MaxConvertedScore      = i.MaxConvertedScore,
                AwardedRawScore        = i.AwardedRawScore,
                AwardedConvertedScore  = i.AwardedConvertedScore,
                AiComment              = i.AiComment,
                Evidence               = i.Evidence,
                ReviewedRawScore       = i.ReviewedRawScore,
                ReviewedConvertedScore = i.ReviewedConvertedScore,
                TeacherComment         = i.TeacherComment,
                IsScoreOverridden      = i.IsScoreOverridden,
                CreatedAt              = i.CreatedAt
            }).ToList(),
        CreatedAt             = r.CreatedAt,
        UpdatedAt             = r.UpdatedAt
    };
}

/// <summary>
/// Thrown when a single-submission grade request arrives while that
/// submission is already being graded (GradingStatus == "GRADING").
/// Mapped to HTTP 409 Conflict by GradingController.
/// </summary>
public sealed class GradingInProgressException : Exception
{
    public GradingInProgressException(string message) : base(message) { }
}
