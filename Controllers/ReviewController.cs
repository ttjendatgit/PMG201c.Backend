using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMG201c.Backend.DTOs.Grading;
using PMG201c.Backend.DTOs.Review;
using PMG201c.Backend.Services;
using System.Security.Claims;

namespace PMG201c.Backend.Controllers;

[ApiController]
[Authorize]
public class ReviewController : ControllerBase
{
    private readonly ReviewService _review;

    public ReviewController(ReviewService review) => _review = review;

    private Guid TeacherId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Invalid token."));

    // ── POST /api/submissions/{submissionId}/manual-result ───────────────────

    /// <summary>
    /// Create or update a manual grading result when AI grading has failed or produced no result.
    /// If a GradingResult already exists it is updated as REVIEWED; otherwise a new one is created.
    /// </summary>
    [HttpPost("api/submissions/{submissionId:guid}/manual-result")]
    [ProducesResponseType(typeof(GradingResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ManualResult(Guid submissionId, [FromBody] ManualGradingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _review.ManualGradeAsync(TeacherId, submissionId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Submission not found." });
        }
    }

    // ── PUT /api/grading-results/{gradingResultId}/review ────────────────────

    [HttpPut("api/grading-results/{gradingResultId:guid}/review")]
    [ProducesResponseType(typeof(GradingResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review(Guid gradingResultId, [FromBody] ReviewGradingResultRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _review.ReviewAsync(TeacherId, gradingResultId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Grading result not found." });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── POST /api/grading-results/{gradingResultId}/finalize ─────────────────

    [HttpPost("api/grading-results/{gradingResultId:guid}/finalize")]
    [ProducesResponseType(typeof(FinalizeGradingResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Finalize(Guid gradingResultId)
    {
        try
        {
            var response = await _review.FinalizeAsync(TeacherId, gradingResultId);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Grading result not found." });
        }
    }

    // ── GET /api/assessments/{assessmentId}/review-results ───────────────────

    [HttpGet("api/assessments/{assessmentId:guid}/review-results")]
    [ProducesResponseType(typeof(List<ReviewResultSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReviewResults(Guid assessmentId)
    {
        var results = await _review.GetReviewResultsAsync(TeacherId, assessmentId);

        if (results is null)
            return NotFound(new { message = "Assessment not found." });

        return Ok(results);
    }
}
