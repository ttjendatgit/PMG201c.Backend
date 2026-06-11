using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMG201c.Backend.DTOs.Submissions;
using PMG201c.Backend.Services;

namespace PMG201c.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/assessments/{assessmentId:guid}/submissions")]
public class SubmissionsController : ControllerBase
{
    private readonly SubmissionService _service;

    public SubmissionsController(SubmissionService service)
    {
        _service = service;
    }

    /// <summary>Upload one or more student submission files for an assessment.</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        Guid assessmentId,
        [FromForm(Name = "files")] List<IFormFile> files)
    {
        if (files is null || files.Count == 0)
            return BadRequest(new { message = "No files provided. Use form field name 'files'." });

        try
        {
            var (successes, errors) = await _service.UploadAsync(GetTeacherId(), assessmentId, files);
            return Ok(new UploadSubmissionsResponse
            {
                Uploaded    = successes.Count,
                Failed      = errors.Count,
                Submissions = successes,
                Errors      = errors
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Get all submissions for an assessment (extractedText truncated to 200 chars).</summary>
    [HttpGet]
    public async Task<IActionResult> GetList(Guid assessmentId)
    {
        var result = await _service.GetListAsync(GetTeacherId(), assessmentId);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Get a single submission with full extractedText.</summary>
    [HttpGet("/api/submissions/{submissionId:guid}")]
    public async Task<IActionResult> GetDetail(Guid submissionId)
    {
        var result = await _service.GetDetailAsync(GetTeacherId(), submissionId);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Delete a submission and its stored file.</summary>
    [HttpDelete("/api/submissions/{submissionId:guid}")]
    public async Task<IActionResult> Delete(Guid submissionId)
    {
        var result = await _service.DeleteAsync(GetTeacherId(), submissionId);
        return result is null ? NotFound() : NoContent();
    }

    private Guid GetTeacherId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.Parse(value!);
    }
}
