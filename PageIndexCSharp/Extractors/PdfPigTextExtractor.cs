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

    /// <summary>
    /// 创建基于 PdfPig 的 PDF 文档索引构建器。
    /// </summary>
    public PdfPigTextExtractor(IPageIndexLlm llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
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
        IReadOnlyList<string> chunks = ChunkTaggedPagesText(pages, options.MaxChunkCharacters);

        List<PageIndexFlatItem> allItems = new();
        for (int i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new PageIndexProgress
            {
                Stage = PageIndexProgressStage.BuildingStructure,
                Message = $"正在生成第 {i + 1}/{chunks.Count} 个文本块的目录。",
                Current = i + 1,
                Total = chunks.Count,
                Percent = (i + 1) * 100d / chunks.Count
            });

            string tocJson = await _llm.CompleteAsync(
                PageIndexPrompts.BuildGenerateTocPrompt(chunks[i]),
                cancellationToken).ConfigureAwait(false);
            List<PageIndexFlatItem> chunkItems = PageIndexJsonUtilities.ParseFlatItems(tocJson);
            allItems.AddRange(chunkItems);
        }

        return new PageIndexBuildResult
        {
            Pages = pages,
            Structure = PageIndexJsonUtilities.BuildTree(allItems, pages.Count)
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
        }
        
        return result;
    }

    private static IReadOnlyList<string> ChunkTaggedPagesText(
        IReadOnlyList<DocumentPageContent> pages,
        int maxCharacters)
    {
        if (maxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharacters), "maxCharacters must be positive.");
        }

        List<string> chunks = new();
        StringBuilder current = new();

        foreach (DocumentPageContent page in pages)
        {
            string tagged = $"<physical_index_{page.Page}>\n{page.Content}\n<physical_index_{page.Page}>\n\n";

            if (current.Length > 0 && current.Length + tagged.Length > maxCharacters)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            current.Append(tagged);
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }
}