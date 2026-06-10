using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMG201c.Backend.DTOs.Assessments;
using PMG201c.Backend.Services;

namespace PMG201c.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/assessments")]
public class AssessmentsController : ControllerBase
{
    private readonly AssessmentService _service;

    public AssessmentsController(AssessmentService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssessmentRequest request)
    {
        var result = await _service.CreateAsync(GetTeacherId(), request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync(GetTeacherId());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(GetTeacherId(), id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssessmentRequest request)
    {
        var result = await _service.UpdateAsync(GetTeacherId(), id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(GetTeacherId(), id);
        return deleted ? NoContent() : NotFound();
    }

    private Guid GetTeacherId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.Parse(value!);
    }
}
