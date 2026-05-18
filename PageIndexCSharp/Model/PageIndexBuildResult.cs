namespace PageIndexCSharp.Model;

/// <summary>
/// 表示文档索引构建的中间结果，包含可检索页面内容和层级结构。
/// </summary>
public sealed class PageIndexBuildResult
{
    /// <summary>
    /// 文档逐页或逻辑分段内容，页码必须从 1 开始。
    /// </summary>
    public IReadOnlyList<DocumentPageContent> Pages { get; init; } = [];

    /// <summary>
    /// 根据文档内容构建出的 PageIndex 层级结构。
    /// </summary>
    public List<PageIndexNode> Structure { get; init; } = [];
}
