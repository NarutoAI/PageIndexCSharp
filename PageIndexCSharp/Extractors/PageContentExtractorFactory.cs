using PageIndexCSharp.Interfaces;

namespace PageIndexCSharp.Extractors;

/// <summary>
/// 默认文档内容提取器工厂，优先使用用户自定义提取器，再使用内置提取器。
/// </summary>
public sealed class PageContentExtractorFactory : IPageContentExtractorFactory
{
    private readonly IReadOnlyList<IPageContentExtractor> _extractors;

    /// <summary>
    /// 创建默认文档内容提取器工厂。
    /// </summary>
    public PageContentExtractorFactory(IEnumerable<IPageContentExtractor>? customExtractors = null)
    {
        List<IPageContentExtractor> extractors = [];

        if (customExtractors is not null)
        {
            extractors.AddRange(customExtractors);
        }

        extractors.Add(new MarkdownTextExtractor());
        extractors.Add(new PdfPigTextExtractor());
        _extractors = extractors;
    }

    /// <inheritdoc />
    public IPageContentExtractor GetExtractor(string documentPath)
    {
        IPageContentExtractor? extractor = _extractors.FirstOrDefault(item => item.CanExtract(documentPath));
        if (extractor is null)
        {
            throw new NotSupportedException($"Unsupported document type: {Path.GetExtension(documentPath)}");
        }

        return extractor;
    }
}
