using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Data;
using PMG201c.Backend.DTOs.Assessments;
using PMG201c.Backend.Models;

namespace PMG201c.Backend.Services;

public class AssessmentService
{
    private readonly AppDbContext _db;

    public AssessmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AssessmentResponse> CreateAsync(Guid teacherId, CreateAssessmentRequest request)
    {
        var assessment = new Assessment
        {
            TeacherId = teacherId,
            Title = request.Title,
            CourseCode = request.CourseCode,
            Description = request.Description
        };

        _db.Assessments.Add(assessment);
        await _db.SaveChangesAsync();

        return Map(assessment);
    }

    public async Task<List<AssessmentResponse>> GetAllAsync(Guid teacherId)
    {
        var assessments = await _db.Assessments
            .Where(a => a.TeacherId == teacherId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return assessments.Select(Map).ToList();
    }

    public async Task<AssessmentResponse?> GetByIdAsync(Guid teacherId, Guid id)
    {
        var assessment = await _db.Assessments
            .FirstOrDefaultAsync(a => a.Id == id && a.TeacherId == teacherId);

        return assessment is null ? null : Map(assessment);
    }

    public async Task<AssessmentResponse?> UpdateAsync(Guid teacherId, Guid id, UpdateAssessmentRequest request)
    {
        var assessment = await _db.Assessments
            .FirstOrDefaultAsync(a => a.Id == id && a.TeacherId == teacherId);

        if (assessment is null)
            return null;

        if (request.Title is not null) assessment.Title = request.Title;
        if (request.CourseCode is not null) assessment.CourseCode = request.CourseCode;
        if (request.Description is not null) assessment.Description = request.Description;
        if (request.TotalRawScore is not null) assessment.TotalRawScore = request.TotalRawScore.Value;
        if (request.TotalConvertedScore is not null) assessment.TotalConvertedScore = request.TotalConvertedScore.Value;
        if (request.Status is not null) assessment.Status = request.Status;

        assessment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Map(assessment);
    }

    public async Task<bool> DeleteAsync(Guid teacherId, Guid id)
    {
        var assessment = await _db.Assessments
            .FirstOrDefaultAsync(a => a.Id == id && a.TeacherId == teacherId);

        if (assessment is null)
            return false;

        _db.Assessments.Remove(assessment);
        await _db.SaveChangesAsync();
        return true;
    }

    private static AssessmentResponse Map(Assessment a) => new()
    {
        Id = a.Id,
        Title = a.Title,
        CourseCode = a.CourseCode,
        Description = a.Description,
        TotalRawScore = a.TotalRawScore,
        TotalConvertedScore = a.TotalConvertedScore,
        Status = a.Status,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };
}
