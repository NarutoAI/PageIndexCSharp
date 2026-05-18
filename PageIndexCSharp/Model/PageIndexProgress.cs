namespace PageIndexCSharp.Model;

/// <summary>
/// 表示 PageIndex 文档索引过程中的进度信息。
/// </summary>
public sealed class PageIndexProgress
{
    /// <summary>
    /// 当前索引阶段。
    /// </summary>
    public PageIndexProgressStage Stage { get; init; }

    /// <summary>
    /// 当前进度提示信息。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 当前已处理数量。
    /// </summary>
    public int? Current { get; init; }

    /// <summary>
    /// 当前阶段总数量。
    /// </summary>
    public int? Total { get; init; }

    /// <summary>
    /// 当前阶段完成百分比。
    /// </summary>
    public double? Percent { get; init; }

    /// <summary>
    /// 当前正在处理的节点标题。
    /// </summary>
    public string? CurrentNodeTitle { get; init; }

    /// <summary>
    /// 当前正在处理的页码。
    /// </summary>
    public int? CurrentPage { get; init; }
}
