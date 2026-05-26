# PageIndexCSharp

## 项目说明

PageIndexCSharp 是 PageIndex 的 C# 实现，目标是让 C# 开发也能使用“无向量库”的 RAG 知识检索方式。

当前项目的核心抽象已经统一为 **`IPageIndexDocumentBuilder`**：

- 文档内容提取
- 文档结构构建
- 页面内容与节点结构的组合

都由同一个构建器完成。

---

## 核心组件

- `PageIndexClient`：索引入口，负责文档索引、节点摘要、文档描述和存储。
- `IPageIndexDocumentBuilder`：统一文档索引构建接口。
- `PageIndexDocumentBuilderFactory`：根据文档类型选择可用的构建器。
- `PdfPigTextExtractor`：PDF 文档的默认统一构建器。
- `PdfVisionTextExtractor`：基于视觉模型的 PDF 统一构建器。
- `MarkdownTextExtractor`：Markdown 文档的统一构建器。
- `IPageIndexDocumentStore`：PageIndex 文档存储接口。
- `FilePageIndexDocumentStore`：文件系统存储实现。
- `PageIndexTools`：提供给智能体使用的检索工具。
- `IPageIndexLlm`：大语言模型抽象接口。
- `MafPageIndexLlm`：当前项目中的 LLM 实现。
- `PageIndexSearchAgentFactory`：用于创建检索智能体的工厂类。

---

## 目录结构

```text
PageIndexCSharp/
  Interfaces/
    IPageIndexDocumentBuilder.cs
    IPageIndexDocumentBuilderFactory.cs
    IPageIndexDocumentStore.cs
    IPageIndexLlm.cs
    IPageIndexVisionLlm.cs
    IImageStore.cs

  DocumentBuilders/
    PageIndexDocumentBuilderFactory.cs

  Extractors/
    MarkdownTextExtractor.cs
    PdfPigTextExtractor.cs
    PdfPigTextExtractor2.cs
    PdfVisionTextExtractor.cs

  Parsing/
    MarkdownPageIndexParser.cs

  Llm/
    OpenAIPageIndexLlm.cs

  Store/
    FilePageIndexDocumentStore.cs
    InMemoryPageIndexDocumentStore.cs
    FileImageStore.cs

  Model/
    DocumentPageContent.cs
    PageIndexBuildResult.cs
    PageIndexDocument.cs
    PageIndexFlatItem.cs
    PageIndexNode.cs
    PageIndexOptions.cs
    PageIndexProgress.cs
    PageIndexProgressStage.cs

  PageIndexClient.cs
  PageIndexSearchAgentFactory.cs
  PageIndexTools.cs
  PageIndexPrompts.cs
  PageIndexJsonUtilities.cs
```

命名空间和目录保持一致，例如：

- `PageIndexCSharp.Interfaces`
- `PageIndexCSharp.DocumentBuilders`
- `PageIndexCSharp.Extractors`
- `PageIndexCSharp.Parsing`
- `PageIndexCSharp.Llm`
- `PageIndexCSharp.Store`
- `PageIndexCSharp.Model`

---

## 核心设计

### 1. 文档索引流程

`PageIndexClient` 是索引生成入口，整体流程如下：

```text
输入文档路径
  ↓
PageIndexDocumentBuilderFactory 选择统一构建器
  ↓
IPageIndexDocumentBuilder.BuildAsync 生成页面内容和结构
  ↓
PageIndexClient 补充节点正文、摘要和文档描述
  ↓
IPageIndexDocumentStore 保存 PageIndex 文档
```

当前内置支持：

- `.pdf`：`PdfPigTextExtractor`
- `.pdf`（视觉模式）：`PdfVisionTextExtractor`
- `.md` / `.markdown`：`MarkdownTextExtractor`

### 2. 统一索引构建接口

统一构建抽象如下：

```csharp
public interface IPageIndexDocumentBuilder
{
    bool Can(string documentPath);

    Task<PageIndexBuildResult> BuildAsync(
        string documentPath,
        PageIndexOptions options,
        CancellationToken cancellationToken = default,
        IProgress<PageIndexProgress>? progress = null);
}
```

`PageIndexBuildResult` 包含：

- `Pages`：文档逐页或逻辑分段内容
- `Structure`：PageIndex 层级结构树

### 3. 默认构建器选择

默认选择顺序：

```text
用户自定义一体化构建器
  ↓
MarkdownTextExtractor
  ↓
PdfPigTextExtractor
```

如果你需要视觉 OCR / 多模态 PDF 处理，可以直接传入 `PdfVisionTextExtractor`。

### 4. 文档存储

PageIndex 数据通过 `IPageIndexDocumentStore` 进行访问。

当前默认实现包括：

- `InMemoryPageIndexDocumentStore`
- `FilePageIndexDocumentStore`

### 5. 索引进度通知

`PageIndexClient.IndexAsync` 支持通过 `IProgress<PageIndexProgress>` 获取索引进度。

进度阶段由 `PageIndexProgressStage` 表示，当前包括：

- `Started`
- `ExtractingContent`
- `ContentExtracted`
- `BuildingStructure`
- `StructureBuilt`
- `AttachingNodeText`
- `SummarizingNodes`
- `GeneratingDocumentDescription`
- `SavingDocument`
- `PdfVisionProcessingPage`
- `PdfVisionRenderingPage`
- `PdfVisionSavingImages`
- `PdfVisionCallingModel`
- `PdfVisionBuildingStructure`
- `Completed`

其中 `SummarizingNodes` 阶段会报告：

- `Current`
- `Total`
- `Percent`
- `CurrentNodeTitle`

`PdfVisionTextExtractor` 还会在逐页处理时报告：

- 当前页处理开始 / 完成
- 页面渲染
- 页面渲染图保存
- 嵌入图片保存
- 视觉模型调用
- Vision Markdown 转 PageIndex 结构
- `CurrentPage`

### 6. 工具注册

`PageIndexTools` 封装了供智能体调用的工具方法，当前已注册的工具包括：

- `GetAllDocumentAsync`：获取全部文档列表
- `GetDocumentStructureAsync`：获取指定文档结构
- `GetPageContentAsync`：获取指定页面内容

### 7. 检索智能体工厂

`PageIndexSearchAgentFactory` 用于创建检索智能体。

职责划分如下：

- `OpenAIPageIndexLlm` 负责 openai LLM 能力本身
- `PageIndexClient` 负责生成 PageIndex 文档
- `PageIndexTools` 负责业务工具方法
- `PageIndexSearchAgentFactory` 负责创建并组装检索智能体

---

## 使用方法

### 1. 引入命名空间

```csharp
using PageIndexCSharp;
using PageIndexCSharp.DocumentBuilders;
using PageIndexCSharp.Extractors;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Llm;
using PageIndexCSharp.Model;
using PageIndexCSharp.Store;
```

### 2. 创建文档存储

```csharp
IPageIndexDocumentStore store = new FilePageIndexDocumentStore("./pageindex-store");
```

### 3. 创建 LLM 实例

```csharp
IPageIndexLlm llm = OpenAIPageIndexLlm.FromOpenAI(...);
```

实际初始化参数请根据你当前使用的模型配置填写。

### 4. 创建 PageIndexClient 并生成索引

```csharp
PageIndexClient client = new PageIndexClient(llm, documentStore: store);
string documentId = await client.IndexAsync("./docs/example.pdf");
```

Markdown 文档也可以直接索引：

```csharp
string documentId = await client.IndexAsync("./docs/example.md");
```

如果文件较大，可以传入进度回调：

```csharp
IProgress<PageIndexProgress> progress = new Progress<PageIndexProgress>(item =>
{
    string percent = item.Percent is null ? string.Empty : $" {item.Percent:0.##}%";
    Console.WriteLine($"[{item.Stage}]{percent} {item.Message}");
});

PageIndexOptions options = new()
{
    AddNodeSummary = true,
    AddDocumentDescription = true
};

string documentId = await client.IndexAsync(
    "./docs/example.pdf",
    options,
    progress);
```

### 5. 传入自定义统一构建器

如果你有自己的文档处理方式，直接实现 `IPageIndexDocumentBuilder` 即可：

```csharp
public sealed class HtmlDocumentBuilder : IPageIndexDocumentBuilder
{
    public bool Can(string documentPath)
    {
        return Path.GetExtension(documentPath)
            .Equals(".html", StringComparison.OrdinalIgnoreCase);
    }

    public Task<PageIndexBuildResult> BuildAsync(
        string documentPath,
        PageIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

然后传入 `PageIndexClient`：

```csharp
PageIndexClient client = new PageIndexClient(
    llm,
    new HtmlDocumentBuilder(),
    documentStore: store);
```

或者传入多个自定义构建器：

```csharp
PageIndexClient client = new PageIndexClient(
    llm,
    customDocumentBuilders: [
        new HtmlDocumentBuilder()
    ],
    documentStore: store);
```

### 6. 使用视觉 PDF 构建器

如果你希望 PDF 走视觉模型，可直接使用 `PdfVisionTextExtractor`：

```csharp
IPageIndexDocumentBuilder builder = new PdfVisionTextExtractor(
    visionLlm,
    new FileImageStore(),
    new PdfVisionExtractorOptions { RenderDpi = 200 });

PageIndexClient client = new PageIndexClient(
    llm,
    builder,
    documentStore: store);
```

### 7. 创建页面索引检索智能体

```csharp
OpenAIPageIndexLlm mafLlm = new OpenAIPageIndexLlm(...);
AIAgent agent = PageIndexSearchAgentFactory.Create(mafLlm, store);
```

创建好 agent 后，即可将用户问题交给该智能体，由它自动调用 `PageIndexTools` 中注册的工具完成检索。

---


# 公众号
![](/doc/gzh.jpg)
