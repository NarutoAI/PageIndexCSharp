namespace PageIndexCSharp.Interfaces;

/// <summary>
/// 文档索引构建器工厂，根据文档路径选择可用的一体化索引构建实现。
/// </summary>
public interface IPageIndexDocumentBuilderFactory
{
    /// <summary>
    /// 获取支持指定文档路径的文档索引构建器。
    /// </summary>
    IPageIndexDocumentBuilder GetBuilder(string documentPath);
}
