using PageIndexCSharp.Model;
using UglyToad.PdfPig;

namespace PageIndexCSharp;

/// <summary>
/// 使用 UglyToad.PdfPig 实现的 PDF 文本提取器。
/// </summary>
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    /// <inheritdoc />
    public IReadOnlyList<PdfPageContent> ExtractPages(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            throw new ArgumentException("PDF path cannot be empty.", nameof(pdfPath));
        }

        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("PDF file not found.", pdfPath);
        }

        using PdfDocument document = PdfDocument.Open(pdfPath);
        return document.GetPages()
            .OrderBy(page => page.Number)
            .Select(page => new PdfPageContent
            {
                Page = page.Number,
                Content = page.Text ?? string.Empty
            })
            .ToList();
    }
}
