using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using PageIndexCSharp.Parsing;

namespace PageIndexCSharp.Extractors;

/// <summary>
/// Markdown 文档索引构建器，将标题段落映射为逻辑页并生成 PageIndex 结构。
/// </summary>
public sealed class MarkdownTextExtractor : IPageIndexDocumentBuilder
{
    /// <inheritdoc />
    public bool Can(string documentPath)
    {
        string extension = Path.GetExtension(documentPath);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<PageIndexBuildResult> BuildAsync(
        string documentPath,
        PageIndexOptions options,
        CancellationToken cancellationToken = default,
        IProgress<PageIndexProgress>? progress = null)
    {
        PageIndexBuildResult result = new()
        {
            Pages = MarkdownPageIndexParser.ExtractPages(documentPath),
            Structure = MarkdownPageIndexParser.BuildStructure(documentPath)
        };

        return Task.FromResult(result);
    }
}