# PageIndexCSharp

## 项目说明

此项目是PageIndex的c#版本的实现，目的是让c#开发也能享受PageIndex的无向量库的Rag知识检索方式
项目当前包含以下核心部分：

- `IPageIndexDocumentStore`：页面索引文档存储接口，主要职责是将LLM提取过后的文件索引json存储
- `FilePageIndexDocumentStore`：基于文件系统的文档存储实现
- `PageIndexTools`：对外提供给智能体使用的检索工具集合
- `IPageIndexLlm`：大语言模型抽象接口
- `MafPageIndexLlm`：当前项目中的 LLM 实现
- `PageIndexSearchAgentFactory`：用于创建检索智能体的工厂类

---

## 核心设计

### 1. 文档存储

PageIndex数据通过 `IPageIndexDocumentStore` 进行访问。
当前默认实现为：

- `FilePageIndexDocumentStore`

它负责从本地目录读取PageIndex的json文档信息。

### 2. 工具注册

`PageIndexTools` 封装了供智能体调用的工具方法，当前已注册的工具包括：

- `GetAllDocumentAsync`：获取全部文档列表
- `GetDocumentStructureAsync`：获取指定文档结构
- `GetPageContentAsync`：获取指定页面内容

### 3. 检索智能体工厂

`PageIndexSearchAgentFactory` 用于创建一个带有上述 3 个工具方法的检索智能体。

这样可以保持职责清晰：

- `MafPageIndexLlm` 负责 LLM 能力本身
- `PageIndexTools` 负责业务工具方法
- `PageIndexSearchAgentFactory` 负责创建并组装检索智能体

---

## 使用方法

### 1. 创建文档存储

```csharp
IPageIndexDocumentStore store = new FilePageIndexDocumentStore("./pageindex-store");
```

### 2. 创建 LLM 实例

```csharp
IPageIndexLlm llm = MafPageIndexLlm.FromOpenAI(...);
```

实际初始化参数请根据你当前使用的模型配置填写。

### 3. 创建页面索引检索智能体

```csharp
AIAgent agent = PageIndexSearchAgentFactory.Create((MafPageIndexLlm)llm, store);
```

如果你直接持有的是 `MafPageIndexLlm` 实例，也可以这样写：

```csharp
MafPageIndexLlm llm = new MafPageIndexLlm(...);
AIAgent agent = PageIndexSearchAgentFactory.Create(llm, store);
```

### 4. 通过智能体进行查询

创建好 agent 后，即可将用户问题交给该智能体，由它自动调用 `PageIndexTools` 中注册的工具完成检索。

---
