using PageIndexCSharp;
using PageIndexCSharp.Llm;
using PageIndexCSharp.Store;

// 计算仓库根目录，用于读取 local.env 和 PageIndex 文档存储。
string repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory());
DotNetEnv.Env.Load(Path.Combine(repositoryRoot, "local.env"));

OpenAIPageIndexLlm llm = OpenAIPageIndexLlm.FromResponseOpenAI(
    GetRequiredEnvironmentVariable("OpenApiUrl"),
    GetRequiredEnvironmentVariable("Apikey"),
    GetRequiredEnvironmentVariable("Model"));

FilePageIndexDocumentStore documentStore = new(repositoryRoot);
var agent = PageIndexSearchAgentFactory.CreateSearchAgent(llm, documentStore);

ConsoleChatSession chatSession = new(agent, new StreamingMarkdownConsoleRenderer());
await chatSession.RunAsync();

/// <summary>
/// 从起始目录向上查找仓库根目录，避免依赖脆弱的字符串切割。
/// </summary>
/// <param name="startDirectory">用于开始查找的目录。</param>
/// <returns>包含 PageIndexCSharp.slnx 的仓库根目录。</returns>
/// <exception cref="InvalidOperationException">找不到仓库根目录时抛出。</exception>
static string FindRepositoryRoot(string startDirectory)
{
    DirectoryInfo? directory = new(startDirectory);
    while (directory is not null)
    {
        string solutionPath = Path.Combine(directory.FullName, "PageIndexCSharp.slnx");
        if (File.Exists(solutionPath))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("无法定位仓库根目录，请从 PageIndexCSharp 仓库内运行 ChatConsole。");
}

/// <summary>
/// 读取必需环境变量，缺失时给出明确错误，避免空值继续传入 LLM 初始化流程。
/// </summary>
/// <param name="name">环境变量名称。</param>
/// <returns>环境变量值。</returns>
/// <exception cref="InvalidOperationException">当环境变量未配置时抛出。</exception>
static string GetRequiredEnvironmentVariable(string name)
{
    string? value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"环境变量 {name} 未配置，请检查 local.env。");
    }

    return value;
}
