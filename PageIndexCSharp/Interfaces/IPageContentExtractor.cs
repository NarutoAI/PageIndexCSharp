using PageIndexCSharp.Model;

namespace PageIndexCSharp.Interfaces;

/// <summary>
/// 文档内容提取抽象，页码必须从 1 开始。
/// </summary>
public interface IPageContentExtractor
{
    /// <summary>
    /// 判断当前提取器是否支持指定文档路径。
    /// </summary>
    bool CanExtract(string documentPath);

    /// <summary>
    /// 读取文档并返回逐页内容。
    /// </summary>
    IReadOnlyList<DocumentPageContent> ExtractPages(string documentPath);
}
