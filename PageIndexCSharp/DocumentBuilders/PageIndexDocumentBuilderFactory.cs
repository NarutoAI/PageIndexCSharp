using PageIndexCSharp.Extractors;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Store;

namespace PageIndexCSharp.DocumentBuilders;

/// <summary>
/// 默认文档索引构建器工厂，按文档路径选择一体化索引构建实现。
/// </summary>
public sealed class PageIndexDocumentBuilderFactory : IPageIndexDocumentBuilderFactory
{
    private readonly IReadOnlyList<IPageIndexDocumentBuilder> _builders;

    /// <summary>
    /// 创建默认文档索引构建器工厂。
    /// </summary>
    public PageIndexDocumentBuilderFactory(
        IPageIndexLlm llm,
        IEnumerable<IPageIndexDocumentBuilder>? customDocumentBuilders = null,
        IImageStore? imageStore = null)
    {
        ArgumentNullException.ThrowIfNull(llm);

        List<IPageIndexDocumentBuilder> builders = [];

        if (customDocumentBuilders is not null)
        {
            builders.AddRange(customDocumentBuilders);
        }

        builders.Add(new MarkdownTextExtractor());
        builders.Add(new PdfPigTextExtractor(llm, imageStore ?? new FileImageStore()));
        _builders = builders;
    }

    /// <inheritdoc />
    public IPageIndexDocumentBuilder GetBuilder(string documentPath)
    {
        IPageIndexDocumentBuilder? builder = _builders.FirstOrDefault(item => item.Can(documentPath));
        if (builder is null)
        {
            throw new NotSupportedException($"Unsupported document type: {Path.GetExtension(documentPath)}");
        }

        return builder;
    }
}
