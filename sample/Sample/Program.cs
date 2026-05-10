using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using PageIndexCSharp;
using PageIndexCSharp.Llm;
using PageIndexCSharp.Store;

var dir = Directory.GetCurrentDirectory();

var dir2 = dir.Split("PageIndexCSharp")[0];
DotNetEnv.Env.Load(Path.Combine(dir2, "local.env"));
var mafPageIndexLlm = MafPageIndexLlm.FromOpenAI(Environment.GetEnvironmentVariable("OpenApiUrl"),
    Environment.GetEnvironmentVariable("Apikey"),
    Environment.GetEnvironmentVariable("Model"));

//var pageIndexClient = new PageIndexClient(mafPageIndexLlm, documentStore: new FilePageIndexDocumentStore(dir2));
  //var res = await pageIndexClient.IndexAsync(Path.Combine(dir2,"Claude Code设计指南--洺熙.pdf"));
// Console.WriteLine(res.Answer);

AIAgent agent = PageIndexSearchAgentFactory.CreateSearchAgent(
    mafPageIndexLlm,
    new FilePageIndexDocumentStore(dir2));

bool isReasoning = false;
await foreach (var item in agent.RunStreamingAsync(" SKILL的两种交互的方式是什么？"))
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