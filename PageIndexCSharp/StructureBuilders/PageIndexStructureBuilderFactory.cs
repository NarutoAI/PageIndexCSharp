using PageIndexCSharp.Interfaces;

namespace PageIndexCSharp.StructureBuilders;

/// <summary>
/// 默认 PageIndex 结构构建器工厂，优先使用用户自定义构建器，再使用内置构建器。
/// </summary>
public sealed class PageIndexStructureBuilderFactory : IPageIndexStructureBuilderFactory
{
    private readonly IReadOnlyList<IPageIndexStructureBuilder> _builders;

    /// <summary>
    /// 创建默认 PageIndex 结构构建器工厂。
    /// </summary>
    public PageIndexStructureBuilderFactory(
        IPageIndexLlm llm,
        IEnumerable<IPageIndexStructureBuilder>? customStructureBuilders = null)
    {
        List<IPageIndexStructureBuilder> builders = [];

        if (customStructureBuilders is not null)
        {
            builders.AddRange(customStructureBuilders);
        }

        builders.Add(new MarkdownStructureBuilder());
        builders.Add(new LlmPageIndexStructureBuilder(llm));
        _builders = builders;
    }

    /// <inheritdoc />
    public IPageIndexStructureBuilder GetBuilder(string documentPath)
    {
        IPageIndexStructureBuilder? builder = _builders.FirstOrDefault(item => item.CanBuild(documentPath));
        if (builder is null)
        {
            throw new NotSupportedException($"Unsupported document type: {Path.GetExtension(documentPath)}");
        }

        return builder;
    }
}
