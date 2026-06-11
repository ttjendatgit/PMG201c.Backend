using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Data;
using PMG201c.Backend.DTOs.Submissions;
using PMG201c.Backend.Models;

namespace PMG201c.Backend.Services;

public class SubmissionService
{
    private readonly AppDbContext _db;
    private readonly FileExtractionService _extractor;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<SubmissionService> _log;

    // Matches common student ID formats: SE170001, HE123456, DE1234567, etc.
    private static readonly Regex StudentIdRegex = new(
        @"\b([A-Z]{2,4}\d{5,8})\b",
        RegexOptions.Compiled);

    public SubmissionService(
        AppDbContext db,
        FileExtractionService extractor,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<SubmissionService> log)
    {
        _db = db;
        _extractor = extractor;
        _env = env;
        _config = config;
        _log = log;
    }

    public async Task<(List<SubmissionResponse> successes, List<FileUploadError> errors)> UploadAsync(
        Guid teacherId,
        Guid assessmentId,
        IList<IFormFile> files)
    {
        _ = await _db.Assessments
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.TeacherId == teacherId)
            ?? throw new KeyNotFoundException("Assessment not found.");

        var maxMb = _config.GetValue<int>("Upload:MaxFileSizeMb", 20);
        var dir = Path.Combine(
            _env.ContentRootPath, "uploads", "submissions", assessmentId.ToString());
        Directory.CreateDirectory(dir);

        var successes = new List<SubmissionResponse>();
        var errors = new List<FileUploadError>();

        foreach (var file in files)
        {
            try
            {
                if (file.Length == 0)
                    throw new ArgumentException("File is empty.");

                if (file.Length > maxMb * 1024L * 1024L)
                    throw new ArgumentException(
                        $"File exceeds the maximum allowed size of {maxMb} MB.");

                if (!_extractor.IsSupported(file.FileName))
                {
                    var badExt = Path.GetExtension(file.FileName);
                    throw new NotSupportedException(
                        $"File type '{badExt}' is not supported. Accepted: .txt, .md, .docx");
                }

                // Save to disk
                var stored = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var absPath = Path.Combine(dir, stored);
                await using (var stream = File.Create(absPath))
                    await file.CopyToAsync(stream);

                // Extract text — save with EXTRACTION_ERROR status but do not abort the batch
                string? extractedText = null;
                var extractionStatus = "EXTRACTED";
                string? extractionError = null;
                try
                {
                    extractedText = await _extractor.ExtractAsync(absPath, file.FileName);
                }
                catch (Exception ex)
                {
                    extractionStatus = "EXTRACTION_ERROR";
                    extractionError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                    _log.LogWarning("[Submission] Extraction failed for {File}: {Err}",
                        file.FileName, ex.Message);
                }

                var (studentId, studentName) = ParseStudentInfo(file.FileName);
                var relPath = Path.Combine(
                    "uploads", "submissions", assessmentId.ToString(), stored);

                var submission = new Submission
                {
                    AssessmentId     = assessmentId,
                    StudentId        = studentId,
                    StudentName      = studentName,
                    OriginalFileName = file.FileName,
                    FileType         = Path.GetExtension(file.FileName)
                                           .TrimStart('.').ToUpperInvariant(),
                    FileSize         = (int)file.Length,
                    StoragePath      = relPath,
                    ExtractedText    = extractedText,
                    ExtractionStatus = extractionStatus,
                    ExtractionError  = extractionError,
                    GradingStatus    = "UPLOADED"
                };

                _db.Submissions.Add(submission);
                await _db.SaveChangesAsync();

                _log.LogDebug("[Submission] Saved {File} → id={Id}", file.FileName, submission.Id);
                successes.Add(MapDetail(submission));
            }
            catch (Exception ex)
            {
                _log.LogWarning("[Submission] Rejected {File}: {Err}", file.FileName, ex.Message);
                errors.Add(new FileUploadError
                {
                    FileName = file.FileName,
                    Error    = ex.Message
                });
            }
        }

        return (successes, errors);
    }

    public async Task<List<SubmissionResponse>?> GetListAsync(Guid teacherId, Guid assessmentId)
    {
        var owns = await _db.Assessments
            .AnyAsync(a => a.Id == assessmentId && a.TeacherId == teacherId);

        if (!owns) return null;

        var items = await _db.Submissions
            .Where(s => s.AssessmentId == assessmentId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return items.Select(MapList).ToList();
    }

    public async Task<SubmissionResponse?> GetDetailAsync(Guid teacherId, Guid submissionId)
    {
        var submission = await _db.Submissions
            .Include(s => s.Assessment)
            .FirstOrDefaultAsync(s => s.Id == submissionId
                                   && s.Assessment.TeacherId == teacherId);

        return submission is null ? null : MapDetail(submission);
    }

    public async Task<bool?> DeleteAsync(Guid teacherId, Guid submissionId)
    {
        var submission = await _db.Submissions
            .Include(s => s.Assessment)
            .FirstOrDefaultAsync(s => s.Id == submissionId
                                   && s.Assessment.TeacherId == teacherId);

        if (submission is null) return null;

        try
        {
            var absPath = Path.Combine(_env.ContentRootPath, submission.StoragePath);
            if (File.Exists(absPath))
                File.Delete(absPath);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Submission] Could not delete file {Path}: {Err}",
                submission.StoragePath, ex.Message);
        }

        _db.Submissions.Remove(submission);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (string? studentId, string? studentName) ParseStudentInfo(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var m = StudentIdRegex.Match(nameWithoutExt);

        if (!m.Success)
            return (null, nameWithoutExt);

        var studentId = m.Value;
        var remaining = StudentIdRegex.Replace(nameWithoutExt, "")
                                      .Trim(' ', '_', '-', '–', '—');

        var studentName = string.IsNullOrWhiteSpace(remaining) ? nameWithoutExt : remaining;
        return (studentId, studentName);
    }

    // List: 200-char preview of extractedText to keep payload light
    private static SubmissionResponse MapList(Submission s) => new()
    {
        Id               = s.Id,
        AssessmentId     = s.AssessmentId,
        StudentId        = s.StudentId,
        StudentName      = s.StudentName,
        OriginalFileName = s.OriginalFileName,
        FileType         = s.FileType,
        FileSize         = s.FileSize,
        ExtractedText    = s.ExtractedText is null ? null
                           : s.ExtractedText.Length > 200
                               ? s.ExtractedText[..200] + "…"
                               : s.ExtractedText,
        ExtractionStatus = s.ExtractionStatus,
        ExtractionError  = s.ExtractionError,
        GradingStatus    = s.GradingStatus,
        CreatedAt        = s.CreatedAt,
        UpdatedAt        = s.UpdatedAt
    };

    // Detail: full extractedText
    private static SubmissionResponse MapDetail(Submission s) => new()
    {
        Id               = s.Id,
        AssessmentId     = s.AssessmentId,
        StudentId        = s.StudentId,
        StudentName      = s.StudentName,
        OriginalFileName = s.OriginalFileName,
        FileType         = s.FileType,
        FileSize         = s.FileSize,
        ExtractedText    = s.ExtractedText,
        ExtractionStatus = s.ExtractionStatus,
        ExtractionError  = s.ExtractionError,
        GradingStatus    = s.GradingStatus,
        CreatedAt        = s.CreatedAt,
        UpdatedAt        = s.UpdatedAt
    };
}
