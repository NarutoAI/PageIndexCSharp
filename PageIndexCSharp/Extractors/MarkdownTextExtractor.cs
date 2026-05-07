using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using PageIndexCSharp.Parsing;

namespace PageIndexCSharp.Extractors;

/// <summary>
/// Markdown 文档文本提取器，将标题段落映射为逻辑页。
/// </summary>
public sealed class MarkdownTextExtractor : IPageContentExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string documentPath)
    {
        string extension = Path.GetExtension(documentPath);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<DocumentPageContent> ExtractPages(string documentPath)
    {
        return MarkdownPageIndexParser.ExtractPages(documentPath);
    }
}
