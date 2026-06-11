using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Data;
using PMG201c.Backend.DTOs.Rubric;
using PMG201c.Backend.Models;

namespace PMG201c.Backend.Services;

public class RubricService
{
    private readonly AppDbContext _db;
    private readonly RubricParserService _parser;

    public RubricService(AppDbContext db, RubricParserService parser)
    {
        _db = db;
        _parser = parser;
    }

    public async Task<List<RubricItemResponse>> ParseRubricAsync(Guid teacherId, Guid assessmentId)
    {
        var assessment = await _db.Assessments
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.TeacherId == teacherId)
            ?? throw new KeyNotFoundException("Assessment not found.");

        if (string.IsNullOrWhiteSpace(assessment.GuideText))
            throw new InvalidOperationException(
                "Assessment has no guide text. Please upload a guide file first.");

        var parsed = _parser.Parse(assessment.GuideText);

        if (parsed.Items.Count == 0)
            throw new InvalidOperationException(
                "No rubric items could be parsed. Ensure the guide uses supported formats " +
                "(e.g. 'Q1 Title - 20 marks', 'Question 1: Title - 20 points', " +
                "'Yêu cầu 1: Tiêu đề - 20 điểm').");

        var totalRaw = parsed.DetectedTotalRawScore ?? parsed.Items.Sum(i => i.MaxRawScore);
        var totalConverted = assessment.TotalConvertedScore;

        await ReplaceRubricItemsAsync(assessmentId, parsed.Items.Select((p, idx) => new RubricItem
        {
            AssessmentId = assessmentId,
            QuestionNo = p.QuestionNo,
            Title = p.Title,
            Description = p.Description,
            MaxRawScore = p.MaxRawScore,
            MaxConvertedScore = ComputeConverted(p.MaxRawScore, totalRaw, totalConverted),
            OrderIndex = idx + 1
        }).ToList());

        assessment.TotalRawScore = totalRaw;
        assessment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetOrderedItemsAsync(assessmentId);
    }

    public async Task<List<RubricItemResponse>?> GetRubricAsync(Guid teacherId, Guid assessmentId)
    {
        var owns = await _db.Assessments
            .AnyAsync(a => a.Id == assessmentId && a.TeacherId == teacherId);

        if (!owns) return null;

        return await GetOrderedItemsAsync(assessmentId);
    }

    public async Task<List<RubricItemResponse>?> UpdateRubricAsync(
        Guid teacherId,
        Guid assessmentId,
        UpdateRubricRequest request)
    {
        var assessment = await _db.Assessments
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.TeacherId == teacherId);

        if (assessment is null) return null;

        if (request.Items.Count == 0)
            throw new ArgumentException("At least one rubric item is required.");

        foreach (var item in request.Items)
        {
            if (item.QuestionNo <= 0)
                throw new ArgumentException("QuestionNo must be greater than 0 for all items.");
            if (string.IsNullOrWhiteSpace(item.Title))
                throw new ArgumentException("Title is required for all items.");
            if (item.MaxRawScore <= 0)
                throw new ArgumentException("MaxRawScore must be greater than 0 for all items.");
        }

        var totalRaw = request.Items.Sum(i => i.MaxRawScore);
        var totalConverted = assessment.TotalConvertedScore;

        await ReplaceRubricItemsAsync(assessmentId, request.Items.Select((req, idx) => new RubricItem
        {
            AssessmentId = assessmentId,
            QuestionNo = req.QuestionNo,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim(),
            MaxRawScore = req.MaxRawScore,
            MaxConvertedScore = ComputeConverted(req.MaxRawScore, totalRaw, totalConverted),
            OrderIndex = req.OrderIndex > 0 ? req.OrderIndex : idx + 1
        }).ToList());

        assessment.TotalRawScore = totalRaw;
        assessment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetOrderedItemsAsync(assessmentId);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task ReplaceRubricItemsAsync(Guid assessmentId, List<RubricItem> newItems)
    {
        var existing = await _db.RubricItems
            .Where(r => r.AssessmentId == assessmentId)
            .ToListAsync();

        _db.RubricItems.RemoveRange(existing);
        _db.RubricItems.AddRange(newItems);
    }

    private async Task<List<RubricItemResponse>> GetOrderedItemsAsync(Guid assessmentId)
    {
        var items = await _db.RubricItems
            .Where(r => r.AssessmentId == assessmentId)
            .OrderBy(r => r.OrderIndex)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    private static double ComputeConverted(double raw, double totalRaw, double totalConverted)
        => totalRaw > 0 ? Math.Round(raw / totalRaw * totalConverted, 2) : 0;

    private static RubricItemResponse Map(RubricItem r) => new()
    {
        Id = r.Id,
        AssessmentId = r.AssessmentId,
        QuestionNo = r.QuestionNo,
        Title = r.Title,
        Description = r.Description,
        MaxRawScore = r.MaxRawScore,
        MaxConvertedScore = r.MaxConvertedScore,
        OrderIndex = r.OrderIndex,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
