using System.Text.Json.Serialization;

namespace PageIndexCSharp.Model;

/// <summary>
/// LLM 生成的扁平目录项，对应 Python 版中的 structure/title/physical_index。
/// </summary>
public sealed class PageIndexFlatItem
{
    /// <summary>
    /// 层级编号，例如 1、1.1、2.3.1。
    /// </summary>
    [JsonPropertyName("structure")]
    public string? Structure { get; set; }

    /// <summary>
    /// 章节标题。
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 章节开始所在的 PDF 物理页码。
    /// </summary>
    [JsonPropertyName("physical_index")]
    public int? PhysicalIndex { get; set; }
}
