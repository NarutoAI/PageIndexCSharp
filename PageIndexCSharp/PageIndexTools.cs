using System.ComponentModel;
using System.Text.Json;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using PageIndexCSharp.Store;

namespace PageIndexCSharp;

/// <summary>
/// PageIndex 面向 AIAgent 的工具封装，负责把文档存储中的能力暴露为可被 AIFunctionFactory 识别的方法。
/// </summary>
public sealed class PageIndexTools
{
    private readonly IPageIndexDocumentStore _documentStore;

    /// <summary>
    /// PageIndex Agent 的推荐系统指令，约束模型按文档结构逐步检索内容。
    /// </summary>
    public const string Instructions = """
        您是 PageIndex，一款文档知识检索助手。
        工具使用方法：
        - 首先调用 `GetAllDocumentAsync()` 来确认状态以及页码/行数。
        - 调用 `GetDocumentStructureAsync()` 来确定相关的页码范围。
        - 使用精确的范围（如 pages: 5-7、3,8、12）调用 `GetPageContentAsync()`；切勿获取整个文档。

        补充说明：
        - 信息是否充分？
          - 是：继续回答问题。
          - 否：继续根据上一步 `GetDocumentStructureAsync()` 工具返回的内容查找是否有可用的信息。
        - 一旦收集到足够的信息，生成一个完整的、有充分依据的答案。
        - 不要在一次调用中，一直用同一个docId来调用`GetDocumentStructureAsync()`工具
        """;

    /// <summary>
    /// 创建 PageIndex Agent 工具封装。
    /// </summary>
    public PageIndexTools(IPageIndexDocumentStore documentStore)
    {
        _documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
    }

    /// <summary>
    /// 获取知识库中所有已索引文档的元信息，供 Agent 判断有哪些文档可用。
    /// </summary>
    [Description("获取知识库中所有已索引文档的元信息，包括文档 id、名称、描述、类型、状态和页数。应优先调用该工具确认可用文档。")]
    public async Task<string> GetAllDocumentAsync()
    {
        IReadOnlyCollection<PageIndexDocument> documents = await _documentStore.GetAllAsync().ConfigureAwait(false);
        return JsonSerializer.Serialize(
            documents.Select(document => new
            {
                doc_id = document.Id,
                doc_name = document.DocName,
                doc_description = document.DocDescription ?? string.Empty,
                type = document.Type,
                status = "completed",
                page_count = document.PageCount
            }),
            PageIndexJsonUtilities.JsonOptions);
    }

    /// <summary>
    /// 获取指定文档的目录结构，供 Agent 定位可能相关的页码范围。
    /// </summary>
    [Description("获取指定文档去掉正文 text 字段后的目录结构，用于判断问题相关的页码范围。")]
    public async Task<string> GetDocumentStructureAsync([Description("文档 id，来自 GetAllDocumentAsync 返回的 doc_id。")] string docId)
    {
        PageIndexDocument? document = await _documentStore.GetAsync(docId).ConfigureAwait(false);
        if (document is null)
        {
            return JsonSerializer.Serialize(new { error = $"Document {docId} not found" }, PageIndexJsonUtilities.JsonOptions);
        }

        List<PageIndexNode> structureWithoutText = PageIndexJsonUtilities.CloneWithoutText(document.Structure);
        return JsonSerializer.Serialize(structureWithoutText, PageIndexJsonUtilities.JsonOptions);
    }

    /// <summary>
    /// 获取指定文档的精确页码正文，供 Agent 基于来源内容回答问题。
    /// </summary>
    [Description("获取指定文档的指定页码正文。pages 支持 5-7、3,8、12 等格式；必须使用精确范围，不能获取整个文档。")]
    public async Task<string> GetPageContentAsync(
        [Description("文档 id，来自 GetAllDocumentAsync 返回的 doc_id。")] string docId,
        [Description("页码范围，例如 5-7、3,8、12。")]
        string pages)
    {
        PageIndexDocument? document = await _documentStore.GetAsync(docId).ConfigureAwait(false);
        if (document is null)
        {
            return JsonSerializer.Serialize(new { error = $"Document {docId} not found" }, PageIndexJsonUtilities.JsonOptions);
        }

        try
        {
            HashSet<int> pageNumbers = ParsePages(pages);
            List<DocumentPageContent> selectedPages = document.Pages
                .Where(page => pageNumbers.Contains(page.Page))
                .OrderBy(page => page.Page)
                .ToList();

            return JsonSerializer.Serialize(selectedPages, PageIndexJsonUtilities.JsonOptions);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid pages format: {pages}. Use \"5-7\", \"3,8\", or \"12\". Error: {ex.Message}" }, PageIndexJsonUtilities.JsonOptions);
        }
    }

    private static HashSet<int> ParsePages(string pages)
    {
        if (string.IsNullOrWhiteSpace(pages))
        {
            throw new ArgumentException("Pages cannot be empty.", nameof(pages));
        }

        HashSet<int> result = [];
        foreach (string rawPart in pages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawPart.Contains('-', StringComparison.Ordinal))
            {
                string[] range = rawPart.Split('-', 2, StringSplitOptions.TrimEntries);
                int start = int.Parse(range[0]);
                int end = int.Parse(range[1]);
                if (start > end)
                {
                    throw new ArgumentException($"Invalid range '{rawPart}': start must be <= end.");
                }

                for (int page = start; page <= end; page++)
                {
                    result.Add(page);
                }
            }
            else
            {
                result.Add(int.Parse(rawPart));
            }
        }

        return result;
    }
}
