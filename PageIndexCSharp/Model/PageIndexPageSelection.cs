using System.Text.Json.Serialization;

namespace PageIndexCSharp.Model;

/// <summary>
/// LLM 根据问题和文档结构选择出的候选文档与页面范围。
/// </summary>
public sealed class PageIndexPageSelection
{
    /// <summary>
    /// 被选择的文档 ID。单文档查询时可以为空。
    /// </summary>
    [JsonPropertyName("doc_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocId { get; set; }

    /// <summary>
    /// 页码表达式，支持 5-7、3,8、12。
    /// </summary>
    [JsonPropertyName("pages")]
    public string Pages { get; set; } = string.Empty;

    /// <summary>
    /// 选择这些页面的简要理由。
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }
}
