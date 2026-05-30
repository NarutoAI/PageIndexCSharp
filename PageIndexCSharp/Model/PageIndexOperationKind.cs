namespace PageIndexCSharp.Model;

/// <summary>
/// 表示当前异步调用链正在执行的 PageIndex 操作类型。
/// </summary>
internal enum PageIndexOperationKind
{
    /// <summary>
    /// 未进入明确的 PageIndex 操作上下文。
    /// </summary>
    None,

    /// <summary>
    /// 正在通过 <see cref="PageIndexClient.IndexAsync(string, PageIndexOptions?, CancellationToken)"/> 构建文档索引。
    /// </summary>
    Indexing
}
