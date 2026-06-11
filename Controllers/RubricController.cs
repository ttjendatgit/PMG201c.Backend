using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMG201c.Backend.DTOs.Rubric;
using PMG201c.Backend.Services;

namespace PMG201c.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/assessments/{assessmentId:guid}")]
public class RubricController : ControllerBase
{
    private readonly RubricService _service;

    public RubricController(RubricService service)
    {
        _service = service;
    }

    [HttpPost("parse-rubric")]
    public async Task<IActionResult> ParseRubric(Guid assessmentId)
    {
        try
        {
            var result = await _service.ParseRubricAsync(GetTeacherId(), assessmentId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("rubric")]
    public async Task<IActionResult> GetRubric(Guid assessmentId)
    {
        var result = await _service.GetRubricAsync(GetTeacherId(), assessmentId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("rubric")]
    public async Task<IActionResult> UpdateRubric(
        Guid assessmentId,
        [FromBody] UpdateRubricRequest request)
    {
        try
        {
            var result = await _service.UpdateRubricAsync(GetTeacherId(), assessmentId, request);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetTeacherId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.Parse(value!);
    }
}
