using System.Text.Json.Serialization;

namespace PageIndexCSharp.Model;

/// <summary>
/// PageIndex 的树形目录节点，用于后续导航和检索。
/// </summary>
public sealed class PageIndexNode
{
    /// <summary>
    /// 章节标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 节点 ID，格式为 0000、0001 等。
    /// </summary>
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 当前节点覆盖内容的起始页。
    /// </summary>
    [JsonPropertyName("start_index")]
    public int StartIndex { get; set; }

    /// <summary>
    /// 当前节点覆盖内容的结束页。
    /// </summary>
    [JsonPropertyName("end_index")]
    public int EndIndex { get; set; }

    /// <summary>
    /// 当前节点的摘要，由 LLM 根据节点正文生成。
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; set; }

    /// <summary>
    /// 当前节点覆盖的正文文本。检索结构时通常会去掉该字段以减少 token。
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>
    /// 子章节节点。
    /// </summary>
    [JsonPropertyName("nodes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PageIndexNode>? Nodes { get; set; }
}
