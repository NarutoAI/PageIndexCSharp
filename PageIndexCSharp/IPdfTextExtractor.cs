using PageIndexCSharp.Model;

namespace PageIndexCSharp;

/// <summary>
/// PDF 文本提取抽象，页码必须从 1 开始。
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>
    /// 读取 PDF 并返回逐页文本。
    /// </summary>
    IReadOnlyList<PdfPageContent> ExtractPages(string pdfPath);
}
