using System.Text;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace PageIndexCSharp.Extractors;

/// <summary>
/// 使用 UglyToad.PdfPig 实现的 PDF 文档索引构建器。
/// </summary>
public sealed class PdfPigTextExtractor : IPageIndexDocumentBuilder
{
    private readonly IPageIndexLlm _llm;
    private readonly IImageStore _imageStore;

    /// <summary>
    /// 创建基于 PdfPig 的 PDF 文档索引构建器。
    /// </summary>
    public PdfPigTextExtractor(IPageIndexLlm llm, IImageStore imageStore)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
    }

    /// <inheritdoc />
    public bool Can(string documentPath)
    {
        return Path.GetExtension(documentPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<PageIndexBuildResult> BuildAsync(
        string documentPath,
        PageIndexOptions options,
        CancellationToken cancellationToken = default,
        IProgress<PageIndexProgress>? progress = null)
    {
        IReadOnlyList<DocumentPageContent> pages = await ExtractPagesAsync(documentPath).ConfigureAwait(false);
        string pagesText = BuildTaggedPagesText(pages, options.MaxChunkCharacters);
        string tocJson = await _llm.CompleteAsync(PageIndexPrompts.BuildGenerateTocPrompt(pagesText), cancellationToken).ConfigureAwait(false);
        List<PageIndexFlatItem> flatItems = PageIndexJsonUtilities.ParseFlatItems(tocJson);

        return new PageIndexBuildResult
        {
            Pages = pages,
            Structure = PageIndexJsonUtilities.BuildTree(flatItems, pages.Count)
        };
    }

    private async Task<IReadOnlyList<DocumentPageContent>> ExtractPagesAsync(string documentPath)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            throw new ArgumentException("Document path cannot be empty.", nameof(documentPath));
        }

        if (!File.Exists(documentPath))
        {
            throw new FileNotFoundException("Document file not found.", documentPath);
        }
        var docName = new FileInfo(documentPath).Name;

        using var document = PdfDocument.Open(documentPath);

        List<DocumentPageContent> result = new(document.NumberOfPages);
        // var index = 0;
        foreach (var page in document.GetPages().OrderBy(page => page.Number))
        {
            var itemDocumentPageContent = new DocumentPageContent
            {
                Page = page.Number,
                Content = ContentOrderTextExtractor.GetText(page) ?? string.Empty,
                Images = []
            };
            result.Add(itemDocumentPageContent);
            // foreach (var itemImage in page.GetImages())
            // {
            //     var fileName = "";
            //
            //     //判断是否为png格式
            //     if (itemImage.TryGetPng(out var bytes))
            //     {
            //         fileName = await _imageStore.AddAsync(bytes, docName,
            //             $"img-{index}.png");
            //     }
            //     else
            //     {
            //         fileName = await _imageStore.AddAsync(itemImage.RawBytes.ToArray(), docName,
            //             $"img-{index}.jpeg");
            //     }
            //
            //     itemDocumentPageContent.Images.Add(fileName);
            //     index++;
            // }
        }
        
        return result;
    }

    private static string BuildTaggedPagesText(IReadOnlyList<DocumentPageContent> pages, int maxCharacters)
    {
        StringBuilder builder = new();
        foreach (DocumentPageContent page in pages)
        {
            string tagged = $"<physical_index_{page.Page}>\n{page.Content}\n<physical_index_{page.Page}>\n\n";
            if (builder.Length + tagged.Length > maxCharacters && builder.Length > 0)
            {
                break;
            }

            builder.Append(tagged);
        }

        return builder.ToString();
    }
}