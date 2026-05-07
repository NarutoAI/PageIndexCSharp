using System.Text;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;

namespace PageIndexCSharp.StructureBuilders;

/// <summary>
/// 基于 LLM 的通用 PageIndex 结构构建器，适用于 PDF 等无法确定性解析标题层级的文档。
/// </summary>
public sealed class LlmPageIndexStructureBuilder : IPageIndexStructureBuilder
{
    private readonly IPageIndexLlm _llm;

    /// <summary>
    /// 创建基于 LLM 的 PageIndex 结构构建器。
    /// </summary>
    public LlmPageIndexStructureBuilder(IPageIndexLlm llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    /// <inheritdoc />
    public bool CanBuild(string documentPath)
    {
        return true;
    }

    /// <inheritdoc />
    public async Task<List<PageIndexNode>> BuildAsync(
        string documentPath,
        IReadOnlyList<DocumentPageContent> pages,
        PageIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        string pagesText = BuildTaggedPagesText(pages, options.MaxChunkCharacters);
        string tocJson = await _llm.CompleteAsync(PageIndexPrompts.BuildGenerateTocPrompt(pagesText), cancellationToken).ConfigureAwait(false);
        List<PageIndexFlatItem> flatItems = PageIndexJsonUtilities.ParseFlatItems(tocJson);
        return PageIndexJsonUtilities.BuildTree(flatItems, pages.Count);
    }

    private static string BuildTaggedPagesText(IReadOnlyList<DocumentPageContent> pages, int maxCharacters)
    {
        StringBuilder builder = new();
        foreach (DocumentPageContent page in pages)
        {
            string tagged = $"<physical_index_{page.Page}>\n{page.Content}\n<physical_index_{page.Page}>\n\n";
            if (builder.Length + tagged.Length > maxCharacters && builder.Length > 0)
            {
                break;
            }

            builder.Append(tagged);
        }

        return builder.ToString();
    }
}
