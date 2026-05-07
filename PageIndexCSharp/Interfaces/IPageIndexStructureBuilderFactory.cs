namespace PageIndexCSharp.Interfaces;

/// <summary>
/// PageIndex 结构构建器工厂，根据文档路径选择可用的结构构建实现。
/// </summary>
public interface IPageIndexStructureBuilderFactory
{
    /// <summary>
    /// 获取支持指定文档路径的结构构建器。
    /// </summary>
    IPageIndexStructureBuilder GetBuilder(string documentPath);
}
