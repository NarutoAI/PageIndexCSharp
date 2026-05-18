using System.Text;
using System.Text.RegularExpressions;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Model;
using PageIndexCSharp.Parsing;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PageIndexCSharp.Extractors;

/// <summary>
/// 把每页 PDF 渲染成 PNG 交给多模态模型，让模型直接产出结构化 Markdown，
/// 同时把嵌入图片单独保存到 <see cref="IImageStore"/>，并按阅读顺序回填到文本占位符。
/// </summary>
public sealed class PdfVisionTextExtractor : IPageIndexDocumentBuilder
{
    private readonly IPageIndexVisionLlm _visionLlm;
    private readonly IImageStore _imageStore;
    private readonly PdfVisionExtractorOptions _options;

    /// <summary>
    /// 创建 vision-based PDF 索引构建器。
    /// </summary>
    public PdfVisionTextExtractor(
        IPageIndexVisionLlm visionLlm,
        IImageStore imageStore,
        PdfVisionExtractorOptions? options = null)
    {
        _visionLlm = visionLlm ?? throw new ArgumentNullException(nameof(visionLlm));
        _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
        _options = options ?? new PdfVisionExtractorOptions();
    }

    /// <inheritdoc />
    public bool Can(string documentPath)
        => Path.GetExtension(documentPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<PageIndexBuildResult> BuildAsync(
        string documentPath,
        PageIndexOptions options,
        CancellationToken cancellationToken = default,
        IProgress<PageIndexProgress>? progress = null)
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
        ReportProgress(progress, PageIndexProgressStage.ExtractingContent, "正在读取 PDF 文件。");
        var pdfBytes = await File.ReadAllBytesAsync(documentPath, cancellationToken).ConfigureAwait(false);

        ReportProgress(progress, PageIndexProgressStage.ExtractingContent, "正在打开 PDF 文档。");
        using var document = PdfDocument.Open(pdfBytes);
        var pdfPages = document.GetPages().OrderBy(p => p.Number).ToList();
        var results = new List<DocumentPageContent>(pdfPages.Count);

        foreach (var page in pdfPages)
        {
            int currentPageIndex = results.Count + 1;
            ReportProgress(
                progress,
                PageIndexProgressStage.PdfVisionProcessingPage,
                $"正在处理 PDF 第 {page.Number} 页，{currentPageIndex}/{pdfPages.Count}。",
                currentPageIndex,
                pdfPages.Count,
                CalculatePercent(currentPageIndex - 1, pdfPages.Count),
                page.Number);

            var pageContent = new DocumentPageContent
            {
                Page = page.Number,
                Images = []
            };
            results.Add(pageContent);

            // 1) 整页渲染（按页索引，从 0 开始）
            ReportProgress(
                progress,
                PageIndexProgressStage.PdfVisionRenderingPage,
                $"正在渲染 PDF 第 {page.Number} 页图像。",
                currentPageIndex,
                pdfPages.Count,
                CalculatePercent(currentPageIndex - 1, pdfPages.Count),
                page.Number);
            var pageRenderPng = RenderPageToPng(pdfBytes, page.Number - 1, _options.RenderDpi);

            // 2) 保存整页渲染图，作为 Agent 兜底“看一眼整页”的资源
            ReportProgress(
                progress,
                PageIndexProgressStage.PdfVisionSavingImages,
                $"正在保存 PDF 第 {page.Number} 页渲染图。",
                currentPageIndex,
                pdfPages.Count,
                CalculatePercent(currentPageIndex - 1, pdfPages.Count),
                page.Number);
            var pageRenderPath = await _imageStore.AddAsync(
                pageRenderPng, docName, $"page-{page.Number}.png").ConfigureAwait(false);
            pageContent.Images.Add(pageRenderPath);

            // 3) 提取并按阅读顺序保存嵌入图片
            var embeddedImages = await SaveEmbeddedImagesAsync(page, docName, progress, currentPageIndex, pdfPages.Count).ConfigureAwait(false);
            foreach (var (path, _) in embeddedImages)
            {
                pageContent.Images.Add(path);
            }

            // 4) 构造提示词 + 调用多模态模型
            var prompt = BuildPrompt(page.Number, pdfPages.Count, embeddedImages.Count);
            string markdown;
            try
            {
                ReportProgress(
                    progress,
                    PageIndexProgressStage.PdfVisionCallingModel,
                    $"正在调用视觉模型识别 PDF 第 {page.Number} 页内容。",
                    currentPageIndex,
                    pdfPages.Count,
                    CalculatePercent(currentPageIndex - 1, pdfPages.Count),
                    page.Number);
                markdown = await _visionLlm
                    .CompleteWithImageAsync(prompt, pageRenderPng, "image/png", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (_options.FallbackOnFailure)
            {
                markdown = $"<!-- vision extraction failed on page {page.Number}: {ex.Message} -->";
            }

            // 5) 把 IMG_i / PAGE_RENDER 占位符回填成真实路径
            pageContent.Content = SubstitutePlaceholders(markdown, embeddedImages, pageRenderPath);
            ReportProgress(
                progress,
                PageIndexProgressStage.PdfVisionProcessingPage,
                $"PDF 第 {page.Number} 页内容处理完成，{currentPageIndex}/{pdfPages.Count}。",
                currentPageIndex,
                pdfPages.Count,
                CalculatePercent(currentPageIndex, pdfPages.Count),
                page.Number);
        }

        ReportProgress(progress, PageIndexProgressStage.PdfVisionBuildingStructure, "正在将 Vision 提取结果转换为 PageIndex 结构。", results.Count, results.Count, 100);
        var markdownContent = BuildMarkdownContent(results);
        var structure = MarkdownPageIndexParser.ParseMarkdownTxtInternal(
            Path.GetFileNameWithoutExtension(documentPath),
            markdownContent).Structure;

        return new PageIndexBuildResult
        {
            Pages = results,
            Structure = structure
        };
    }

    private static byte[] RenderPageToPng(byte[] pdfBytes, int pageIndex, int dpi)
    {
        var options = new RenderOptions {Dpi = dpi};
        using var bmp = Conversion.ToImage(pdfBytes, page: new Index(pageIndex), options: options);
        using var ms = new MemoryStream();
        bmp.Encode(ms, SKEncodedImageFormat.Png, 90);
        return ms.ToArray();
    }

    private async Task<List<(string Path, string Marker)>> SaveEmbeddedImagesAsync(
        Page page,
        string docName,
        IProgress<PageIndexProgress>? progress,
        int currentPageIndex,
        int totalPages)
    {
        // 按 PDF 阅读顺序：Top 大的先（PDF 原点在左下），同 Top 的从左到右
        var orderedImages = page.GetImages()
            .OrderByDescending(img => img.BoundingBox.Top)
            .ThenBy(img => img.BoundingBox.Left)
            .ToList();

        if (orderedImages.Count == 0)
        {
            ReportProgress(
                progress,
                PageIndexProgressStage.PdfVisionSavingImages,
                $"PDF 第 {page.Number} 页没有可提取的嵌入图片。",
                currentPageIndex,
                totalPages,
                CalculatePercent(currentPageIndex - 1, totalPages),
                page.Number);
            return [];
        }

        var saved = new List<(string Path, string Marker)>(orderedImages.Count);
        for (var i = 0; i < orderedImages.Count; i++)
        {
            byte[] bytes;
            string ext;
            if (orderedImages[i].TryGetPng(out var pngBytes))
            {
                bytes = pngBytes;
                ext = "png";
            }
            else
            {
                bytes = orderedImages[i].RawBytes.ToArray();
                ext = "jpeg";
            }

            ReportProgress(
                progress,
                PageIndexProgressStage.PdfVisionSavingImages,
                $"正在保存 PDF 第 {page.Number} 页的第 {i + 1}/{orderedImages.Count} 张嵌入图片。",
                currentPageIndex,
                totalPages,
                CalculatePercent(currentPageIndex - 1, totalPages),
                page.Number);

            var fileName = $"img-p{page.Number}-{i}.{ext}";
            var path = await _imageStore.AddAsync(bytes, docName, fileName).ConfigureAwait(false);
            saved.Add((path, $"IMG_{i}"));
        }

        return saved;
    }

    private static string BuildPrompt(int pageNumber, int totalPages, int embeddedImageCount)
    {
        var sb = new StringBuilder();
        sb.Append("请把下面这张图片（PDF 第 ").Append(pageNumber).Append(" 页 / 共 ")
            .Append(totalPages).AppendLine(" 页）的内容转换成结构化 Markdown：");
        sb.AppendLine("- 保留原始的标题层级（# / ## / ###）、有序与无序列表、表格、代码块、引用等结构");
        sb.AppendLine("- 段落之间用空行分隔，不要把多段压缩成一段");

        if (embeddedImageCount > 0)
        {
            sb.Append("- 本页有 ").Append(embeddedImageCount)
                .Append(" 张可独立提取的嵌入图片，按它们在页面上从上到下、从左到右的顺序，")
                .Append("在它们出现的位置插入 `![](IMG_i)`，i 从 0 开始递增（即 IMG_0 到 IMG_")
                .Append(embeddedImageCount - 1).AppendLine("）");
        }

        sb.AppendLine("- 如果页面上有流程图 / 示意图 / 图表等矢量图形（不属于上述嵌入图片），用 `![](PAGE_RENDER)` 标记");
        sb.AppendLine("- 表格请用 Markdown 表格语法还原，单元格内的换行用 <br> 表示");
        sb.AppendLine("- 不要输出任何说明性文字（例如\"这是第 N 页\"、\"以下是 Markdown 内容\"），不要把整段输出包在 ```markdown 代码块里");
        sb.AppendLine("- 只输出 Markdown 正文本身");
        return sb.ToString();
    }

    private static string SubstitutePlaceholders(
        string markdown,
        IReadOnlyList<(string Path, string Marker)> embeddedImages,
        string pageRenderPath)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        markdown = StripOuterCodeFence(markdown);

        for (int i = 0; i < embeddedImages.Count; i++)
        {
            var p = embeddedImages[i].Path.Replace('\\', '/');
            markdown = markdown.Replace(embeddedImages[i].Marker, $"./{p}");
        }

        markdown = markdown.Replace("PAGE_RENDER", $"./{pageRenderPath.Replace('\\', '/')}");

        return markdown.Trim();
    }

    private static string StripOuterCodeFence(string text)
    {
        text = text.Trim();
        var match = Regex.Match(
            text,
            @"^```(?:markdown|md)?\s*\r?\n([\s\S]*?)\r?\n```$",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : text;
    }

    private static string BuildMarkdownContent(IEnumerable<DocumentPageContent> pages)
    {
        StringBuilder builder = new();
        foreach (DocumentPageContent item in pages)
        {
            builder.AppendLine(item.Content);
        }

        return builder.ToString();
    }

    private static double CalculatePercent(int current, int total)
    {
        if (total <= 0)
        {
            return 100;
        }

        return current * 100d / total;
    }

    private static void ReportProgress(
        IProgress<PageIndexProgress>? progress,
        PageIndexProgressStage stage,
        string message,
        int? current = null,
        int? total = null,
        double? percent = null,
        int? currentPage = null)
    {
        progress?.Report(new PageIndexProgress
        {
            Stage = stage,
            Message = message,
            Current = current,
            Total = total,
            Percent = percent,
            CurrentPage = currentPage
        });
    }
}

/// <summary>
/// <see cref="PdfVisionTextExtractor"/> 的可选配置。
/// </summary>
public sealed class PdfVisionExtractorOptions
{
    /// <summary>
    /// 整页渲染的 DPI。150 → 文件小、小字易糊；300 → 清晰但慢且贵。默认 200。
    /// </summary>
    public int RenderDpi { get; init; } = 200;

    /// <summary>
    /// 调用 vision 模型抛异常时是否吞掉错误并写入注释占位（true）；false 则让异常向上抛。
    /// </summary>
    public bool FallbackOnFailure { get; init; } = true;

}