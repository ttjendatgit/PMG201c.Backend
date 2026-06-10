using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PMG201c.Backend.Services;

public class FileExtractionService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".docx"
    };

    public bool IsSupported(string fileName)
        => Supported.Contains(Path.GetExtension(fileName));

    public async Task<string> ExtractAsync(string filePath, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
            ".docx"         => ExtractDocx(filePath),
            _               => throw new NotSupportedException($"Unsupported file type: {ext}")
        };
    }

    private static string ExtractDocx(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text);
        }
        return sb.ToString().TrimEnd();
    }
}
