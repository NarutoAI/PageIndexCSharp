using System.Text.Json;
using PageIndexCSharp;
using PageIndexCSharp.Model;
using PageIndexCSharp.Store;

namespace PageIndexCSharp.Tests;

public sealed class UnitTest1
{
    [Fact]
    public void ParseFlatItems_ShouldReadPhysicalIndexTag()
    {
        string llmJson = """
            [
              { "structure": "1", "title": "Introduction", "physical_index": "<physical_index_2>" },
              { "structure": "1.1", "title": "Background", "physical_index": "<physical_index_3>" }
            ]
            """;

        List<PageIndexFlatItem> items = PageIndexJsonUtilities.ParseFlatItems(llmJson);

        Assert.Equal(2, items.Count);
        Assert.Equal("Introduction", items[0].Title);
        Assert.Equal(2, items[0].PhysicalIndex);
        Assert.Equal("1.1", items[1].Structure);
        Assert.Equal(3, items[1].PhysicalIndex);
    }

    [Fact]
    public void BuildTree_ShouldCreateHierarchyAndInferEndIndex()
    {
        List<PageIndexFlatItem> items =
        [
            new() { Structure = "1", Title = "Introduction", PhysicalIndex = 1 },
            new() { Structure = "1.1", Title = "Background", PhysicalIndex = 2 },
            new() { Structure = "2", Title = "Method", PhysicalIndex = 4 }
        ];

        List<PageIndexNode> tree = PageIndexJsonUtilities.BuildTree(items, pageCount: 6);

        Assert.Equal(2, tree.Count);
        Assert.Equal("0000", tree[0].NodeId);
        Assert.Equal(1, tree[0].StartIndex);
        Assert.Equal(1, tree[0].EndIndex);
        Assert.NotNull(tree[0].Nodes);
        Assert.Single(tree[0].Nodes!);
        Assert.Equal("Background", tree[0].Nodes![0].Title);
        Assert.Equal(2, tree[0].Nodes![0].StartIndex);
        Assert.Equal(3, tree[0].Nodes![0].EndIndex);
        Assert.Equal(4, tree[1].StartIndex);
        Assert.Equal(6, tree[1].EndIndex);
    }

    [Fact]
    public void CloneWithoutText_ShouldRemoveTextRecursively()
    {
        List<PageIndexNode> nodes =
        [
            new()
            {
                Title = "Root",
                NodeId = "0000",
                StartIndex = 1,
                EndIndex = 2,
                Text = "root text",
                Nodes =
                [
                    new() { Title = "Child", NodeId = "0001", StartIndex = 2, EndIndex = 2, Text = "child text" }
                ]
            }
        ];

        List<PageIndexNode> cloned = PageIndexJsonUtilities.CloneWithoutText(nodes);
        string json = JsonSerializer.Serialize(cloned, PageIndexJsonUtilities.JsonOptions);

        Assert.DoesNotContain("text", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Root", json, StringComparison.Ordinal);
        Assert.Contains("Child", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilePageIndexDocumentStore_ShouldPersistAndLoadDocument()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "page-index-csharp-tests", Guid.NewGuid().ToString("N"));
        FilePageIndexDocumentStore store = new(directoryPath);
        PageIndexDocument document = new()
        {
            Id = "doc-1",
            Type = "pdf",
            Path = "/tmp/demo.pdf",
            DocName = "demo.pdf",
            PageCount = 1,
            Structure =
            [
                new() { Title = "Intro", NodeId = "0000", StartIndex = 1, EndIndex = 1, Summary = "summary" }
            ],
            Pages =
            [
                new() { Page = 1, Content = "hello" }
            ]
        };

        Assert.True(await store.IsEmptyAsync());

        await store.SaveAsync(document);
        PageIndexDocument? loaded = await store.GetAsync("doc-1");
        IReadOnlyCollection<PageIndexDocument> allDocuments = await store.GetAllAsync();

        Assert.False(await store.IsEmptyAsync());
        Assert.NotNull(loaded);
        Assert.Equal("demo.pdf", loaded.DocName);
        Assert.Single(allDocuments);
        Assert.Equal("Intro", allDocuments.Single().Structure.Single().Title);
    }
}
