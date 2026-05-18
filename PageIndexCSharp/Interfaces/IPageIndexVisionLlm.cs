namespace PageIndexCSharp.Interfaces;

/// <summary>
/// 多模态 LLM 抽象：在文本提示词之外接受一张图片，用于把 PDF 渲染页转成 Markdown 等场景。
/// </summary>
public interface IPageIndexVisionLlm
{
    /// <summary>
    /// 发送提示词 + 一张图片，返回模型生成的纯文本 / Markdown。
    /// </summary>
    /// <param name="prompt">提示词。</param>
    /// <param name="imageBytes">图片二进制内容。</param>
    /// <param name="mediaType">MIME，如 image/png、image/jpeg。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<string> CompleteWithImageAsync(
        string prompt,
        byte[] imageBytes,
        string mediaType,
        CancellationToken cancellationToken = default);
}
