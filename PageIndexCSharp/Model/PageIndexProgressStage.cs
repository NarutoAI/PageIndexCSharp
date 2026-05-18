namespace PageIndexCSharp.Model;

/// <summary>
/// 表示 PageIndex 文档索引过程中的进度阶段。
/// </summary>
public enum PageIndexProgressStage
{
    /// <summary>
    /// 已开始索引文档。
    /// </summary>
    Started,

    /// <summary>
    /// 正在提取文档内容。
    /// </summary>
    ExtractingContent,

    /// <summary>
    /// 文档内容已提取完成。
    /// </summary>
    ContentExtracted,

    /// <summary>
    /// 正在构建文档结构。
    /// </summary>
    BuildingStructure,

    /// <summary>
    /// 文档结构已构建完成。
    /// </summary>
    StructureBuilt,

    /// <summary>
    /// 正在为结构节点挂载正文。
    /// </summary>
    AttachingNodeText,

    /// <summary>
    /// 正在生成结构节点摘要。
    /// </summary>
    SummarizingNodes,

    /// <summary>
    /// 正在生成文档描述。
    /// </summary>
    GeneratingDocumentDescription,

    /// <summary>
    /// 正在保存索引文档。
    /// </summary>
    SavingDocument,

    /// <summary>
    /// 正在处理 PDF Vision 页级内容。
    /// </summary>
    PdfVisionProcessingPage,

    /// <summary>
    /// 正在渲染 PDF 页面图片。
    /// </summary>
    PdfVisionRenderingPage,

    /// <summary>
    /// 正在保存 PDF 页面或嵌入图片。
    /// </summary>
    PdfVisionSavingImages,

    /// <summary>
    /// 正在调用视觉模型生成页面内容。
    /// </summary>
    PdfVisionCallingModel,

    /// <summary>
    /// 正在将视觉模型结果转换为结构。
    /// </summary>
    PdfVisionBuildingStructure,

    /// <summary>
    /// 文档索引已完成。
    /// </summary>
    Completed
}
