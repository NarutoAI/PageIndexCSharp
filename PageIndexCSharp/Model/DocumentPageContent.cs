using System.Text.Json.Serialization;

namespace PageIndexCSharp.Model;

/// <summary>
/// 表示文档单页文本内容，页码从 1 开始。
/// </summary>
public sealed class DocumentPageContent
{
    /// <summary>
    /// 文档物理页码，从 1 开始。
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    /// <summary>
    /// 当前页提取出的纯文本内容。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
