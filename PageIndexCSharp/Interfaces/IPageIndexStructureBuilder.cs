using PageIndexCSharp.Model;

namespace PageIndexCSharp.Interfaces;

/// <summary>
/// PageIndex 结构构建器，根据文档内容生成层级结构树。
/// </summary>
public interface IPageIndexStructureBuilder
{
    /// <summary>
    /// 判断当前构建器是否支持指定文档路径。
    /// </summary>
    bool CanBuild(string documentPath);

    /// <summary>
    /// 根据文档逻辑页构建层级结构树。
    /// </summary>
    Task<List<PageIndexNode>> BuildAsync(
        string documentPath,
        IReadOnlyList<DocumentPageContent> pages,
        PageIndexOptions options,
        CancellationToken cancellationToken = default);
}
