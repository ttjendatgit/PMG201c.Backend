using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Data;
using PMG201c.Backend.DTOs.Assessments;
using PMG201c.Backend.Models;

namespace PMG201c.Backend.Services;

public class AssessmentFileService
{
    private readonly AppDbContext _db;
    private readonly FileExtractionService _extractor;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public AssessmentFileService(
        AppDbContext db,
        FileExtractionService extractor,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _db = db;
        _extractor = extractor;
        _env = env;
        _config = config;
    }

    public async Task<AssessmentFileResponse> UploadAsync(
        Guid teacherId,
        Guid assessmentId,
        IFormFile file,
        string fileType)
    {
        var assessment = await _db.Assessments
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.TeacherId == teacherId)
            ?? throw new KeyNotFoundException();

        if (file.Length == 0)
            throw new ArgumentException("File is empty.");

        var maxMb = _config.GetValue<int>("Upload:MaxFileSizeMb", 20);
        if (file.Length > maxMb * 1024L * 1024L)
            throw new ArgumentException($"File exceeds the maximum allowed size of {maxMb} MB.");

        if (!_extractor.IsSupported(file.FileName))
        {
            var ext = Path.GetExtension(file.FileName);
            throw new NotSupportedException(
                $"File type '{ext}' is not supported. Accepted: .txt, .md, .docx");
        }

        // Save file to disk
        var dir = Path.Combine(
            _env.ContentRootPath, "uploads", "assessment-files", assessmentId.ToString());
        Directory.CreateDirectory(dir);

        var stored = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var absPath = Path.Combine(dir, stored);

        await using (var stream = File.Create(absPath))
            await file.CopyToAsync(stream);

        // Extract text
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
            extractionError = ex.Message;
        }

        // Relative path stored in DB
        var relPath = Path.Combine(
            "uploads", "assessment-files", assessmentId.ToString(), stored);

        var record = new AssessmentFile
        {
            AssessmentId = assessmentId,
            FileType     = fileType,
            OriginalFileName = file.FileName,
            StoragePath  = relPath,
            MimeType     = file.ContentType,
            Size         = (int)file.Length,
            ExtractedText    = extractedText,
            ExtractionStatus = extractionStatus,
            ExtractionError  = extractionError
        };

        _db.AssessmentFiles.Add(record);

        if (fileType == "QUESTION")
            assessment.QuestionText = extractedText;
        else
            assessment.GuideText = extractedText;

        assessment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Map(record);
    }

    public async Task<AssessmentFileResponse?> GetLatestAsync(
        Guid teacherId,
        Guid assessmentId,
        string fileType)
    {
        var owns = await _db.Assessments
            .AnyAsync(a => a.Id == assessmentId && a.TeacherId == teacherId);

        if (!owns) return null;

        var record = await _db.AssessmentFiles
            .Where(f => f.AssessmentId == assessmentId && f.FileType == fileType)
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync();

        return record is null ? null : Map(record);
    }

    private static AssessmentFileResponse Map(AssessmentFile f) => new()
    {
        Id               = f.Id,
        AssessmentId     = f.AssessmentId,
        FileType         = f.FileType,
        OriginalFileName = f.OriginalFileName,
        Size             = f.Size,
        ExtractedText    = f.ExtractedText,
        ExtractionStatus = f.ExtractionStatus,
        ExtractionError  = f.ExtractionError,
        CreatedAt        = f.CreatedAt
    };
}
