using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using PageIndexCSharp.Parsing;

namespace PageIndexCSharp.StructureBuilders;

/// <summary>
/// Markdown 结构构建器，根据标题层级确定性生成 PageIndex 结构。
/// </summary>
public sealed class MarkdownStructureBuilder : IPageIndexStructureBuilder
{
    /// <inheritdoc />
    public bool CanBuild(string documentPath)
    {
        string extension = Path.GetExtension(documentPath);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<List<PageIndexNode>> BuildAsync(
        string documentPath,
        IReadOnlyList<DocumentPageContent> pages,
        PageIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MarkdownPageIndexParser.BuildStructure(documentPath));
    }
}
