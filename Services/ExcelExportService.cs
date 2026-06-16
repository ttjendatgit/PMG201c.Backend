using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Data;
using PMG201c.Backend.Models;

namespace PMG201c.Backend.Services;

public class ExcelExportService
{
    private readonly AppDbContext _db;

    public ExcelExportService(AppDbContext db) => _db = db;

    public async Task<(byte[] Bytes, string FileName)?> ExportAsync(Guid teacherId, Guid assessmentId)
    {
        var assessment = await _db.Assessments
            .Include(a => a.RubricItems)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.TeacherId == teacherId);

        if (assessment is null) return null;

        var allResults = await _db.GradingResults
            .Include(r => r.Submission)
            .Include(r => r.Items)
            .Where(r => r.AssessmentId == assessmentId)
            .ToListAsync();

        // One row per submission: keep the latest GradingResult per submissionId
        var results = allResults
            .GroupBy(r => r.SubmissionId)
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .OrderBy(r => r.Submission.StudentId ?? "~")  // nulls last
            .ThenBy(r => r.Submission.StudentName ?? "")
            .ToList();

        var rubricItems = assessment.RubricItems
            .OrderBy(ri => ri.OrderIndex)
            .ThenBy(ri => ri.QuestionNo)
            .ToList();

        var bytes    = BuildWorkbook(results, rubricItems);
        var stamp    = DateTime.UtcNow.ToString("yyyyMMdd_HHmm");
        var fileName = $"PMG201c_Assessment_{assessmentId}_Results_{stamp}.xlsx";

        return (bytes, fileName);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static byte[] BuildWorkbook(List<GradingResult> results, List<RubricItem> rubricItems)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Grading Results");

        // ── Column definitions ────────────────────────────────────────────────
        var fixedHeaders = new[]
        {
            "No.",
            "Student ID",
            "Student Name",
            "Original File Name",
            "Submission Status",
            "Review Status",
            "AI Raw Score",
            "AI Converted Score",
            "Reviewed Raw Score",
            "Reviewed Converted Score",
            "Final Raw Score",
            "Final Converted Score",
            "Teacher Overall Comment",
            "AI Overall Comment",
            "Created At",
            "Updated At"
        };

        // Score column indices among the fixed headers (1-based)
        const int colAiRaw      = 7;
        const int colAiConv     = 8;
        const int colRevRaw     = 9;
        const int colRevConv    = 10;
        const int colFinalRaw   = 11;
        const int colFinalConv  = 12;
        const int colCreatedAt  = 15;
        const int colUpdatedAt  = 16;
        const int dynBase       = 17;   // first dynamic column

        var dynamicHeaders = new List<string>();
        foreach (var ri in rubricItems)
        {
            dynamicHeaders.Add($"Q{ri.QuestionNo} AI Raw");
            dynamicHeaders.Add($"Q{ri.QuestionNo} Reviewed Raw");
            dynamicHeaders.Add($"Q{ri.QuestionNo} Final Raw");
            dynamicHeaders.Add($"Q{ri.QuestionNo} Teacher Comment");
        }

        var allHeaders = fixedHeaders.Concat(dynamicHeaders).ToList();
        int totalCols  = allHeaders.Count;

        // ── Header row ────────────────────────────────────────────────────────
        for (int c = 1; c <= totalCols; c++)
        {
            var cell = ws.Cell(1, c);
            cell.Value = allHeaders[c - 1];
            cell.Style.Font.Bold            = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            cell.Style.Font.FontColor       = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // ── Data rows ─────────────────────────────────────────────────────────
        int dataRow = 2;
        int no      = 1;

        foreach (var gr in results)
        {
            var sub = gr.Submission;

            // Score fallback: Final → Reviewed → AI
            double finalRaw  = gr.FinalRawScore      ?? gr.ReviewedRawScore      ?? gr.TotalRawScore;
            double finalConv = gr.FinalConvertedScore ?? gr.ReviewedConvertedScore ?? gr.TotalConvertedScore;

            int col = 1;

            // No.
            ws.Cell(dataRow, col++).Value = no++;

            // Student ID — only show if it contains at least one letter (real student code)
            WriteText(ws.Cell(dataRow, col++), IsValidStudentId(sub.StudentId) ? sub.StudentId : null);

            // Student Name — filename-without-ext fallback is acceptable per spec
            WriteText(ws.Cell(dataRow, col++), sub.StudentName);

            WriteText(ws.Cell(dataRow, col++), sub.OriginalFileName);
            WriteText(ws.Cell(dataRow, col++), sub.GradingStatus);
            WriteText(ws.Cell(dataRow, col++), gr.ReviewStatus);

            ws.Cell(dataRow, col++).Value = gr.TotalRawScore;
            ws.Cell(dataRow, col++).Value = gr.TotalConvertedScore;
            WriteNullableScore(ws.Cell(dataRow, col++), gr.ReviewedRawScore);
            WriteNullableScore(ws.Cell(dataRow, col++), gr.ReviewedConvertedScore);
            ws.Cell(dataRow, col++).Value = finalRaw;
            ws.Cell(dataRow, col++).Value = finalConv;

            // Comments — only write if non-empty after review
            WriteText(ws.Cell(dataRow, col++), gr.TeacherOverallComment);
            WriteText(ws.Cell(dataRow, col++), gr.AiOverallComment);

            // Dates as DateTime so Excel can format them
            ws.Cell(dataRow, col++).Value = gr.CreatedAt;
            ws.Cell(dataRow, col++).Value = gr.UpdatedAt;

            // Dynamic rubric columns
            foreach (var ri in rubricItems)
            {
                var item = gr.Items.FirstOrDefault(i => i.QuestionNo == ri.QuestionNo);

                double  aiRaw       = item?.AwardedRawScore  ?? 0;
                double? revRaw      = item?.ReviewedRawScore;
                double  itemFinal   = revRaw ?? aiRaw;

                ws.Cell(dataRow, col++).Value = aiRaw;
                WriteNullableScore(ws.Cell(dataRow, col++), revRaw);
                ws.Cell(dataRow, col++).Value = itemFinal;
                WriteText(ws.Cell(dataRow, col++), item?.TeacherComment);
            }

            dataRow++;
        }

        // ── Alternate row shading ─────────────────────────────────────────────
        for (int r = 2; r < dataRow; r++)
        {
            if (r % 2 == 0)
                ws.Range(r, 1, r, totalCols).Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#EBF3FB");
        }

        // ── Column-level number / date formats ────────────────────────────────
        const string scoreFormat = "0.00";
        const string dateFormat  = "yyyy-mm-dd hh:mm";

        foreach (int c in new[] { colAiRaw, colAiConv, colRevRaw, colRevConv, colFinalRaw, colFinalConv })
            ws.Column(c).Style.NumberFormat.Format = scoreFormat;

        foreach (int c in new[] { colCreatedAt, colUpdatedAt })
            ws.Column(c).Style.NumberFormat.Format = dateFormat;

        for (int qi = 0; qi < rubricItems.Count; qi++)
        {
            int b = dynBase + qi * 4;
            ws.Column(b).Style.NumberFormat.Format     = scoreFormat; // AI Raw
            ws.Column(b + 1).Style.NumberFormat.Format = scoreFormat; // Reviewed Raw
            ws.Column(b + 2).Style.NumberFormat.Format = scoreFormat; // Final Raw
            // b+3 is Teacher Comment — text, no format needed
        }

        // ── AutoFilter on the full header range ───────────────────────────────
        ws.Range(1, 1, dataRow - 1, totalCols).SetAutoFilter();

        // ── Freeze header row & auto-fit columns ─────────────────────────────
        ws.SheetView.FreezeRows(1);
        ws.Columns(1, totalCols).AdjustToContents();

        // Clamp widths
        for (int c = 1; c <= totalCols; c++)
        {
            var col = ws.Column(c);
            if (col.Width < 10) col.Width = 10;
            if (col.Width > 55) col.Width = 55;
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    // ── Cell-write helpers ────────────────────────────────────────────────────

    /// <summary>Writes text only if non-null and non-whitespace; leaves cell blank otherwise.</summary>
    private static void WriteText(IXLCell cell, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            cell.Value = value;
        // Do not write "" — leave cell truly blank (Blank.Value default)
    }

    /// <summary>Writes a nullable double; leaves cell blank when null.</summary>
    private static void WriteNullableScore(IXLCell cell, double? value)
    {
        if (value.HasValue)
            cell.Value = value.Value;
    }

    /// <summary>
    /// A valid student ID must contain at least one letter (e.g. "SE170001").
    /// Pure digit strings ("1", "32") or whitespace are rejected.
    /// </summary>
    private static bool IsValidStudentId(string? id)
        => !string.IsNullOrWhiteSpace(id) && id.Any(char.IsLetter);
}
