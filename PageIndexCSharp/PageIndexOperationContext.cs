using System.Threading;
using PageIndexCSharp.Model;

namespace PageIndexCSharp;

/// <summary>
/// 
/// </summary>
internal static class PageIndexOperationContext
{
    private static readonly AsyncLocal<PageIndexOperationKind> CurrentOperation = new();

    /// <summary>
    ///
    /// </summary>
    private static PageIndexOperationKind Current => CurrentOperation.Value;

    /// <summary>
    /// 判断当前是否处于文档索引构建流程。
    /// </summary>
    public static bool IsIndexing => Current == PageIndexOperationKind.Indexing;

    /// <summary>
    /// 进入文档索引构建上下文
    /// </summary>
    public static IDisposable BeginIndexing()
    {
        return Begin(PageIndexOperationKind.Indexing);
    }

    private static IDisposable Begin(PageIndexOperationKind operationKind)
    {
        PageIndexOperationKind previous = CurrentOperation.Value;
        CurrentOperation.Value = operationKind;
        return new OperationScope(previous);
    }

    private sealed class OperationScope(PageIndexOperationKind previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentOperation.Value = previous;
            _disposed = true;
        }
    }
}
