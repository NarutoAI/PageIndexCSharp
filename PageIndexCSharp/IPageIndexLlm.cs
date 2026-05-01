namespace PageIndexCSharp;

/// <summary>
/// PageIndex 调用 LLM 的抽象，便于测试和替换不同 Maf Agent。
/// </summary>
public interface IPageIndexLlm
{
    /// <summary>
    /// 向 LLM 发送提示词并返回纯文本响应。
    /// </summary>
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
    
}