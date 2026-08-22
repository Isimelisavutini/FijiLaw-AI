using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FijiLaw.Api;

public sealed class DocumentTextExtractor
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt"
    };

    public async Task<string> ExtractAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0) throw new ArgumentException("The uploaded file is empty.");
        if (file.Length > 8 * 1024 * 1024) throw new ArgumentException("The uploaded file exceeds the 8 MB limit.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new ArgumentException("Supported document types are PDF, DOCX and TXT.");

        await using var input = file.OpenReadStream();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory, ct);
        memory.Position = 0;

        var text = extension.ToLowerInvariant() switch
        {
            ".txt" => await ReadTextAsync(memory, ct),
            ".docx" => ReadDocx(memory),
            ".pdf" => ReadPdf(memory),
            _ => throw new ArgumentException("Unsupported document type.")
        };

        text = Normalize(text);
        if (text.Length < 20)
            throw new ArgumentException("Very little readable text was found. Scanned/image-only PDFs are not yet supported by OCR.");

        return text.Length > 60_000 ? text[..60_000] : text;
    }

    private static async Task<string> ReadTextAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static string ReadDocx(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }

    private static string ReadPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(ContentOrderTextExtractor.GetText(page));
        }
        return sb.ToString();
    }

    private static string Normalize(string value)
    {
        var lines = value.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(x => string.Join(' ', x.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join("\n", lines).Trim();
    }
}
