using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using PageIndexCSharp;
using PageIndexCSharp.Extractors;
using PageIndexCSharp.Llm;
using PageIndexCSharp.Model;
using PageIndexCSharp.Store;
using Serilog;

var dir = Directory.GetCurrentDirectory();

var dir2 = dir.Split("PageIndexCSharp")[0];
DotNetEnv.Env.Load(Path.Combine(dir2, "local.env"));
 static ILoggerFactory? GetLoggerFactory()
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .MinimumLevel.Is(Serilog.Events.LogEventLevel.Verbose)
        .CreateLogger();
    return LoggerFactory.Create(a => { a.AddSerilog(); });
}
#pragma warning disable OPENAI001
#pragma warning disable MAAI001
 var chatClient = new OpenAIClient(new ApiKeyCredential(Environment.GetEnvironmentVariable("Apikey")), new OpenAIClientOptions()
    {
        Endpoint = new Uri(Environment.GetEnvironmentVariable("OpenApiUrl")),
        NetworkTimeout = TimeSpan.FromMinutes(15),
        //启用日志
        ClientLoggingOptions = new ClientLoggingOptions
        {
            LoggerFactory = GetLoggerFactory(),
            //总开关：启用日志
            EnableLogging = true,
            EnableMessageLogging = true,
            // 记录请求/响应的行与头
            EnableMessageContentLogging = true,
            //记录请求/响应的完整内容
            MessageContentSizeLimit = 64 * 1024
        },
    })
    .GetResponsesClient().AsIChatClientWithStoredOutputDisabled(Environment.GetEnvironmentVariable("Model"));
#pragma warning restore MAAI001
#pragma warning restore OPENAI001
 
 var mafPageIndexLlm=new OpenAIPageIndexLlm(chatClient);

var visionExtractor = new PdfVisionTextExtractor(
    mafPageIndexLlm,                                                 // IPageIndexVisionLlm
    new FileImageStore(),
    new PdfVisionExtractorOptions { RenderDpi = 200 });

var client = new PageIndexClient(
    mafPageIndexLlm,
     visionExtractor,
    documentStore: new FilePageIndexDocumentStore(dir2));

await client.IndexAsync(Path.Combine(dir2,"系统架构设计师教程（第4版）-划重点版本_副本.pdf"),new PageIndexOptions()
{
    MaxChunkCharacters = 2000
});
AIAgent agent = PageIndexSearchAgentFactory.CreateSearchAgent(
    mafPageIndexLlm,
    new FilePageIndexDocumentStore(dir2));

bool isReasoning = false;
await foreach (var item in agent.RunStreamingAsync(" 辅助存储器？"))
{
    if (item.Contents is {Count: > 0} && item.Contents.FirstOrDefault(a => a is TextReasoningContent) != null)
    {
        var textReasoningContent = (TextReasoningContent) item.Contents.FirstOrDefault(a => a is TextReasoningContent);
        if (!isReasoning)
        {
            Console.WriteLine("思考内容：");
            isReasoning = true;
        }

        Console.Write(textReasoningContent.Text);
    }
    else if (!string.IsNullOrWhiteSpace(item.Text))
    {
        if (isReasoning)
        {
            isReasoning = false;

            Console.WriteLine();
            Console.WriteLine("开始回复正文：");
        }

        Console.Write(item.Text);
    }
}


Console.ReadLine();