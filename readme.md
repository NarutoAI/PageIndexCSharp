# PageIndexCSharp

## 项目说明

此项目是 PageIndex 的 C# 版本实现，目的是让 C# 开发也能使用 PageIndex 的无向量库 RAG 知识检索方式。

项目当前包含以下核心部分：

- `PageIndexClient`：文档索引入口，负责组织文档提取、结构构建、摘要生成和存储。
- `IPageContentExtractor`：文档内容提取接口，用于把不同格式文档提取为页面或逻辑分段内容。
- `IPageContentExtractorFactory`：文档内容提取器工厂，根据文档类型选择提取器。
- `PdfPigTextExtractor`：PDF 文档内容提取实现。
- `MarkdownTextExtractor`：Markdown 文档内容提取实现。
- `IPageIndexStructureBuilder`：页面索引结构构建接口，用于从文档内容生成目录结构。
- `IPageIndexStructureBuilderFactory`：页面索引结构构建器工厂，根据文档类型选择结构构建器。
- `MarkdownStructureBuilder`：Markdown 结构构建实现，直接根据标题层级生成目录结构。
- `LlmPageIndexStructureBuilder`：基于 LLM 的通用结构构建实现，当前 PDF 默认使用该实现。
- `IPageIndexDocumentStore`：页面索引文档存储接口，主要职责是保存和读取 PageIndex 文档 JSON。
- `FilePageIndexDocumentStore`：基于文件系统的文档存储实现。
- `PageIndexTools`：对外提供给智能体使用的检索工具集合。
- `IPageIndexLlm`：大语言模型抽象接口。
- `MafPageIndexLlm`：当前项目中的 LLM 实现。
- `PageIndexSearchAgentFactory`：用于创建检索智能体的工厂类。

---

## 目录结构

```text
PageIndexCSharp/
  Interfaces/
    IPageContentExtractor.cs
    IPageContentExtractorFactory.cs
    IPageIndexDocumentStore.cs
    IPageIndexLlm.cs
    IPageIndexStructureBuilder.cs
    IPageIndexStructureBuilderFactory.cs

  Extractors/
    PdfPigTextExtractor.cs
    MarkdownTextExtractor.cs
    PageContentExtractorFactory.cs

  StructureBuilders/
    MarkdownStructureBuilder.cs
    LlmPageIndexStructureBuilder.cs
    PageIndexStructureBuilderFactory.cs

  Parsing/
    MarkdownPageIndexParser.cs

  Llm/
    MafPageIndexLlm.cs

  Store/
    FilePageIndexDocumentStore.cs
    InMemoryPageIndexDocumentStore.cs

  Model/
    DocumentPageContent.cs
    PageIndexDocument.cs
    PageIndexFlatItem.cs
    PageIndexNode.cs
    PageIndexOptions.cs
    PageIndexPageSelection.cs

  PageIndexClient.cs
  PageIndexSearchAgentFactory.cs
  PageIndexTools.cs
  PageIndexPrompts.cs
  PageIndexJsonUtilities.cs
```

命名空间和目录保持一致，例如：

- `PageIndexCSharp.Interfaces`
- `PageIndexCSharp.Extractors`
- `PageIndexCSharp.StructureBuilders`
- `PageIndexCSharp.Parsing`
- `PageIndexCSharp.Llm`
- `PageIndexCSharp.Store`

---

## 核心设计

### 1. 文档索引流程

`PageIndexClient` 是索引生成入口，整体流程如下：

```text
输入文档路径
  ↓
IPageContentExtractorFactory 选择内容提取器
  ↓
IPageContentExtractor.ExtractPages 提取页面或逻辑分段
  ↓
IPageIndexStructureBuilderFactory 选择结构构建器
  ↓
IPageIndexStructureBuilder.BuildAsync 构建目录结构
  ↓
PageIndexClient 补充节点正文、摘要和文档描述
  ↓
IPageIndexDocumentStore 保存 PageIndex 文档
```

当前内置支持：

- `.pdf`：使用 `PdfPigTextExtractor` 提取内容，然后使用 `LlmPageIndexStructureBuilder` 调用 LLM 生成目录结构。
- `.md` / `.markdown`：使用 `MarkdownTextExtractor` 提取逻辑分段，然后使用 `MarkdownStructureBuilder` 根据标题层级直接生成目录结构。

### 2. 文档内容提取

文档内容提取统一通过 `IPageContentExtractor` 完成：

```csharp
public interface IPageContentExtractor
{
    bool CanExtract(string documentPath);

    IReadOnlyList<DocumentPageContent> ExtractPages(string documentPath);
}
```

默认提取器选择顺序：

```text
用户自定义提取器
  ↓
MarkdownTextExtractor
  ↓
PdfPigTextExtractor
```

因此用户自定义提取器可以覆盖内置的 Markdown 或 PDF 提取行为。

### 3. 页面索引结构构建

页面索引结构构建统一通过 `IPageIndexStructureBuilder` 完成：

```csharp
public interface IPageIndexStructureBuilder
{
    bool CanBuild(string documentPath);

    Task<List<PageIndexNode>> BuildAsync(
        string documentPath,
        IReadOnlyList<DocumentPageContent> pages,
        PageIndexOptions options,
        CancellationToken cancellationToken = default);
}
```

默认结构构建器选择顺序：

```text
用户自定义结构构建器
  ↓
MarkdownStructureBuilder
  ↓
LlmPageIndexStructureBuilder
```

其中：

- `MarkdownStructureBuilder` 不调用 LLM，直接根据 Markdown 标题生成结构。
- `LlmPageIndexStructureBuilder` 是兜底实现，适合 PDF 等需要 LLM 识别目录结构的文档。

### 4. 文档存储

PageIndex 数据通过 `IPageIndexDocumentStore` 进行访问。
当前默认实现包括：

- `InMemoryPageIndexDocumentStore`
- `FilePageIndexDocumentStore`

`FilePageIndexDocumentStore` 负责从本地目录读取和保存 PageIndex 的 JSON 文档信息。

### 5. 工具注册

`PageIndexTools` 封装了供智能体调用的工具方法，当前已注册的工具包括：

- `GetAllDocumentAsync`：获取全部文档列表。
- `GetDocumentStructureAsync`：获取指定文档结构。
- `GetPageContentAsync`：获取指定页面内容。

### 6. 检索智能体工厂

`PageIndexSearchAgentFactory` 用于创建一个带有上述 3 个工具方法的检索智能体。

这样可以保持职责清晰：

- `MafPageIndexLlm` 负责 LLM 能力本身。
- `PageIndexClient` 负责生成 PageIndex 文档。
- `PageIndexTools` 负责业务工具方法。
- `PageIndexSearchAgentFactory` 负责创建并组装检索智能体。

---

## 使用方法

### 1. 引入命名空间

```csharp
using PageIndexCSharp;
using PageIndexCSharp.Extractors;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Llm;
using PageIndexCSharp.Model;
using PageIndexCSharp.Store;
using PageIndexCSharp.StructureBuilders;
```

### 2. 创建文档存储

```csharp
IPageIndexDocumentStore store = new FilePageIndexDocumentStore("./pageindex-store");
```

### 3. 创建 LLM 实例

```csharp
IPageIndexLlm llm = MafPageIndexLlm.FromOpenAI(...);
```

实际初始化参数请根据你当前使用的模型配置填写。

### 4. 创建 PageIndexClient 并生成索引

使用默认 PDF / Markdown 支持：

```csharp
PageIndexClient client = new PageIndexClient(
    llm,
    documentStore: store);

string documentId = await client.IndexAsync("./docs/example.pdf");
```

Markdown 文档也可以直接索引：

```csharp
string documentId = await client.IndexAsync("./docs/example.md");
```

### 5. 传入自定义内容提取器

如果需要支持 Word、HTML 或其他格式，可以实现 `IPageContentExtractor`：

```csharp
public sealed class HtmlTextExtractor : IPageContentExtractor
{
    public bool CanExtract(string documentPath)
    {
        return Path.GetExtension(documentPath)
            .Equals(".html", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<DocumentPageContent> ExtractPages(string documentPath)
    {
        // 提取 HTML 内容并转换为 DocumentPageContent。
        throw new NotImplementedException();
    }
}
```

然后传入 `PageIndexClient`：

```csharp
PageIndexClient client = new PageIndexClient(
    llm,
    customExtractors: [
        new HtmlTextExtractor()
    ],
    documentStore: store);
```

### 6. 传入自定义结构构建器

如果某种文档不需要 LLM 生成目录，也可以实现 `IPageIndexStructureBuilder`：

```csharp
public sealed class HtmlStructureBuilder : IPageIndexStructureBuilder
{
    public bool CanBuild(string documentPath)
    {
        return Path.GetExtension(documentPath)
            .Equals(".html", StringComparison.OrdinalIgnoreCase);
    }

    public Task<List<PageIndexNode>> BuildAsync(
        string documentPath,
        IReadOnlyList<DocumentPageContent> pages,
        PageIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        // 根据 HTML 标题结构生成 PageIndexNode 树。
        throw new NotImplementedException();
    }
}
```

然后传入 `PageIndexClient`：

```csharp
PageIndexClient client = new PageIndexClient(
    llm,
    customExtractors: [
        new HtmlTextExtractor()
    ],
    customStructureBuilders: [
        new HtmlStructureBuilder()
    ],
    documentStore: store);
```

### 7. 创建页面索引检索智能体

```csharp
MafPageIndexLlm mafLlm = new MafPageIndexLlm(...);
AIAgent agent = PageIndexSearchAgentFactory.Create(mafLlm, store);
```

创建好 agent 后，即可将用户问题交给该智能体，由它自动调用 `PageIndexTools` 中注册的工具完成检索。

---
