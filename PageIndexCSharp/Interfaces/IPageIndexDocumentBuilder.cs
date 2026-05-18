using PageIndexCSharp.Model;

namespace PageIndexCSharp.Interfaces;

/// <summary>
/// 文档索引构建抽象，统一负责内容提取和 PageIndex 结构构建。
/// </summary>
public interface IPageIndexDocumentBuilder
{
    /// <summary>
    /// 判断当前构建器是否支持指定文档路径。
    /// </summary>
    bool Can(string documentPath);

    /// <summary>
    /// 读取文档并构建 PageIndex 索引所需的页面内容和层级结构。
    /// </summary>
    Task<PageIndexBuildResult> BuildAsync(
        string documentPath,
        PageIndexOptions options,
        CancellationToken cancellationToken = default);
}
