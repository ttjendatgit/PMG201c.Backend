using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMG201c.Backend.Services;

namespace PMG201c.Backend.Controllers;

[Authorize]
[ApiController]
public class GradingController : ControllerBase
{
    private readonly GradingJobService _service;

    public GradingController(GradingJobService service)
    {
        _service = service;
    }

    /// <summary>Start a grading job for all eligible submissions in an assessment.</summary>
    [HttpPost("api/assessments/{assessmentId:guid}/grading-jobs")]
    public async Task<IActionResult> CreateJob(Guid assessmentId)
    {
        try
        {
            var result = await _service.CreateJobAsync(GetTeacherId(), assessmentId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)  { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Get a specific grading job by id.</summary>
    [HttpGet("api/assessments/{assessmentId:guid}/grading-jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJob(Guid assessmentId, Guid jobId)
    {
        var result = await _service.GetJobAsync(GetTeacherId(), assessmentId, jobId);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Get grading status summary for an assessment.</summary>
    [HttpGet("api/assessments/{assessmentId:guid}/grading-status")]
    public async Task<IActionResult> GetStatus(Guid assessmentId)
    {
        var result = await _service.GetStatusAsync(GetTeacherId(), assessmentId);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Grade a single submission (useful for retrying ERROR submissions).</summary>
    [HttpPost("api/submissions/{submissionId:guid}/grade")]
    public async Task<IActionResult> GradeSingle(Guid submissionId)
    {
        try
        {
            var result = await _service.GradeSingleAsync(GetTeacherId(), submissionId);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        // A second grade request arrived while this submission was already
        // being graded — reject instead of racing two AI calls.
        catch (GradingInProgressException ex) { return Conflict(new { message = ex.Message }); }
        // Network/timeout talking to the AI provider (an ERROR GradingResult
        // was already persisted by GradeSingleAsync's own catch block).
        catch (AiTransientException ex)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
        }
        // AI returned content that could not be parsed as valid JSON, or
        // failed the response's business-rule validation — this is a content
        // problem, not a server bug.
        catch (AiParsingException ex) { return UnprocessableEntity(new { message = ex.Message }); }
        catch (AiValidationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Get the latest grading result for a submission.</summary>
    [HttpGet("api/submissions/{submissionId:guid}/grading-result")]
    public async Task<IActionResult> GetResult(Guid submissionId)
    {
        var result = await _service.GetResultAsync(GetTeacherId(), submissionId);
        return result is null ? NotFound() : Ok(result);
    }

    private Guid GetTeacherId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.Parse(value!);
    }
}
