namespace PageIndexCSharp.Interfaces;

/// <summary>
/// 文档内容提取器工厂，根据文档路径选择可用的提取实现。
/// </summary>
public interface IPageContentExtractorFactory
{
    /// <summary>
    /// 获取支持指定文档路径的内容提取器。
    /// </summary>
    IPageContentExtractor GetExtractor(string documentPath);
}
