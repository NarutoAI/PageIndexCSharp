using PageIndexCSharp.Model;

namespace PageIndexCSharp.Store;

/// <summary>
/// 基于内存字典的 PageIndex 文档存储默认实现。
/// </summary>
public sealed class InMemoryPageIndexDocumentStore : IPageIndexDocumentStore
{
    private readonly Dictionary<string, PageIndexDocument> _documents = [];

    /// <inheritdoc />
    public Task SaveAsync(PageIndexDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(document.Id))
        {
            throw new ArgumentException("Document id cannot be empty.", nameof(document));
        }

        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PageIndexDocument?> GetAsync(string docId, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(docId, out PageIndexDocument? document);
        return Task.FromResult(document);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<PageIndexDocument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<PageIndexDocument> documents = _documents.Values.ToList();
        return Task.FromResult(documents);
    }

    /// <inheritdoc />
    public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_documents.Count == 0);
    }
}
