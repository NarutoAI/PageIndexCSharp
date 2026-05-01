using PageIndexCSharp.Model;

namespace PageIndexCSharp.Store;

/// <summary>
/// PageIndex 文档存储抽象，用于支持内存、文件、数据库、远程 API 等多种异步数据源。
/// </summary>
public interface IPageIndexDocumentStore
{
    /// <summary>
    /// 保存或更新文档索引。
    /// </summary>
    Task SaveAsync(PageIndexDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按文档 ID 获取文档索引；不存在时返回 null。
    /// </summary>
    Task<PageIndexDocument?> GetAsync(string docId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前存储中的所有文档。
    /// </summary>
    Task<IReadOnlyCollection<PageIndexDocument>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断存储是否为空。
    /// </summary>
    Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default);
}
