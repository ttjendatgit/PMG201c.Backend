using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMG201c.Backend.DTOs.Assessments;
using PMG201c.Backend.Services;

namespace PMG201c.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/assessments/{assessmentId:guid}")]
public class AssessmentFilesController : ControllerBase
{
    private readonly AssessmentFileService _service;

    public AssessmentFilesController(AssessmentFileService service)
    {
        _service = service;
    }

    [HttpPost("question-file")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AssessmentFileResponse>> UploadQuestion(
        Guid assessmentId,
        IFormFile file)
        => await HandleUpload(assessmentId, file, "QUESTION");

    [HttpPost("guide-file")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AssessmentFileResponse>> UploadGuide(
        Guid assessmentId,
        IFormFile file)
        => await HandleUpload(assessmentId, file, "GUIDE");

    [HttpGet("question")]
    public async Task<IActionResult> GetQuestion(Guid assessmentId)
        => await HandleGet(assessmentId, "QUESTION");

    [HttpGet("guide")]
    public async Task<IActionResult> GetGuide(Guid assessmentId)
        => await HandleGet(assessmentId, "GUIDE");

    // ── private helpers ──────────────────────────────────────────────────────

    private async Task<ActionResult<AssessmentFileResponse>> HandleUpload(
        Guid assessmentId, IFormFile file, string fileType)
    {
        try
        {
            var result = await _service.UploadAsync(GetTeacherId(), assessmentId, file, fileType);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<IActionResult> HandleGet(Guid assessmentId, string fileType)
    {
        var result = await _service.GetLatestAsync(GetTeacherId(), assessmentId, fileType);
        return result is null ? NotFound() : Ok(result);
    }

    private Guid GetTeacherId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.Parse(value!);
    }
}
