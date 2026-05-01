using System.Text;
using System.Text.Json;
using PageIndexCSharp.Model;
using PageIndexCSharp.Store;

namespace PageIndexCSharp;

/// <summary>
/// PageIndex C# 客户端：负责 PDF 索引生成和后续按结构/页码检索。
/// </summary>
public sealed class PageIndexClient
{
    private readonly IPageIndexLlm _llm;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IPageIndexDocumentStore _documentStore;

    /// <summary>
    /// 创建 PageIndex 客户端。
    /// </summary>
    public PageIndexClient(
        IPageIndexLlm llm,
        IPdfTextExtractor? pdfTextExtractor = null,
        IPageIndexDocumentStore? documentStore = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _pdfTextExtractor = pdfTextExtractor ?? new PdfPigTextExtractor();
        _documentStore = documentStore ?? new InMemoryPageIndexDocumentStore();
    }
    /// <summary>
    /// 索引 PDF 文件，返回生成的 doc_id。
    /// </summary>
    public async Task<string> IndexAsync(string pdfPath, PageIndexOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new PageIndexOptions();
        string fullPath = Path.GetFullPath(pdfPath);
        IReadOnlyList<PdfPageContent> pages = _pdfTextExtractor.ExtractPages(fullPath);
        if (pages.Count == 0)
        {
            throw new InvalidOperationException("PDF contains no readable pages.");
        }

        string docId = Guid.NewGuid().ToString();
        string pagesText = BuildTaggedPagesText(pages, options.MaxChunkCharacters);
        string tocJson = await _llm.CompleteAsync(PageIndexPrompts.BuildGenerateTocPrompt(pagesText), cancellationToken).ConfigureAwait(false);
        List<PageIndexFlatItem> flatItems = PageIndexJsonUtilities.ParseFlatItems(tocJson);
        List<PageIndexNode> structure = PageIndexJsonUtilities.BuildTree(flatItems, pages.Count);

        if (options.AddNodeText || options.AddNodeSummary)
        {
            AddNodeText(structure, pages);
        }

        if (options.AddNodeSummary)
        {
            await AddNodeSummariesAsync(structure, cancellationToken).ConfigureAwait(false);
        }

        if (!options.AddNodeText)
        {
            RemoveNodeText(structure);
        }

        PageIndexDocument document = new()
        {
            Id = docId,
            Type = "pdf",
            Path = fullPath,
            DocName = Path.GetFileName(fullPath),
            PageCount = pages.Count,
            Structure = structure,
            Pages = pages.ToList()
        };

        if (options.AddDocumentDescription)
        {
            string structureJson = JsonSerializer.Serialize(PageIndexJsonUtilities.CloneWithoutText(structure), PageIndexJsonUtilities.JsonOptions);
            document.DocDescription = await _llm.CompleteAsync(PageIndexPrompts.BuildDocumentDescriptionPrompt(structureJson), cancellationToken).ConfigureAwait(false);
        }

        await _documentStore.SaveAsync(document, cancellationToken).ConfigureAwait(false);
        return docId;
    }
    
    private static string BuildTaggedPagesText(IReadOnlyList<PdfPageContent> pages, int maxCharacters)
    {
        StringBuilder builder = new();
        foreach (PdfPageContent page in pages)
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

    private async Task AddNodeSummariesAsync(IEnumerable<PageIndexNode> nodes, CancellationToken cancellationToken)
    {
        foreach (PageIndexNode node in Flatten(nodes))
        {
            string text = node.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            node.Summary = await _llm.CompleteAsync(PageIndexPrompts.BuildNodeSummaryPrompt(node.Title, text), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void AddNodeText(IEnumerable<PageIndexNode> nodes, IReadOnlyList<PdfPageContent> pages)
    {
        Dictionary<int, string> pageMap = pages.ToDictionary(page => page.Page, page => page.Content);
        foreach (PageIndexNode node in Flatten(nodes))
        {
            StringBuilder builder = new();
            for (int page = node.StartIndex; page <= node.EndIndex; page++)
            {
                if (pageMap.TryGetValue(page, out string? content))
                {
                    builder.AppendLine(content);
                }
            }

            node.Text = builder.ToString().Trim();
        }
    }

    private static void RemoveNodeText(IEnumerable<PageIndexNode> nodes)
    {
        foreach (PageIndexNode node in Flatten(nodes))
        {
            node.Text = null;
        }
    }

    private static IEnumerable<PageIndexNode> Flatten(IEnumerable<PageIndexNode> nodes)
    {
        foreach (PageIndexNode node in nodes)
        {
            yield return node;
            if (node.Nodes is null)
            {
                continue;
            }

            foreach (PageIndexNode child in Flatten(node.Nodes))
            {
                yield return child;
            }
        }
    }
}
