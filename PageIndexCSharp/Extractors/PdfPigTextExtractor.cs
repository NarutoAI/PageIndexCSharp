using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using UglyToad.PdfPig;

namespace PageIndexCSharp.Extractors;

/// <summary>
/// 使用 UglyToad.PdfPig 实现的 PDF 文本提取器。
/// </summary>
public sealed class PdfPigTextExtractor : IPageContentExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string documentPath)
    {
        return Path.GetExtension(documentPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<DocumentPageContent> ExtractPages(string documentPath)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            throw new ArgumentException("Document path cannot be empty.", nameof(documentPath));
        }

        if (!File.Exists(documentPath))
        {
            throw new FileNotFoundException("Document file not found.", documentPath);
        }

        using PdfDocument document = PdfDocument.Open(documentPath);
        return document.GetPages()
            .OrderBy(page => page.Number)
            .Select(page => new DocumentPageContent
            {
                Page = page.Number,
                Content = page.Text ?? string.Empty
            })
            .ToList();
    }
}
