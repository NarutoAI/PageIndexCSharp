using System.Text;
using System.Text.Json;
using PageIndexCSharp.Extractors;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using PageIndexCSharp.Store;
using PageIndexCSharp.StructureBuilders;

namespace PageIndexCSharp;

/// <summary>
/// PageIndex C# 客户端：负责文档索引生成和后续按结构/页码检索。
/// </summary>
public sealed class PageIndexClient
{
    private readonly IPageIndexLlm _llm;
    private readonly IPageContentExtractorFactory _pageContentExtractorFactory;
    private readonly IPageIndexStructureBuilderFactory _structureBuilderFactory;
    private readonly IPageIndexDocumentStore _documentStore;

    /// <summary>
    /// 创建 PageIndex 客户端。
    /// </summary>
    public PageIndexClient(
        IPageIndexLlm llm,
        IPageContentExtractor? pageContentExtractor = null,
        IPageIndexDocumentStore? documentStore = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _pageContentExtractorFactory = new PageContentExtractorFactory(pageContentExtractor is null ? null : [pageContentExtractor]);
        _structureBuilderFactory = new PageIndexStructureBuilderFactory(_llm);
        _documentStore = documentStore ?? new InMemoryPageIndexDocumentStore();
    }

    /// <summary>
    /// 创建 PageIndex 客户端，并允许传入多个自定义内容提取器和结构构建器。
    /// </summary>
    public PageIndexClient(
        IPageIndexLlm llm,
        IEnumerable<IPageContentExtractor> customExtractors,
        IPageIndexDocumentStore? documentStore = null,
        IEnumerable<IPageIndexStructureBuilder>? customStructureBuilders = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _pageContentExtractorFactory = new PageContentExtractorFactory(customExtractors);
        _structureBuilderFactory = new PageIndexStructureBuilderFactory(_llm, customStructureBuilders);
        _documentStore = documentStore ?? new InMemoryPageIndexDocumentStore();
    }

    /// <summary>
    /// 创建 PageIndex 客户端，并允许传入自定义内容提取器工厂和结构构建器工厂。
    /// </summary>
    public PageIndexClient(
        IPageIndexLlm llm,
        IPageContentExtractorFactory pageContentExtractorFactory,
        IPageIndexStructureBuilderFactory structureBuilderFactory,
        IPageIndexDocumentStore? documentStore = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _pageContentExtractorFactory = pageContentExtractorFactory ?? throw new ArgumentNullException(nameof(pageContentExtractorFactory));
        _structureBuilderFactory = structureBuilderFactory ?? throw new ArgumentNullException(nameof(structureBuilderFactory));
        _documentStore = documentStore ?? new InMemoryPageIndexDocumentStore();
    }
    /// <summary>
    /// 索引文档文件，返回生成的 doc_id。
    /// </summary>
    public async Task<string> IndexAsync(string documentPath, PageIndexOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new PageIndexOptions();
        string fullPath = Path.GetFullPath(documentPath);
        string documentType = GetDocumentType(fullPath);
        IPageContentExtractor pageContentExtractor = _pageContentExtractorFactory.GetExtractor(fullPath);
        IReadOnlyList<DocumentPageContent> pages = pageContentExtractor.ExtractPages(fullPath);

        if (pages.Count == 0)
        {
            throw new InvalidOperationException("Document contains no readable pages.");
        }

        IPageIndexStructureBuilder structureBuilder = _structureBuilderFactory.GetBuilder(fullPath);
        List<PageIndexNode> structure = await structureBuilder.BuildAsync(fullPath, pages, options, cancellationToken).ConfigureAwait(false);

        string docId = Guid.NewGuid().ToString();

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
            Type = documentType,
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
    
    private static string GetDocumentType(string documentPath)
    {
        string extension = Path.GetExtension(documentPath).ToLowerInvariant();
        return extension switch
        {
            ".md" or ".markdown" => "md",
            ".pdf" => "pdf",
            _ => "pdf"
        };
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

    private static void AddNodeText(IEnumerable<PageIndexNode> nodes, IReadOnlyList<DocumentPageContent> pages)
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
