using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace PMG201c.Backend.Services;

public class FileExtractionService
{
    private readonly ILogger<FileExtractionService> _log;

    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".docx"
    };

    public FileExtractionService(ILogger<FileExtractionService> log)
    {
        _log = log;
    }

    public bool IsSupported(string fileName)
        => Supported.Contains(Path.GetExtension(fileName));

    public async Task<string> ExtractAsync(string filePath, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
            ".docx"         => ExtractDocx(filePath, fileName),
            _               => throw new NotSupportedException($"Unsupported file type: {ext}")
        };
    }

    private string ExtractDocx(string filePath, string fileName)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var sb = new System.Text.StringBuilder();
        int paraCount = 0;
        int tableCount = 0;
        int rowCount = 0;

        // Walk body children in document order so paragraphs and tables are interleaved correctly
        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph para)
            {
                var text = para.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                    paraCount++;
                }
            }
            else if (element is Table table)
            {
                tableCount++;
                foreach (var row in table.Elements<TableRow>())
                {
                    rowCount++;
                    // Collect each cell's text (may contain nested paragraphs)
                    var cellTexts = row.Elements<TableCell>()
                        .Select(cell =>
                        {
                            var parts = cell.Elements<Paragraph>()
                                .Select(p => p.InnerText.Trim())
                                .Where(t => !string.IsNullOrWhiteSpace(t));
                            return string.Join(" ", parts);
                        })
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();

                    if (cellTexts.Count > 0)
                        sb.AppendLine(string.Join(" | ", cellTexts));
                }
            }
        }

        var result = sb.ToString().TrimEnd();

        _log.LogDebug("[FileExtraction] file={File} length={Len} paragraphs={P} tables={T} rows={R}",
            fileName, result.Length, paraCount, tableCount, rowCount);
        _log.LogDebug("[FileExtraction] first500={Text}",
            result.Length > 500 ? result[..500] : result);

        return result;
    }
}
