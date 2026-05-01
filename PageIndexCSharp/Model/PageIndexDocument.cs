using System.Text.Json.Serialization;

namespace PageIndexCSharp.Model;

/// <summary>
/// PageIndex 生成的完整文档索引。
/// </summary>
public sealed class PageIndexDocument
{
    /// <summary>
    /// 文档 ID，由客户端生成并作为检索主键。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 文档类型，目前 C# MVP 支持 pdf。
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "pdf";

    /// <summary>
    /// 原始 PDF 文件路径。
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 文档名称，默认取文件名。
    /// </summary>
    [JsonPropertyName("doc_name")]
    public string DocName { get; set; } = string.Empty;

    /// <summary>
    /// 文档整体描述，由 LLM 根据结构摘要生成。
    /// </summary>
    [JsonPropertyName("doc_description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocDescription { get; set; }

    /// <summary>
    /// PDF 总页数。
    /// </summary>
    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    /// <summary>
    /// 树形目录索引。
    /// </summary>
    [JsonPropertyName("structure")]
    public List<PageIndexNode> Structure { get; set; } = [];

    /// <summary>
    /// PDF 逐页文本，用于 get_page_content 精确取页。
    /// </summary>
    [JsonPropertyName("pages")]
    public List<PdfPageContent> Pages { get; set; } = [];
}
