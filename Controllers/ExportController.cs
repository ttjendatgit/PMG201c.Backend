using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMG201c.Backend.Services;
using System.Security.Claims;

namespace PMG201c.Backend.Controllers;

[ApiController]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly ExcelExportService _export;

    public ExportController(ExcelExportService export) => _export = export;

    private Guid TeacherId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Invalid token."));

    /// <summary>
    /// Export all grading results for an assessment to an Excel file.
    /// </summary>
    [HttpGet("api/assessments/{assessmentId:guid}/export/excel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportExcel(Guid assessmentId)
    {
        var result = await _export.ExportAsync(TeacherId, assessmentId);

        if (result is null)
            return NotFound(new { message = "Assessment not found." });

        var (bytes, fileName) = result.Value;

        const string contentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return File(bytes, contentType, fileName);
    }
}
