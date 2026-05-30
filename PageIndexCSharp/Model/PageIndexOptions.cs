namespace PageIndexCSharp.Model;

/// <summary>
/// PageIndex 索引配置。
/// </summary>
public sealed class PageIndexOptions
{
    /// <summary>
    /// 是否为每个节点生成摘要。
    /// </summary>
    public bool AddNodeSummary { get; set; } = true;

    /// <summary>
    /// 是否把节点正文写入 structure.text。
    /// </summary>
    public bool AddNodeText { get; set; } = true;

    /// <summary>
    /// 是否生成文档整体描述。
    /// </summary>
    public bool AddDocumentDescription { get; set; } = true;

    /// <summary>
    /// 单次发给 LLM 的最大字符数，用于粗略分块控制上下文。
    /// </summary>
    public int MaxChunkCharacters { get; set; } = 50_000;
}
