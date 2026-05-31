using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
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
    return default;
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .MinimumLevel.Is(Serilog.Events.LogEventLevel.Verbose)
        .CreateLogger();
    return LoggerFactory.Create(a => { a.AddSerilog(); });
}

var mafPageIndexLlm = OpenAIPageIndexLlm.FromResponseOpenAI(Environment.GetEnvironmentVariable("OpenApiUrl"),
    Environment.GetEnvironmentVariable("Apikey"), Environment.GetEnvironmentVariable("Model"), GetLoggerFactory());

var visionExtractor = new PdfVisionTextExtractor(
    mafPageIndexLlm, // IPageIndexVisionLlm
    new FileImageStore(),
    new PdfVisionExtractorOptions {RenderDpi = 200});

var client = new PageIndexClient(
    mafPageIndexLlm,
    visionExtractor,
    documentStore: new FilePageIndexDocumentStore(dir2));
Stopwatch stopwatch = Stopwatch.StartNew();
await client.IndexAsync(Path.Combine(dir2, Environment.GetEnvironmentVariable("FileName")), new PageIndexOptions()
{
    // MaxChunkCharacters = 2000
}, progress: new Progress<PageIndexProgress>(a => { Console.WriteLine(a.Message); }));
stopwatch.Stop();
Console.WriteLine($"耗时：{stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine();
Console.ReadLine();