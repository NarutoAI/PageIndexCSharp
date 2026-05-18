using System.Text;
using System.Text.Json;
using PageIndexCSharp.DocumentBuilders;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using PageIndexCSharp.Store;

namespace PageIndexCSharp;

/// <summary>
/// PageIndex C# 客户端：负责文档索引生成和后续按结构/页码检索。
/// </summary>
public sealed class PageIndexClient
{
    private readonly IPageIndexLlm _llm;
    private readonly IPageIndexDocumentBuilderFactory _documentBuilderFactory;
    private readonly IPageIndexDocumentStore _documentStore;

    /// <summary>
    /// 创建 PageIndex 客户端。
    /// </summary>
    public PageIndexClient(
        IPageIndexLlm llm,
        IPageIndexDocumentStore? documentStore = null,
        IImageStore? imageStore = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _documentBuilderFactory = new PageIndexDocumentBuilderFactory(_llm, imageStore: imageStore);
        _documentStore = documentStore ?? new InMemoryPageIndexDocumentStore();
    }

    /// <summary>
    /// 创建 PageIndex 客户端，并允许传入自定义一体化文档索引构建器。
    /// </summary>
    public PageIndexClient(
        IPageIndexLlm llm,
        IPageIndexDocumentBuilder documentBuilder,
        IPageIndexDocumentStore? documentStore = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _documentBuilderFactory = new PageIndexDocumentBuilderFactory(
            _llm,
            customDocumentBuilders: [documentBuilder ?? throw new ArgumentNullException(nameof(documentBuilder))]);
        _documentStore = documentStore ?? new InMemoryPageIndexDocumentStore();
    }

    /// <summary>
    /// 创建 PageIndex 客户端，并允许传入多个自定义一体化文档索引构建器。
    /// </summary>
    public PageIndexClient(
        IPageIndexLlm llm,
        IEnumerable<IPageIndexDocumentBuilder> customDocumentBuilders,
        IPageIndexDocumentStore? documentStore = null,
        IImageStore? imageStore = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _documentBuilderFactory = new PageIndexDocumentBuilderFactory(
            _llm,
            customDocumentBuilders: customDocumentBuilders,
            imageStore: imageStore);
        _documentStore = documentStore ?? new InMemoryPageIndexDocumentStore();
    }

    /// <summary>
    /// 索引文档文件，返回生成的 doc_id。
    /// </summary>
    public Task<string> IndexAsync(string documentPath, PageIndexOptions? options = null, CancellationToken cancellationToken = default)
    {
        return IndexAsync(documentPath, options, progress: null, cancellationToken);
    }

    /// <summary>
    /// 索引文档文件，返回生成的 doc_id，并通过进度回调报告当前索引阶段。
    /// </summary>
    public async Task<string> IndexAsync(
        string documentPath,
        PageIndexOptions? options,
        IProgress<PageIndexProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        options ??= new PageIndexOptions();
        string fullPath = Path.GetFullPath(documentPath);
        string documentType = GetDocumentType(fullPath);

        ReportProgress(progress, PageIndexProgressStage.Started, "开始索引文档。");
        ReportProgress(progress, PageIndexProgressStage.ExtractingContent, "正在提取文档内容。");
        ReportProgress(progress, PageIndexProgressStage.BuildingStructure, "正在构建文档结构。");

        IPageIndexDocumentBuilder documentBuilder = _documentBuilderFactory.GetBuilder(fullPath);
        PageIndexBuildResult buildResult = await documentBuilder
            .BuildAsync(fullPath, options, cancellationToken, progress)
            .ConfigureAwait(false);

        IReadOnlyList<DocumentPageContent> pages = buildResult.Pages;
        List<PageIndexNode> structure = buildResult.Structure;

        if (pages.Count == 0)
        {
            throw new InvalidOperationException("Document contains no readable pages.");
        }

        ReportProgress(progress, PageIndexProgressStage.ContentExtracted, $"文档内容提取完成，共 {pages.Count} 页或分段。", pages.Count, pages.Count, 100);
        ReportProgress(progress, PageIndexProgressStage.StructureBuilt, "文档结构构建完成。");

        string docId = Guid.NewGuid().ToString();

        if (options.AddNodeText || options.AddNodeSummary)
        {
            ReportProgress(progress, PageIndexProgressStage.AttachingNodeText, "正在为结构节点挂载正文。");
            AddNodeText(structure, pages);
        }

        if (options.AddNodeSummary)
        {
            await AddNodeSummariesAsync(structure, progress, cancellationToken).ConfigureAwait(false);
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
            ReportProgress(progress, PageIndexProgressStage.GeneratingDocumentDescription, "正在生成文档描述。");
            string structureJson = JsonSerializer.Serialize(PageIndexJsonUtilities.CloneWithoutText(structure), PageIndexJsonUtilities.JsonOptions);
            document.DocDescription = await _llm.CompleteAsync(PageIndexPrompts.BuildDocumentDescriptionPrompt(structureJson), cancellationToken).ConfigureAwait(false);
        }

        ReportProgress(progress, PageIndexProgressStage.SavingDocument, "正在保存索引文档。");
        await _documentStore.SaveAsync(document, cancellationToken).ConfigureAwait(false);
        ReportProgress(progress, PageIndexProgressStage.Completed, "文档索引完成。", 1, 1, 100);
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


    private async Task AddNodeSummariesAsync(
        IEnumerable<PageIndexNode> nodes,
        IProgress<PageIndexProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<PageIndexNode> nodesToSummarize = Flatten(nodes)
            .Where(node => !string.IsNullOrWhiteSpace(node.Text))
            .ToList();

        if (nodesToSummarize.Count == 0)
        {
            ReportProgress(progress, PageIndexProgressStage.SummarizingNodes, "没有需要生成摘要的结构节点。", 0, 0, 100);
            return;
        }

        for (int index = 0; index < nodesToSummarize.Count; index++)
        {
            PageIndexNode node = nodesToSummarize[index];
            int current = index + 1;
            double percent = current * 100d / nodesToSummarize.Count;
            string text = node.Text ?? string.Empty;

            ReportProgress(
                progress,
                PageIndexProgressStage.SummarizingNodes,
                $"正在生成节点摘要：{node.Title}，{current}/{nodesToSummarize.Count}。",
                current,
                nodesToSummarize.Count,
                percent,
                node.Title);

            node.Summary = await _llm.CompleteAsync(PageIndexPrompts.BuildNodeSummaryPrompt(node.Title, text), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ReportProgress(
        IProgress<PageIndexProgress>? progress,
        PageIndexProgressStage stage,
        string message,
        int? current = null,
        int? total = null,
        double? percent = null,
        string? currentNodeTitle = null)
    {
        progress?.Report(new PageIndexProgress
        {
            Stage = stage,
            Message = message,
            Current = current,
            Total = total,
            Percent = percent,
            CurrentNodeTitle = currentNodeTitle
        });
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
