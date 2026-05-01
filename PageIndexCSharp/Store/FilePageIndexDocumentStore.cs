using System.Text.Json;
using PageIndexCSharp.Model;

namespace PageIndexCSharp.Store;

/// <summary>
/// 基于本地文件系统的 PageIndex 文档存储实现，每个文档独立保存为一个 JSON 文件。
/// </summary>
public sealed class FilePageIndexDocumentStore : IPageIndexDocumentStore
{
    private readonly string _directoryPath;

    /// <summary>
    /// 创建文件存储实例，并确保目标目录存在。
    /// </summary>
    public FilePageIndexDocumentStore(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be empty.", nameof(directoryPath));
        }

        _directoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(_directoryPath);
    }

    /// <inheritdoc />
    public async Task SaveAsync(PageIndexDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(document.Id))
        {
            throw new ArgumentException("Document id cannot be empty.", nameof(document));
        }

        string filePath = GetDocumentFilePath(document.Id);
        string json = JsonSerializer.Serialize(document, PageIndexJsonUtilities.JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PageIndexDocument?> GetAsync(string docId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docId))
        {
            return null;
        }

        string filePath = GetDocumentFilePath(docId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<PageIndexDocument>(stream, PageIndexJsonUtilities.JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<PageIndexDocument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<PageIndexDocument> documents = [];
        foreach (string filePath in Directory.EnumerateFiles(_directoryPath, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using FileStream stream = File.OpenRead(filePath);
            PageIndexDocument? document = await JsonSerializer.DeserializeAsync<PageIndexDocument>(stream, PageIndexJsonUtilities.JsonOptions, cancellationToken).ConfigureAwait(false);
            if (document is not null)
            {
                documents.Add(document);
            }
        }

        return documents;
    }

    /// <inheritdoc />
    public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool isEmpty = !Directory.EnumerateFiles(_directoryPath, "*.json").Any();
        return Task.FromResult(isEmpty);
    }

    private string GetDocumentFilePath(string docId)
    {
        string safeFileName = string.Concat(docId.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return Path.Combine(_directoryPath, safeFileName + ".json");
    }
}
